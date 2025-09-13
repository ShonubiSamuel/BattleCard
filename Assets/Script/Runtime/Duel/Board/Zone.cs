// Zone.cs
// Zone containers (multi-card piles and single-slot zones) with a common interface.

using System;
using System.Collections.Generic;
using YGO.Duel.Board;
using static YGO.Duel.Board.BoardManager;
using Card = YGO.Duel.Cards.Card;

namespace YGO.Duel.Zones
{
    /// <summary>Minimal container contract used by BoardManager.FindZone(...).</summary>
    public interface IZone
    {
        Card Top();                              // peek the top/only card; null if empty
        Card RemoveTop();                        // remove top/only card and return it; null if empty   <-- NEW
        bool Add(Card c);                        // add card into this zone
        bool Remove(Card c);                     // remove exact card reference
        bool Contains(Card c);
        int Count { get; }                       // number of cards currently in zone
        CardZone ZoneType { get; }               // semantic kind (Deck, Hand, Monster, etc.)
        Seat Seat { get; }                       // owner seat
        ZoneId Id { get; }                       // global identifier (Seat + Kind + Index)
    }

    /// <summary>Base for zones bound to a specific player seat.</summary>
    public abstract class ZoneBase : IZone
    {
        public Seat  Seat { get; private set; }
        public ZoneId Id  { get; private set; }
        public abstract CardZone ZoneType { get; }
        public abstract int Count { get; }
        protected ZoneBase(Seat seat, ZoneId id) { Seat = seat; Id = id; }
        public abstract Card Top();
        public abstract Card RemoveTop();        // <-- NEW abstract
        public abstract bool Add(Card c);
        public abstract bool Remove(Card c);
        public abstract bool Contains(Card c);
    }

    /// <summary>List-backed containers: Deck, Extra, Hand, Graveyard, Banished.</summary>
    public abstract class ListZoneBase : ZoneBase
    {
        protected readonly List<Card> _cards;
        public int Capacity { get; private set; }

        protected ListZoneBase(Seat seat, ZoneId id, int capacity = 60) : base(seat, id)
        {
            Capacity = Math.Max(1, capacity);
            _cards = new List<Card>(Capacity);
        }

        public override int Count => _cards.Count;
        public override Card Top() => _cards.Count > 0 ? _cards[_cards.Count - 1] : null;
        public override bool Contains(Card c) => c != null && _cards.Contains(c);

        public override bool Add(Card c)
        {
            if (c == null) return false;
            _cards.Add(c);
            return true;
        }

        public override bool Remove(Card c)
        {
            if (c == null) return false;
            return _cards.Remove(c);
        }

        // Uniform API: removing top
        public override Card RemoveTop() => PopTop(); // <-- NEW

        // Helpers for deck/extra
        public void ReplaceAll(IEnumerable<Card> items)
        {
            _cards.Clear();
            if (items == null) return;
            foreach (var c in items) _cards.Add(c);
        }

        public Card PopTop()
        {
            if (_cards.Count == 0) return null;
            var last = _cards.Count - 1;
            var c = _cards[last];
            _cards.RemoveAt(last);
            return c;
        }

        public bool AddBottom(Card c)
        {
            if (c == null) return false;
            _cards.Add(c);
            return true;
        }

        public IList<Card> RawList => _cards;
    }

    // ---------------- Concrete list zones ----------------

    public sealed class DeckZone      : ListZoneBase { public override CardZone ZoneType => CardZone.Deck;      public DeckZone(Seat seat, ZoneId id, int capacity = 60) : base(seat, id, capacity) { } }
    public sealed class ExtraDeckZone : ListZoneBase { public override CardZone ZoneType => CardZone.ExtraDeck; public ExtraDeckZone(Seat seat, ZoneId id, int capacity = 15) : base(seat, id, capacity) { } }
    public sealed class HandZone      : ListZoneBase { public override CardZone ZoneType => CardZone.Hand;      public HandZone(Seat seat, ZoneId id, int capacity = 20) : base(seat, id, capacity) { } }
    public sealed class GraveyardZone : ListZoneBase { public override CardZone ZoneType => CardZone.Graveyard; public GraveyardZone(Seat seat, ZoneId id) : base(seat, id, 64) { } }
    public sealed class BanishedZone  : ListZoneBase
    {
        public override CardZone ZoneType => CardZone.Banished;
        public BanishedZone(Seat seat, ZoneId id) : base(seat, id, 64) { }
        public void Add(Card c, bool faceDown) => Add(c); // face state can be stored on Card if you track it
    }

    // ---------------- Single-slot zones ----------------

    public abstract class SingleSlotZoneBase : ZoneBase
    {
        protected Card _slot;
        protected SingleSlotZoneBase(Seat seat, ZoneId id) : base(seat, id) { }

        public override int Count => _slot != null ? 1 : 0;
        public override Card Top() => _slot;

        public override Card RemoveTop()        // <-- NEW
        {
            if (_slot == null) return null;
            var c = _slot;
            _slot = null;
            return c;
        }
        
        // ✅ add these two helpers
        public bool IsEmpty => _slot == null;
        
        public override bool Contains(Card c) => c != null && _slot == c;

        public override bool Add(Card c)
        {
            if (c == null || _slot != null) return false;
            _slot = c;
            return true;
        }

        public override bool Remove(Card c)
        {
            if (_slot == null || c == null) return false;
            if (!ReferenceEquals(_slot, c)) return false;
            _slot = null;
            return true;
        }
    }

    public sealed class MonsterZone   : SingleSlotZoneBase { public override CardZone ZoneType => CardZone.Monster;   public MonsterZone(Seat seat, ZoneId id) : base(seat, id) { } public Card Card { get => _slot; set => _slot = value; } }
    public sealed class SpellTrapZone : SingleSlotZoneBase { public override CardZone ZoneType => CardZone.SpellTrap; public SpellTrapZone(Seat seat, ZoneId id) : base(seat, id) { } public Card Card { get => _slot; set => _slot = value; } }
    public sealed class PendulumZone  : SingleSlotZoneBase { public override CardZone ZoneType => CardZone.Pendulum;  public PendulumZone(Seat seat, ZoneId id) : base(seat, id) { } public Card Card { get => _slot; set => _slot = value; } }
    public sealed class FieldZone     : SingleSlotZoneBase { public override CardZone ZoneType => CardZone.Field;     public FieldZone(Seat seat, ZoneId id) : base(seat, id) { } public Card Card { get => _slot; set => _slot = value; } }
}
