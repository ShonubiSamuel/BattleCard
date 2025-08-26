// SetCardAction.cs
// Sets a card from hand: (a) Monster Set to MZ[index] face-down DEF, or (b) Set S/T to ST[index] face-down.

using System;
using UnityEngine;
using YGO.Duel.Battle;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Foundation;

namespace YGO.Duel.Runtime.Actions
{
    public enum SetDestination { MonsterZone = 0, SpellTrapZone = 1 }

    [Serializable]
    public sealed class SetCardAction : GameAction
    {
        public override ActionType Type => ActionType.SetCard;

        public string handCardId;
        public SetDestination destination = SetDestination.MonsterZone;
        public int zoneIndex = -1;

        public override bool Validate(ActionContext ctx, out string reason)
        {
            reason = "";
            if (string.IsNullOrEmpty(handCardId)) { reason = "Missing handCardId"; return false; }
            if (zoneIndex < 0) { reason = "Invalid zone index"; return false; }
            return true; // deeper checks left to executor/position rules
        }

        public override bool Execute(ActionContext ctx, out string error)
        {
            error = "";
            var card = ActionUtil.ResolveCard(ctx, handCardId, seat, out error);
            if (card == null) return false;

            var z = ctx.Board.Zones[(int)seat];
            var hand = z.Hand;

            if (!hand.Contains(card)) { error = "Card not in hand"; return false; }

            switch (destination)
            {
                // SetCardAction.cs — inside Execute(), after you successfully move the card out of Hand
                // and into the destination zone, add the state + events per destination.

                case SetDestination.MonsterZone:
                {
                    var mz = z.Monsters;
                    if (zoneIndex < 0 || zoneIndex >= mz.Length) { error = "Zone index OOB"; return false; }
                    if (mz[zoneIndex].Top() != null && mz[zoneIndex].Card != null) { error = "Zone occupied"; return false; }

                    if (!hand.Remove(card)) { error = "Failed to remove from hand"; return false; }
                    if (!mz[zoneIndex].Add(card)) { error = "Failed to set to MZ"; return false; }

                    // SetCardAction.cs  — inside Execute(...) after you remove from hand & place into MZ:
                    card.SetPosition(CardBattlePosition.Defense, faceUp: false); // face-down DEF
                    ctx.Turns.MarkNormalSummonUsed(); // ← consume the shared Normal/Set budget


// Keep your controller/zone bookkeeping + EventBus raises:
                    card.SetController(seat);
                    card.CurrentZone = BoardManager.CardZone.Monster;
                    card.ZoneIndex   = zoneIndex;
                    
                    // SetCardAction.Execute(...) — AFTER successful placement to MZ/ST
                    card.MarkEnteredField(ctx.Turns?.TurnNumber ?? 0, wasSet:true);
                    
                    if (ServiceLocator.TryGet<PositionManager>(out var pm) && pm != null)
                    {
                        pm.MarkSetThisTurn(card);
                        pm.SetCanAttackThisTurn(card, false); // common to forbid attacks same turn
                    }

                    
                    if (ServiceLocator.TryGet<EventBus>(out var bus) && bus != null)
                    {
                        var from = new BoardManager.ZoneId(seat, BoardManager.CardZone.Hand);
                        var to   = new BoardManager.ZoneId(seat, BoardManager.CardZone.Monster, zoneIndex);
                        bus.RaiseCardMoved(card, new ZoneMove(from, to));
                        bus.RaiseSummoned(card, seat, SummonType.Normal, zoneIndex); // “Normal” bucket covers Set/NS for now
                    }

                    ctx.Logger.LogText("Action.SetMonster", "Set monster (face-down DEF by rule)",
                        data: $"card={handCardId}; MZ={zoneIndex}; seat={seat}", source: nameof(SetCardAction));
                    return true;
                }

                case SetDestination.SpellTrapZone:
                {
                    var st = z.SpellsTraps;
                    if (zoneIndex < 0 || zoneIndex >= st.Length) { error = "Zone index OOB"; return false; }
                    if (st[zoneIndex].Top() != null && st[zoneIndex].Card != null) { error = "Zone occupied"; return false; }

                    if (!hand.Remove(card)) { error = "Failed to remove from hand"; return false; }
                    if (!st[zoneIndex].Add(card)) { error = "Failed to set to STZ"; return false; }

                    // 🔽 Authoritative runtime state
                    card.SetController(seat);
                    // Face-down S/T set (if you don’t have SetFaceUp, reuse SetPosition(..., faceUp:false))
                    // card.SetFaceUp(false);
                    card.SetPosition(CardBattlePosition.Defense, faceUp: false);
                    card.CurrentZone = BoardManager.CardZone.SpellTrap;
                    card.ZoneIndex   = zoneIndex;
                    
                    // SetCardAction.Execute(...) — AFTER successful placement to MZ/ST
                    card.MarkEnteredField(ctx.Turns?.TurnNumber ?? 0, wasSet:true);
                    
                    if (ServiceLocator.TryGet<PositionManager>(out var pm) && pm != null)
                    {
                        pm.MarkSetThisTurn(card);
                        pm.SetCanAttackThisTurn(card, false); // common to forbid attacks same turn
                    }


                    // 🔽 Tell the world for visuals
                    if (ServiceLocator.TryGet<EventBus>(out var bus) && bus != null)
                    {
                        var from = new BoardManager.ZoneId(seat, BoardManager.CardZone.Hand);
                        var to   = new BoardManager.ZoneId(seat, BoardManager.CardZone.SpellTrap, zoneIndex);
                        bus.RaiseCardMoved(card, new ZoneMove(from, to));
                    }

                    ctx.Logger.LogText("Action.SetST", "Set Spell/Trap (face-down)",
                        data: $"card={handCardId}; ST={zoneIndex}; seat={seat}", source: nameof(SetCardAction));
                    return true;
                }
            }

            error = "Unknown destination";
            return false;
        }
    }
}
