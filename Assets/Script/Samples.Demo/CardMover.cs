// CardMover.cs
// Authoritative moves between zones + Summon helpers. Fires EventBus notifications.

using System;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Zones;

namespace YGO.Duel.Systems
{
    public sealed class CardMover
    {
        private readonly BoardManager _board;
        private readonly DuelLogger _logger;
        private readonly EventBus _bus;

        public CardMover(BoardManager board, DuelLogger logger = null, EventBus bus = null)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _logger = logger ?? new DuelLogger();
            _bus = bus ?? (ServiceLocator.TryGet<EventBus>(out var b) ? b : null);
        }

        // ---------------- Core move primitive ----------------

        /// <summary>Move exact card reference from 'from' to 'to'. Updates card zone, controller & index. Raises CardMoved.</summary>
        public bool Move(IZone from, IZone to, Card card, out string error)
        {
            error = "";
            if (from == null || to == null || card == null) { error = "Null arg"; return false; }
            if (!from.Contains(card))                        { error = "Source doesn't contain card"; return false; }
            if (!to.Add(card))                               { error = "Destination rejected card"; return false; }
            if (!from.Remove(card))                          { /* try to undo */ to.Remove(card); error = "Failed to remove from source"; return false; }

            // Update card markers
            card.CurrentZone = to.ZoneType;
            card.Controller  = to.Seat;
            card.ZoneIndex   = to.Id.Index;

            _logger.LogText("Move", $"{card.Name} : {from.Id} → {to.Id}", source: nameof(CardMover));
            _bus?.RaiseCardMoved(card, new ZoneMove(from.Id, to.Id));
            return true;
        }

        // ---------------- Convenience: hand -> MZ (Normal Summon) ----------------

        public bool NormalSummonFromHand(BoardManager.Seat seat, Card handCard, int mzIndex, CardBattlePosition pos, bool faceUp, RuleSet rules, Runtime.TurnManager turns, SummonValidator validator, out string error)
        {
            error = "";
            if (validator != null && !validator.CanNormalSummon(seat, handCard, mzIndex, turns, out error))
                return false;

            var hand = _board.Zones[(int)seat].Hand;
            var mz   = _board.Zones[(int)seat].Monsters[mzIndex];

            if (!hand.Contains(handCard)) { error = "Card not in hand"; return false; }
            if (!mz.IsEmpty && mz.Top() != null) { error = "MZ occupied"; return false; }

            // Perform the move
            if (!Move(hand, mz, handCard, out error)) return false;

            // Set on-field state
            handCard.SetPosition(pos, faceUp: faceUp);

            // Mark once per turn
            if (rules != null && turns != null) turns.MarkNormalSummonUsed();

            // Summon event
            _bus?.RaiseSummoned(handCard, seat, SummonType.Normal, mzIndex);

            return true;
        }

        // ---------------- Quick helpers used by destruction/costs ----------------

        public bool SendToGY(BoardManager.Seat seatPerspective, Card card, DestroyReason reason, out string error)
        {
            error = "";
            if (card == null) { error = "Null card"; return false; }

            // Try remove from any controller side, then owner side
            RemoveFromAllKnownZones(card);

            // Owner’s GY (YGO rule)
            var gy = _board.Zones[(int)card.Owner].Graveyard;
            if (!gy.Add(card)) { error = "GY rejected card"; return false; }
            card.CurrentZone = BoardManager.CardZone.Graveyard;
            card.ZoneIndex   = -1;

            _logger.LogText("MoveToGY", $"{card.Name} → Owner GY", data:$"reason={reason}");
            return true;
        }

        public bool ReturnToHand(BoardManager.Seat seat, Card card, out string error)
        {
            error = "";
            if (card == null) { error = "Null card"; return false; }
            RemoveFromAllKnownZones(card);
            var hand = _board.Zones[(int)seat].Hand;
            if (!hand.Add(card)) { error = "Hand rejected card"; return false; }
            card.CurrentZone = BoardManager.CardZone.Hand;
            card.ZoneIndex   = -1;
            return true;
        }

        // ---- internal: brute-force remove wherever it is ----
        private void RemoveFromAllKnownZones(Card card)
        {
            foreach (BoardManager.Seat s in new[] { BoardManager.Seat.P1, BoardManager.Seat.P2 })
            {
                var z = _board.Zones[(int)s];

                // List zones
                z.Hand.Remove(card);
                z.Graveyard.Remove(card);
                z.Banished.Remove(card);
                z.MainDeck.Remove(card);
                z.ExtraDeck.Remove(card);

                // Single-slot zones
                foreach (var mz in z.Monsters) if (ReferenceEquals(mz.Top(), card)) { mz.RemoveTop(); break; }
                foreach (var st in z.SpellsTraps) if (ReferenceEquals(st.Top(), card)) { st.RemoveTop(); break; }
                if (z.Field != null && ReferenceEquals(z.Field.Top(), card)) z.Field.RemoveTop();
                if (z.Pendulum != null)
                {
                    for (int i = 0; i < z.Pendulum.Length; i++)
                        if (ReferenceEquals(z.Pendulum[i].Top(), card)) { z.Pendulum[i].RemoveTop(); break; }
                }
            }
        }
    }
}
