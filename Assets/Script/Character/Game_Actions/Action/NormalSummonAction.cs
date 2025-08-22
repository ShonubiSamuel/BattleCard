// NormalSummonAction.cs
using System;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Foundation; // <-- ADD THIS


namespace YGO.Duel.Runtime.Actions
{
    [Serializable]
    public sealed class NormalSummonAction : GameAction
    {
        public override ActionType Type => ActionType.NormalSummon;

        // Preferred names (kept legacy normalizers for safety)
        public string handCardId;
        public int    monsterZoneIndex = -1;

        private string CardIdNormalized => handCardId;   // 'cardId' if still serialized
        private int    MZIndexNormalized => monsterZoneIndex;     // 'mzIndex' if still serialized

        public override bool Validate(ActionContext ctx, out string reason)
        {
            reason = "";
            if (ctx?.Board == null) { reason = "Board unavailable"; return false; }
            if (ctx?.Turns == null) { reason = "TurnManager unavailable"; return false; }
            if (ctx?.Rules == null) { reason = "RuleSet unavailable"; return false; }

            var id  = CardIdNormalized;
            var idx = MZIndexNormalized;

            // Resolve runtime card (now returns YGO.Duel.Cards.Card)
            var card = ActionUtil.ResolveCard(ctx, id, seat, out reason);
            if (card == null) return false;

            // Must be in actor's hand
            var hand = ctx.Board.Zones[(int)seat].Hand;
            bool inHand =
                hand != null &&
                (
                    (hand.GetType().GetField("Cards") != null &&
                     ((System.Collections.IList)hand.GetType().GetField("Cards").GetValue(hand)).Contains(card))
                 || (hand.GetType().GetMethod("Contains") != null &&
                     (bool)hand.GetType().GetMethod("Contains").Invoke(hand, new object[] { card }))
                 || (hand.GetType().GetMethod("IndexOf") != null &&
                     (int)hand.GetType().GetMethod("IndexOf").Invoke(hand, new object[] { card }) >= 0)
                );
            if (!inHand) { reason = "Card not in hand"; return false; }

            // Target MZ index must exist and be empty
            var mz = ctx.Board.Zones[(int)seat].Monsters;
            if (idx < 0 || idx >= mz.Length) { reason = "Invalid MZ index"; return false; }
            if (!IsMonsterZoneEmpty(mz[idx])) { reason = "Monster Zone is occupied"; return false; }

            // Tribute requirement (basic gate; extend with selection UI later)
            int req = ctx.Rules.GetRequiredTributes(card.Level);
            if (req > 0) { reason = "Tributes required (not provided)"; return false; }

            // RuleSet timing/phase/OPT checks
            var adapters = new ActionPolicyValidator.PlayerRuleAdapters(ctx.Board, ctx.Turns, seat);
            
            // TEMP: log everything the rules might care about
            ctx.Logger?.LogText("NS.Debug",
                $"seat={seat} curPlayer={ctx.Turns?.CurrentPlayer} phase={ctx.Turns?.CurrentPhase} " +
                $"chainEmpty={(ctx.Turns?.IsChainEmpty ?? true)} level={(card.Def?.level ?? 0)} " +
                $"mzEmpty={ctx.Board.Zones[(int)seat].Monsters[idx].Top()==null}",
                source: nameof(NormalSummonAction));

            
            if (!ctx.Rules.CanNormalSummon(adapters.Player, adapters.State, adapters.Board, card.Level))
            {
                reason = "RuleSet rejected Normal Summon at this timing";
                return false;
            }

            return true;
        }

        public override bool Execute(ActionContext ctx, out string error)
        {
            error = "";
            if (!Validate(ctx, out error)) return false;

            var id  = CardIdNormalized;
            var idx = MZIndexNormalized;

            var card = ActionUtil.ResolveCard(ctx, id, seat, out error);
            if (card == null) return false;

            var zones = ctx.Board.Zones[(int)seat];

            // Remove from hand
            if (!TryRemoveFromHand(zones.Hand, card))
            {
                error = "Failed to remove card from hand";
                return false;
            }

            // Place onto Monster Zone
            if (!TryPlaceIntoMonsterZone(zones.Monsters[idx], card))
            {
                error = "Failed to place card into Monster Zone";
                return false;
            }

            // After TryPlaceIntoMonsterZone(...) succeeds
            card.SetPosition(CardBattlePosition.Attack, faceUp: true);   // <-- make it face-up ATK
            // Mark once-per-turn flag
            ctx.Turns.MarkNormalSummonUsed();
            
// Track zone & owner (keep runtime state coherent)
            card.SetController(seat);
            card.CurrentZone = BoardManager.CardZone.Monster;
            card.ZoneIndex   = idx;

// Raise events so UI can update
            if (ServiceLocator.TryGet<EventBus>(out var bus) && bus != null)
            {
                var from = new BoardManager.ZoneId(seat, BoardManager.CardZone.Hand);
                var to   = new BoardManager.ZoneId(seat, BoardManager.CardZone.Monster, idx);
                bus.RaiseCardMoved(card, new ZoneMove(from, to));
                bus.RaiseSummoned(card, seat, SummonType.Normal, idx);
            }

            // Log
            ctx.Logger?.LogText(
                type: "Action.NormalSummon",
                summary: $"Normal Summon {card.Name} → MZ[{idx}]",
                data: $"seat=P{(seat==BoardManager.Seat.P1?1:2)}; turn={turnNumber}",
                source: nameof(NormalSummonAction));

            return true;
        }

        // ----------- helpers (work with current zone APIs) -----------

        private static bool IsMonsterZoneEmpty(object monsterZone)
        {
            if (monsterZone == null) return false;

            var f = monsterZone.GetType().GetField("Card");           // legacy single-slot shape
            if (f != null) return f.GetValue(monsterZone) == null;

            var top = monsterZone.GetType().GetMethod("Top");         // modern Top()
            if (top != null) return top.Invoke(monsterZone, null) == null;

            var emptyProp = monsterZone.GetType().GetProperty("IsEmpty");
            if (emptyProp != null) return (bool)emptyProp.GetValue(monsterZone);

            return false;
        }

        private static bool TryPlaceIntoMonsterZone(object monsterZone, YGO.Duel.Cards.Card card)
        {
            if (monsterZone == null || card == null) return false;

            var f = monsterZone.GetType().GetField("Card"); // old
            if (f != null && f.GetValue(monsterZone) == null) { f.SetValue(monsterZone, card); return true; }

            var set = monsterZone.GetType().GetMethod("Set", new[] { typeof(YGO.Duel.Cards.Card) });
            if (set != null) { set.Invoke(monsterZone, new object[] { card }); return true; }

            var place = monsterZone.GetType().GetMethod("Place", new[] { typeof(YGO.Duel.Cards.Card) });
            if (place != null) { place.Invoke(monsterZone, new object[] { card }); return true; }

            var add = monsterZone.GetType().GetMethod("Add", new[] { typeof(YGO.Duel.Cards.Card) });
            if (add != null) { add.Invoke(monsterZone, new object[] { card }); return true; }

            return false;
        }

        private static bool TryRemoveFromHand(object handZone, YGO.Duel.Cards.Card card)
        {
            if (handZone == null || card == null) return false;

            var remove = handZone.GetType().GetMethod("Remove", new[] { typeof(YGO.Duel.Cards.Card) });
            if (remove != null) return (bool)remove.Invoke(handZone, new object[] { card });

            var cardsField = handZone.GetType().GetField("Cards");
            if (cardsField != null)
            {
                var list = cardsField.GetValue(handZone) as System.Collections.IList;
                if (list != null && list.Contains(card)) { list.Remove(card); return true; }
            }

            var rawListProp = handZone.GetType().GetProperty("RawList");
            if (rawListProp != null)
            {
                var list = rawListProp.GetValue(handZone) as System.Collections.IList;
                if (list != null && list.Contains(card)) { list.Remove(card); return true; }
            }

            return false;
        }
    }
}
