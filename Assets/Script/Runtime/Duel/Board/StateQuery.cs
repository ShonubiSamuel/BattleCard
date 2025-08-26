// StateQuery.cs
// High-level questions over the current board (or a snapshot).
// Examples: ControlsFaceUpDragon, HasFreeMonsterZone, CountMonsters, OpponentHasBackrow, etc.

using System;
using System.Collections.Generic;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Rules;
using YGO.Duel.Foundation;
using YGO.Duel.Runtime;
using Card = YGO.Duel.Cards.Card;

namespace YGO.Duel.Query
{
    /// <summary>Optional card database contract (backed by your CardDefinition ScriptableObjects).</summary>
    public interface ICardDatabase
    {
        // Return null if unknown
        object GetDefinition(Card card);
        // Lightweight helpers (return false if unknown)
        bool TryGetRace(Card card, out string race);           // e.g., "Dragon", "Warrior"
        bool TryGetAttribute(Card card, out string attribute); // e.g., "LIGHT", "DARK"
        bool HasArchetype(Card card, string archetypeIdOrName);
        bool IsMonster(Card card);
    }

    /// <summary>Optional position reader; typically your PositionManager.</summary>
    public interface IPositionReader
    {
        bool TryGet(Card card, out bool faceUp, out string posText);
        bool IsFaceUp(Card card);
    }

    public static class StateQuery
    {
        // ---------- Convenience: opponent ----------
        public static BoardManager.Seat OpponentOf(BoardManager.Seat s) => s == BoardManager.Seat.P1 ? BoardManager.Seat.P2 : BoardManager.Seat.P1;

        // ---------- Counts / availability ----------
        public static bool HasFreeMonsterZone(BoardManager board, BoardManager.Seat seat)
            => board.Zones[(int)seat].HasFreeMonsterZone();

        public static int CountMonsters(BoardManager board, BoardManager.Seat seat)
        {
            var arr = board.Zones[(int)seat].Monsters;
            int n = 0;
            for (int i = 0; i < arr.Length; i++)
                if (Top(arr[i]) != null) n++;
            return n;
        }

        public static int CountSetSpellsTraps(BoardManager board, BoardManager.Seat seat, IPositionReader pos = null)
        {
            var st = board.Zones[(int)seat].SpellsTraps;
            int n = 0;
            for (int i = 0; i < st.Length; i++)
            {
                var c = Top(st[i]);
                if (c == null) continue;
                bool faceUp = true;
                if (pos != null) faceUp = pos.IsFaceUp(c);
                // set (face-down)
                if (!faceUp) n++;
            }
            return n;
        }

        public static bool OpponentHasBackrow(BoardManager board, BoardManager.Seat you, IPositionReader pos = null)
            => CountSetSpellsTraps(board, OpponentOf(you), pos) > 0;

        public static int CountHand(BoardManager board, BoardManager.Seat seat)
            => CountList(board.Zones[(int)seat].Hand);

        // ---------- Race/Attribute/Archetype checks ----------

        public static bool ControlsFaceUpDragon(BoardManager board, BoardManager.Seat seat, ICardDatabase db = null, IPositionReader pos = null)
            => ControlsFaceUpRace(board, seat, "Dragon", db, pos);

        public static bool ControlsFaceUpRace(BoardManager board, BoardManager.Seat seat, string race, ICardDatabase db = null, IPositionReader pos = null)
        {
            var arr = board.Zones[(int)seat].Monsters;
            for (int i = 0; i < arr.Length; i++)
            {
                var c = Top(arr[i]);
                if (c == null) continue;

                // Face-up?
                if (pos != null && !pos.IsFaceUp(c)) continue;

                if (db != null)
                {
                    if (db.TryGetRace(c, out var r) && string.Equals(r, race, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        public static bool ControlsFaceUpAttribute(BoardManager board, BoardManager.Seat seat, string attribute, ICardDatabase db = null, IPositionReader pos = null)
        {
            var arr = board.Zones[(int)seat].Monsters;
            for (int i = 0; i < arr.Length; i++)
            {
                var c = Top(arr[i]);
                if (c == null) continue;
                if (pos != null && !pos.IsFaceUp(c)) continue;

                if (db != null)
                {
                    if (db.TryGetAttribute(c, out var a) && string.Equals(a, attribute, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        public static bool ControlsArchetype(BoardManager board, BoardManager.Seat seat, string archetype, ICardDatabase db = null)
        {
            if (db == null) return false;
            var arr = board.Zones[(int)seat].Monsters;
            for (int i = 0; i < arr.Length; i++)
            {
                var c = Top(arr[i]);
                if (c == null) continue;
                if (db.HasArchetype(c, archetype)) return true;
            }
            return false;
        }

        // ---------- Tribute requirements helper ----------

        public static int CountTributableMonsters(BoardManager board, BoardManager.Seat seat)
            => board.CountTributableMonsters(seat);

        // ---------- Snapshot-friendly helpers ----------

        public static bool CanEnterBattlePhase(RuleSet rules, DuelState s)
        {
            if (rules == null || s == null) return false;
            // Mirror RuleSet.CanEnterBattlePhase logic (phase gate + first turn restriction)
            if (s.Phase != RuleSet.Phase.Main1) return false;
            if (s.TurnNumber == 1 && !rules.firstTurnCanEnterBattlePhase) return false;
            return true;
        }

        // ---------- Internals / reflection helpers ----------

        private static Card Top(object zoneLike)
        {
            if (zoneLike == null) return null;
            var f = zoneLike.GetType().GetField("Card");
            if (f != null) return f.GetValue(zoneLike) as Card;

            var m = zoneLike.GetType().GetMethod("Top", Type.EmptyTypes);
            if (m != null) return m.Invoke(zoneLike, null) as Card;

            return null;
        }

        private static int CountList(object zoneLike)
        {
            if (zoneLike == null) return 0;
            var propCount = zoneLike.GetType().GetProperty("Count");
            if (propCount != null && propCount.PropertyType == typeof(int))
                return (int)propCount.GetValue(zoneLike);

            var fldCards = zoneLike.GetType().GetField("Cards");
            if (fldCards != null && typeof(System.Collections.ICollection).IsAssignableFrom(fldCards.FieldType))
            {
                var coll = fldCards.GetValue(zoneLike) as System.Collections.ICollection;
                return coll?.Count ?? 0;
            }
            return 0;
        }
    }
}
