// SetCardAction.cs
// Sets a card from hand: (a) Monster Set to MZ[index] face-down DEF, or (b) Set S/T to ST[index] face-down.

using System;
using UnityEngine;
using YGO.Duel.Board;
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
                case SetDestination.MonsterZone:
                {
                    var mz = z.Monsters;
                    if (zoneIndex < 0 || zoneIndex >= mz.Length) { error = "Zone index OOB"; return false; }
                    if (mz[zoneIndex].Top() != null && mz[zoneIndex].Card != null) { error = "Zone occupied"; return false; }

                    if (!hand.Remove(card)) { error = "Failed to remove from hand"; return false; }
                    if (!mz[zoneIndex].Add(card)) { error = "Failed to set to MZ"; return false; }
                    card.CurrentZone = BoardManager.CardZone.Monster;

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
                    card.CurrentZone = BoardManager.CardZone.SpellTrap;

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
