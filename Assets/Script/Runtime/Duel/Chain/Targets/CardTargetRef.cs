using System;
using YGO.Duel.Board;
using YGO.Duel.Chain.YGO.Duel.Chain;
using YGO.Duel.Foundation;
using Card = YGO.Duel.Cards.Card;

namespace YGO.Duel.Chain
{
    /// <summary>
    /// Target reference that identifies a card by a stable id (Card.InstanceId).
    /// It re-resolves the runtime card when asked if it’s still valid.
    /// </summary>
    [Serializable]
    public sealed class CardTargetRef : ITargetRef
    {
        public string Id { get; }
        public string DebugName { get; private set; }
        public object Raw { get; private set; }

        /// <summary>Optional seat hint (for UI/debug only).</summary>
        public BoardManager.Seat SeatHint { get; }

        // Optional custom validity checker; falls back to “card still on field”.
        private readonly Func<Card, bool> _isValid;

        /// <summary>Create from a live Card.</summary>
        public CardTargetRef(Card card, Func<Card, bool> isValid = null)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            Id = card.InstanceId ?? "";
            SeatHint = card.Controller;
            Raw = card;
            DebugName = card.Name ?? $"Card[{Id}]";
            _isValid = isValid ?? (c => c != null && c.IsOnField);
        }

        /// <summary>Create from a stable id (e.g., when you only have the id during UI selection).</summary>
        public CardTargetRef(string instanceId, BoardManager.Seat seatHint = default, Func<Card, bool> isValid = null)
        {
            Id = instanceId ?? "";
            SeatHint = seatHint;
            Raw = null;
            DebugName = $"Card[{Id}]";
            _isValid = isValid ?? (c => c != null && c.IsOnField);
        }

        // ----- Static factories (so your UI code can use FromCard / FromId) -----

        public static CardTargetRef FromCard(Card card) => new CardTargetRef(card);
        public static CardTargetRef FromCard(Card card, Func<Card, bool> isValid) => new CardTargetRef(card, isValid);
        public static CardTargetRef FromId(string instanceId, BoardManager.Seat seatHint = default) => new CardTargetRef(instanceId, seatHint);

        // ----- ITargetRef -----

        public bool IsStillValid()
        {
            var card = Raw as Card;
            if (card == null || string.IsNullOrEmpty(card.InstanceId) || !StringComparer.Ordinal.Equals(card.InstanceId, Id))
            {
                card = TryResolveById(Id);
                Raw = card; // cache (may be null if not found)
                if (card != null) DebugName = card.Name ?? DebugName;
            }
            return _isValid(card);
        }

        private static Card TryResolveById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            // Prefer the index if present
            if (ServiceLocator.TryGet<ICardIndex>(out var idx) && idx != null)
            {
                var c = idx.Find(id);
                if (c != null) return c;
            }

            // Fallback: scan board
            if (ServiceLocator.TryGet<BoardManager>(out var board) && board != null)
            {
                foreach (var c in board.AllCards())
                    if (c != null && StringComparer.Ordinal.Equals(c.InstanceId, id))
                        return c;
            }
            return null;
        }

        public override string ToString() => DebugName ?? $"Card[{Id}]";
        public override int GetHashCode() => (Id ?? "").GetHashCode();
        public override bool Equals(object obj)
            => obj is CardTargetRef other && StringComparer.Ordinal.Equals(Id, other.Id);
    }
}