using System.Collections.Generic;
using YGO.Duel.Cards;

namespace YGO.Duel.Foundation
{
    public interface ICardIndex
    {
        Card   Find(string runtimeId);
        string GetId(Card card);
        void   Register(Card card);
        bool   Unregister(Card card);
    }

    /// <summary>Maps runtimeId → Card and back.</summary>
    public sealed class SimpleCardIndex : ICardIndex
    {
        private readonly Dictionary<string, Card> _byId   = new();
        private readonly Dictionary<Card, string> _byCard = new();

        public Card Find(string runtimeId)
        {
            if (string.IsNullOrEmpty(runtimeId)) return null;
            _byId.TryGetValue(runtimeId, out var c);
            return c;
        }

        public string GetId(Card card)
        {
            if (card == null) return null;
            if (_byCard.TryGetValue(card, out var id)) return id;
            return card.InstanceId; // safe fallback
        }

        public void Register(Card card)
        {
            if (card == null) return;
            var id = card.InstanceId;
            if (string.IsNullOrEmpty(id)) return;
            _byId[id] = card;
            _byCard[card] = id;
        }

        public bool Unregister(Card card)
        {
            if (card == null) return false;
            if (!_byCard.TryGetValue(card, out var id)) return false;
            _byCard.Remove(card);
            return _byId.Remove(id);
        }
    }
}