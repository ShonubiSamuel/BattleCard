// DuelState.cs
// Authoritative snapshot of the duel at a point in time: players, zones, turn/phase, chain.
// Capture via DuelState.Capture(...) or DuelState.CaptureFromServices().

using System;
using System.Collections.Generic;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Chain;
using Card = YGO.Duel.Cards.Card;

namespace YGO.Duel.Runtime
{
    /// <summary>Minimal chain reader to decouple from your concrete ChainManager.</summary>
    public interface IChainReader
    {
        bool IsChainEmpty { get; }
        int Count { get; }
        ChainLink PeekTop(); // may return null if empty
        IReadOnlyList<ChainLink> Snapshot(); // optional; can return a copy
    }

    [Serializable]
    public sealed class DuelState
    {
        // -------- High level --------
        public int TurnNumber;
        public RuleSet.Phase Phase;
        public BoardManager.Seat CurrentPlayer;
        public bool ChainEmpty;
        public int ChainCount;
        public string ChainTopSummary; // e.g., "Blue-Eyes effect"

        // -------- Players --------
        public PlayerSnap P1;
        public PlayerSnap P2;

        [Serializable]
        public sealed class PlayerSnap
        {
            public string Name;
            public int LifePoints;

            public int DeckCount;
            public int ExtraCount;
            public int HandCount;
            public int GraveCount;
            public int BanishedCount;

            public int MonsterOccupied;
            public int MonsterCapacity;
            public int STOccupied;
            public int STCapacity;

            public bool HasField;
            public bool HasLeftPendulum;
            public bool HasRightPendulum;

            public override string ToString()
                => $"{Name ?? "Player"} LP={LifePoints} Hand={HandCount} Deck={DeckCount} GY={GraveCount} Banished={BanishedCount} " +
                   $"MZ {MonsterOccupied}/{MonsterCapacity} ST {STOccupied}/{STCapacity}";
        }

        public override string ToString()
            => $"T{TurnNumber}:{Phase} P{(CurrentPlayer == BoardManager.Seat.P1 ? "1" : "2")} " +
               $"Chain={ChainCount}{(ChainCount > 0 ? $" (Top={ChainTopSummary})" : "")}\n{P1}\n{P2}";

        // -------- Capture helpers --------

        public static DuelState CaptureFromServices()
        {
            ServiceLocator.TryGet(out BoardManager board);
            ServiceLocator.TryGet(out TurnManager turns);
            // Chain is optional
            IChainReader chain = null;
            if (ServiceLocator.TryGet<IChainReader>(out var cr)) chain = cr;
            else if (ServiceLocator.TryGet<ChainManager>(out var cm)) chain = new ChainManagerReaderAdapter(cm);

            return Capture(board, turns, chain);
        }

        public static DuelState Capture(BoardManager board, TurnManager turns, IChainReader chain = null)
        {
            if (board == null || turns == null)
                throw new ArgumentNullException("Capture requires BoardManager and TurnManager.");

            var ds = new DuelState
            {
                TurnNumber    = turns.TurnNumber,
                Phase         = turns.CurrentPhase,
                CurrentPlayer = turns.CurrentPlayer,
                ChainEmpty    = chain?.IsChainEmpty ?? true,
                ChainCount    = chain?.Count ?? 0,
                ChainTopSummary = SummarizeTop(chain)
            };

            ds.P1 = CapturePlayer(board, BoardManager.Seat.P1);
            ds.P2 = CapturePlayer(board, BoardManager.Seat.P2);
            return ds;
        }

        private static PlayerSnap CapturePlayer(BoardManager board, BoardManager.Seat seat)
        {
            var z  = board.Zones[(int)seat];
            var ps = board.Players[(int)seat];
            var p  = new PlayerSnap();

            p.Name       = ps?.DisplayName ?? (seat == BoardManager.Seat.P1 ? "Player 1" : "Player 2");
            p.LifePoints = ps?.LifePoints ?? 0;

            // Deck/Extra/Hand/GY/Banished counts — support both earlier and newer BoardManager shapes
            p.DeckCount     = CountListZone(z.MainDeck);
            p.ExtraCount    = CountListZone(z.ExtraDeck);
            p.HandCount     = CountListZone(z.Hand);
            p.GraveCount    = CountListZone(z.Graveyard);
            p.BanishedCount = CountListZone(z.Banished);

            // Monster/ST occupancy + capacities
            p.MonsterCapacity = z.Monsters?.Length ?? 0;
            p.STCapacity      = z.SpellsTraps?.Length ?? 0;

            p.MonsterOccupied = CountOccupied(z.Monsters);
            p.STOccupied      = CountOccupied(z.SpellsTraps);

            p.HasField        = z.Field != null && GetTop(z.Field) != null;
            p.HasLeftPendulum = z.Pendulum != null && z.Pendulum.Length > 0 && GetTop(z.Pendulum[0]) != null;
            p.HasRightPendulum= z.Pendulum != null && z.Pendulum.Length > 1 && GetTop(z.Pendulum[1]) != null;

            return p;
        }

        private static int CountListZone(object zone)
        {
            if (zone == null) return 0;
            // Try .Count
            var propCount = zone.GetType().GetProperty("Count");
            if (propCount != null && propCount.PropertyType == typeof(int))
                return (int)propCount.GetValue(zone);
            // Try .Cards (List<T>)
            var fldCards = zone.GetType().GetField("Cards");
            if (fldCards != null && typeof(System.Collections.ICollection).IsAssignableFrom(fldCards.FieldType))
            {
                var coll = fldCards.GetValue(zone) as System.Collections.ICollection;
                return coll?.Count ?? 0;
            }
            return 0;
        }

        private static int CountOccupied(Array slots)
        {
            if (slots == null) return 0;
            int occ = 0;
            foreach (var s in slots)
                if (GetTop(s) != null) occ++;
            return occ;
        }

        private static Card GetTop(object zoneLike)
        {
            if (zoneLike == null) return null;
            // Try field "Card"
            var f = zoneLike.GetType().GetField("Card");
            if (f != null) return f.GetValue(zoneLike) as Card;
            // Try method Top()
            var m = zoneLike.GetType().GetMethod("Top", Type.EmptyTypes);
            if (m != null) return m.Invoke(zoneLike, null) as Card;
            return null;
        }

        private static string SummarizeTop(IChainReader chain)
        {
            if (chain == null || chain.IsChainEmpty) return null;
            var top = chain.PeekTop();
            if (top == null) return null;
            var eff = top.Effect?.EffectName ?? "Effect";
            var who = top.Activator.ToString();
            return $"{eff} by {who}";
        }

        // ------- Adapter for your existing ChainManager (optional) -------

        private sealed class ChainManagerReaderAdapter : IChainReader
        {
            private readonly ChainManager _cm;
            public ChainManagerReaderAdapter(ChainManager cm) { _cm = cm; }
            public bool IsChainEmpty => _cm == null || _cm.Count == 0;
            public int Count => _cm?.Count ?? 0;
            public ChainLink PeekTop() => _cm?.PeekTop();
            public IReadOnlyList<ChainLink> Snapshot() => _cm?.Snapshot();
        }
    }
}
