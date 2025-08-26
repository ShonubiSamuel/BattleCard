// BoardManager.cs
// Two-player YGO-style board: players, zones, deck load/shuffle/draw, queries, and zone lookup.

using System;
using System.Collections.Generic;
using UnityEngine;
using YGO.Duel.Foundation;     // GameConfig, DeterministicRng
using YGO.Duel.Zones;          // IZone + concrete zones
using Card = YGO.Duel.Cards.Card;

namespace YGO.Duel.Board
{
    public sealed class BoardManager
    {
        // ---------------- Seats & helpers ----------------
        public enum Seat : int { P1 = 0, P2 = 1 }
        public static Seat OpponentOf(Seat s) => s == Seat.P1 ? Seat.P2 : Seat.P1;

        // ---------------- Zone identity model ----------------
        public enum CardZone
        {
            Unknown = 0,
            Deck,
            ExtraDeck,
            Hand,
            Monster,
            SpellTrap,
            Graveyard,
            Banished,
            Pendulum,
            Field
        }

        /// <summary>Identifies a specific zone: (Seat, Kind, Index). Index is used for slotted zones (Monster/ST/Pendulum) and ignored for others.</summary>
        public readonly struct ZoneId : IEquatable<ZoneId>
        {
            public readonly Seat Seat;
            public readonly CardZone Kind;
            public readonly int Index; // 0-based index for Monster/SpellTrap/Pendulum; ignored otherwise

            public ZoneId(Seat seat, CardZone kind, int index = 0) { Seat = seat; Kind = kind; Index = index; }

            public override string ToString() => Kind switch
            {
                CardZone.Monster or CardZone.SpellTrap or CardZone.Pendulum => $"{Seat}.{Kind}[{Index}]",
                _ => $"{Seat}.{Kind}"
            };

            public bool Equals(ZoneId other) => Seat == other.Seat && Kind == other.Kind && Index == other.Index;
            public override bool Equals(object obj) => obj is ZoneId z && Equals(z);
            public override int GetHashCode() => (Seat, Kind, Index).GetHashCode();
        }

        // ---------------- Player state ----------------
        public sealed class PlayerState
        {
            public string DisplayName;
            public int LifePoints;
            public bool NormalSummonUsedThisTurn;

            public PlayerState(string displayName, int lp)
            {
                DisplayName = displayName;
                LifePoints = lp;
                NormalSummonUsedThisTurn = false;
            }
        }

        // ---------------- Public board layout ----------------
        public readonly struct Layout
        {
            public readonly int MaxMonsterZones;
            public readonly int MaxSpellTrapZones;
            public readonly bool EnablePendulumZones;
            public readonly bool UseFieldZone;

            public Layout(int maxMZ, int maxST, bool pendulum, bool field)
            {
                MaxMonsterZones = Mathf.Max(1, maxMZ);
                MaxSpellTrapZones = Mathf.Max(1, maxST);
                EnablePendulumZones = pendulum;
                UseFieldZone = field;
            }
        }

        // ---------------- Player zones aggregate ----------------
        public sealed class PlayerZones
        {
            public DeckZone      MainDeck     { get; internal set; }
            public ExtraDeckZone ExtraDeck    { get; internal set; }
            public HandZone      Hand         { get; internal set; }
            public GraveyardZone Graveyard    { get; internal set; }
            public BanishedZone  Banished     { get; internal set; }

            public MonsterZone[]   Monsters    { get; internal set; }
            public SpellTrapZone[] SpellsTraps { get; internal set; }
            public PendulumZone[]  Pendulum    { get; internal set; } // length 2 if enabled
            public FieldZone       Field       { get; internal set; } // may be null if disabled

            public bool HasFreeMonsterZone()
            {
                foreach (var mz in Monsters)
                    if (mz.Top() == null) return true;
                return false;
            }

            public int CountTributableMonsters()
            {
                int count = 0;
                foreach (var mz in Monsters)
                {
                    var c = mz.Top();
                    if (c != null && c.IsTributable) count++;
                }
                return count;
            }

            public bool ControlsAnyMonsters()
            {
                foreach (var mz in Monsters)
                    if (mz.Top() != null) return true;
                return false;
            }
        }

        // ---------------- State ----------------
        public Layout       BoardLayout { get; private set; }
        public PlayerState[] Players    { get; private set; } = new PlayerState[2];
        public PlayerZones[] Zones      { get; private set; } = new PlayerZones[2];

        // quick lookup map for FindZone
        private readonly Dictionary<ZoneId, IZone> _zoneIndex = new Dictionary<ZoneId, IZone>(64);

        public bool IsBuilt { get; private set; }

        // ---------------- Build / load ----------------

        public void BuildEmptyBoard(GameConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            BuildEmptyBoard(cfg.BuildRuntime());
        }

        public void BuildEmptyBoard(GameConfig.Runtime rt)
        {
            // layout
            BoardLayout = new Layout(rt.MaxMonsterZones, rt.MaxSpellTrapZones, rt.EnablePendulumZones, rt.UseFieldZone);

            // allocate player zones
            Zones[(int)Seat.P1] = CreateZonesFor(Seat.P1, BoardLayout);
            Zones[(int)Seat.P2] = CreateZonesFor(Seat.P2, BoardLayout);

            // clear players until loaded
            Players[(int)Seat.P1] = null;
            Players[(int)Seat.P2] = null;

            // index all zones for fast FindZone
            _zoneIndex.Clear();
            IndexPlayerZones(Seat.P1, Zones[(int)Seat.P1]);
            IndexPlayerZones(Seat.P2, Zones[(int)Seat.P2]);

            IsBuilt = true;
        }

        private PlayerZones CreateZonesFor(Seat seat, Layout layout)
        {
            var pz = new PlayerZones();

            // ids
            var deckId  = new ZoneId(seat, CardZone.Deck);
            var handId  = new ZoneId(seat, CardZone.Hand);
            var gyId    = new ZoneId(seat, CardZone.Graveyard);
            var banId   = new ZoneId(seat, CardZone.Banished);
            var xtraId  = new ZoneId(seat, CardZone.ExtraDeck);

            pz.MainDeck   = new DeckZone(seat, deckId, capacity: 60);
            pz.Hand       = new HandZone(seat, handId, capacity: 20);
            pz.Graveyard  = new GraveyardZone(seat, gyId);
            pz.Banished   = new BanishedZone(seat, banId);
            pz.ExtraDeck  = new ExtraDeckZone(seat, xtraId, capacity: 15);

            pz.Monsters = new MonsterZone[layout.MaxMonsterZones];
            for (int i = 0; i < pz.Monsters.Length; i++)
                pz.Monsters[i] = new MonsterZone(seat, new ZoneId(seat, CardZone.Monster, i));

            pz.SpellsTraps = new SpellTrapZone[layout.MaxSpellTrapZones];
            for (int i = 0; i < pz.SpellsTraps.Length; i++)
                pz.SpellsTraps[i] = new SpellTrapZone(seat, new ZoneId(seat, CardZone.SpellTrap, i));

            if (layout.EnablePendulumZones)
                pz.Pendulum = new PendulumZone[2]
                {
                    new PendulumZone(seat, new ZoneId(seat, CardZone.Pendulum, 0)),
                    new PendulumZone(seat, new ZoneId(seat, CardZone.Pendulum, 1))
                };

            if (layout.UseFieldZone)
                pz.Field = new FieldZone(seat, new ZoneId(seat, CardZone.Field));

            return pz;
        }

        private void IndexPlayerZones(Seat seat, PlayerZones pz)
        {
            // list zones
            _zoneIndex[new ZoneId(seat, CardZone.Deck)]      = pz.MainDeck;
            _zoneIndex[new ZoneId(seat, CardZone.Hand)]      = pz.Hand;
            _zoneIndex[new ZoneId(seat, CardZone.Graveyard)] = pz.Graveyard;
            _zoneIndex[new ZoneId(seat, CardZone.Banished)]  = pz.Banished;
            _zoneIndex[new ZoneId(seat, CardZone.ExtraDeck)] = pz.ExtraDeck;

            // slotted
            for (int i = 0; i < pz.Monsters.Length; i++)
                _zoneIndex[new ZoneId(seat, CardZone.Monster, i)] = pz.Monsters[i];

            for (int i = 0; i < pz.SpellsTraps.Length; i++)
                _zoneIndex[new ZoneId(seat, CardZone.SpellTrap, i)] = pz.SpellsTraps[i];

            if (pz.Pendulum != null)
            {
                _zoneIndex[new ZoneId(seat, CardZone.Pendulum, 0)] = pz.Pendulum[0];
                _zoneIndex[new ZoneId(seat, CardZone.Pendulum, 1)] = pz.Pendulum[1];
            }

            if (pz.Field != null)
                _zoneIndex[new ZoneId(seat, CardZone.Field)] = pz.Field;
        }

        public void LoadPlayersAndDecks(GameConfig cfg, IDeckSource deckSource = null)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            LoadPlayersAndDecks(cfg.BuildRuntime(), deckSource);
        }

        public void LoadPlayersAndDecks(GameConfig.Runtime rt, IDeckSource deckSource = null)
        {
            if (!IsBuilt) throw new InvalidOperationException("Board not built. Call BuildEmptyBoard first.");

            if (deckSource == null && ServiceLocator.Contains<IDeckSource>())
                deckSource = ServiceLocator.Get<IDeckSource>();

            var p1Deck  = deckSource?.GetMainDeck(Seat.P1)  ?? new List<Card>();
            var p2Deck  = deckSource?.GetMainDeck(Seat.P2)  ?? new List<Card>();
            var p1Extra = deckSource?.GetExtraDeck(Seat.P1) ?? new List<Card>();
            var p2Extra = deckSource?.GetExtraDeck(Seat.P2) ?? new List<Card>();

            var p1Name  = deckSource?.GetPlayerName(Seat.P1) ?? "Player 1";
            var p2Name  = deckSource?.GetPlayerName(Seat.P2) ?? "Player 2";

            Players[(int)Seat.P1] = new PlayerState(p1Name, rt.StartingLifePoints);
            Players[(int)Seat.P2] = new PlayerState(p2Name, rt.StartingLifePoints);

            Zones[(int)Seat.P1].MainDeck.ReplaceAll(p1Deck);
            Zones[(int)Seat.P2].MainDeck.ReplaceAll(p2Deck);
            Zones[(int)Seat.P1].ExtraDeck.ReplaceAll(p1Extra);
            Zones[(int)Seat.P2].ExtraDeck.ReplaceAll(p2Extra);

            // Update Card.CurrentZone for list containers
            foreach (var c in p1Deck) c.CurrentZone = CardZone.Deck;
            foreach (var c in p2Deck) c.CurrentZone = CardZone.Deck;
            foreach (var c in p1Extra) c.CurrentZone = CardZone.ExtraDeck;
            foreach (var c in p2Extra) c.CurrentZone = CardZone.ExtraDeck;
        }

        public void ShuffleBothDecks(DeterministicRng rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            EnsurePlayersReady();

            rng.Shuffle(((ListZoneBase)Zones[(int)Seat.P1].MainDeck).RawList);
            rng.Shuffle(((ListZoneBase)Zones[(int)Seat.P2].MainDeck).RawList);
        }

        public void DrawOpeningHands(int startingHandSize)
        {
            EnsurePlayersReady();
            for (int i = 0; i < startingHandSize; i++)
            {
                DrawOne(Seat.P1);
                DrawOne(Seat.P2);
            }
        }

        public Card DrawOne(Seat seat)
        {
            var z = Zones[(int)seat];
            var top = z.MainDeck.PopTop();
            if (top != null)
            {
                z.Hand.Add(top);
                top.CurrentZone = CardZone.Hand;
            }
            return top;

            Debug.Log("dddd  " + top);
        }

        // ---------------- Required helper methods ----------------

        /// <summary>Return the opponent's seat for a given player seat.</summary>
        public Seat GetOpponent(Seat seat) => OpponentOf(seat);

        /// <summary>Return the opponent's PlayerState for a given PlayerState reference.</summary>
        public PlayerState GetOpponent(PlayerState player)
        {
            if (player == null) return null;
            if (ReferenceEquals(player, Players[(int)Seat.P1])) return Players[(int)Seat.P2];
            if (ReferenceEquals(player, Players[(int)Seat.P2])) return Players[(int)Seat.P1];
            return null;
        }

        /// <summary>Enumerate all cards across both players and all zones.</summary>
        public IEnumerable<Card> AllCards()
        {
            foreach (Seat s in new[] { Seat.P1, Seat.P2 })
            {
                var z = Zones[(int)s];
                // list zones
                foreach (var c in ((ListZoneBase)z.MainDeck).RawList) yield return c;
                foreach (var c in ((ListZoneBase)z.ExtraDeck).RawList) yield return c;
                foreach (var c in ((ListZoneBase)z.Hand).RawList) yield return c;
                foreach (var c in ((ListZoneBase)z.Graveyard).RawList) yield return c;
                foreach (var c in ((ListZoneBase)z.Banished).RawList) yield return c;
                // slotted
                foreach (var mz in z.Monsters) { var c = mz.Top(); if (c != null) yield return c; }
                foreach (var st in z.SpellsTraps) { var c = st.Top(); if (c != null) yield return c; }
                if (z.Pendulum != null)
                {
                    var c0 = z.Pendulum[0].Top(); if (c0 != null) yield return c0;
                    var c1 = z.Pendulum[1].Top(); if (c1 != null) yield return c1;
                }
                if (z.Field != null)
                {
                    var cf = z.Field.Top(); if (cf != null) yield return cf;
                }
            }
        }

        /// <summary>Resolve a ZoneId to a concrete zone container.</summary>
        public IZone FindZone(ZoneId id)
        {
            if (_zoneIndex.TryGetValue(id, out var z)) return z;
            return null;
        }

        // Existing helpers (kept for battle/rules code compatibility)
        public bool HasFreeMonsterZone(Seat seat) => Zones[(int)seat].HasFreeMonsterZone();
        public int CountTributableMonsters(Seat seat) => Zones[(int)seat].CountTributableMonsters();
        public bool OpponentControlsAnyMonsters(Seat seat) => Zones[(int)OpponentOf(seat)].ControlsAnyMonsters();

        // Safety
        private void EnsurePlayersReady()
        {
            if (!IsBuilt) throw new InvalidOperationException("Board not built.");
            if (Players[(int)Seat.P1] == null || Players[(int)Seat.P2] == null)
                throw new InvalidOperationException("Players/decks not loaded.");
        }

        // ---------------- Deck source contract ----------------
        public interface IDeckSource
        {
            string GetPlayerName(Seat seat);
            List<Card> GetMainDeck(Seat seat);
            List<Card> GetExtraDeck(Seat seat);
        }
    }
}
