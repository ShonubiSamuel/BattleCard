// TargetingService.cs
// UI-agnostic targeting with stable target refs + validation.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Foundation;

namespace YGO.Duel.Targeting
{
    // -------- Target model --------

    [Flags]
    public enum TargetLocation
    {
        None      = 0,
        Field     = 1 << 0, // shorthand: any field zones (Monster/SpellTrap/Pendulum/Field)
        Monster   = 1 << 1,
        SpellTrap = 1 << 2,
        Pendulum  = 1 << 3,
        FieldZone = 1 << 4,
        Hand      = 1 << 5,
        Graveyard = 1 << 6,
        Banished  = 1 << 7,
        Deck      = 1 << 8,
        ExtraDeck = 1 << 9,
        All       = ~0
    }

    public enum TargetSide { Self, Opponent, Both }
    public enum FaceConstraint { Any, FaceUp, FaceDown }
    public enum PositionConstraint { Any, Attack, Defense }
    public enum CardTypeConstraint { Any, MonsterOnly, SpellTrapOnly }

    /// <summary>Defines "what" and "how many" you want to select.</summary>
    [Serializable]
    public sealed class TargetSpec
    {
        [Range(0, 5)] public int min = 1;
        [Range(0, 5)] public int max = 1;
        public bool requireDistinct = true;

        public TargetSide side = TargetSide.Both;
        public TargetLocation locations = TargetLocation.Field;

        public CardTypeConstraint cardType = CardTypeConstraint.Any;
        public FaceConstraint face = FaceConstraint.Any;
        public PositionConstraint position = PositionConstraint.Any;

        public bool allowPlayers = false; // e.g., "target a player"
        public bool playersMustBeOpponent = true;

        /// <summary>Additional predicate (e.g., "Dragon", "Level 4 or lower").</summary>
        public Func<Card, bool> extraFilter;

        /// <summary>Human-readable label to show in the UI.</summary>
        public string prompt = "Select targets";

        /// <summary>Convenience: one card exactly.</summary>
        public static TargetSpec OneOnField(CardTypeConstraint type = CardTypeConstraint.Any)
            => new TargetSpec { min = 1, max = 1, locations = TargetLocation.Field, cardType = type };
    }

    // -------- Stable target refs (replay/net safe) --------

    public interface ITargetRef
    {
        /// <summary>Seat perspective (for player targets) or controller/owner fallback for cards.</summary>
        BoardManager.Seat Seat { get; }
        /// <summary>Try to resolve to a runtime Card if this is a card ref; returns false for player targets.</summary>
        bool TryResolveCard(BoardManager board, out Card card);
        /// <summary>Short description for logs/UI.</summary>
        string Describe();
        /// <summary>True if this ref points to a player (not a card).</summary>
        bool IsPlayer { get; }
    }

    [Serializable]
    public sealed class CardTargetRef : ITargetRef, IEquatable<CardTargetRef>
    {
        // Prefer runtime instance id (Card.InstanceId). Fallback to name for bring-up only.
        public string instanceIdOrName;
        public BoardManager.Seat seatHint; // helps resolve if multiple matches (hand, etc.)

        public CardTargetRef() { }
        public CardTargetRef(string id, BoardManager.Seat seat)
        { instanceIdOrName = id; seatHint = seat; }

        public BoardManager.Seat Seat => seatHint;
        public bool IsPlayer => false;

        public bool TryResolveCard(BoardManager board, out Card card)
        {
            card = null;
            if (board == null || string.IsNullOrEmpty(instanceIdOrName)) return false;

            // If you registered an ICardIndex, use it
            if (ServiceLocator.TryGet<ICardIndex>(out var index) && index != null)
            {
                card = index.Find(instanceIdOrName);
                if (card != null) return true;
            }

            // Fallback: search all cards by InstanceId, then by Name (slow but OK for dev)
            foreach (var c in board.AllCards())
            {
                if (c == null) continue;
                if (string.Equals(c.InstanceId, instanceIdOrName, StringComparison.Ordinal))
                { card = c; return true; }
            }
            foreach (var c in board.AllCards())
            {
                if (c == null) continue;
                if (string.Equals(c.Name, instanceIdOrName, StringComparison.Ordinal))
                { card = c; return true; }
            }
            return false;
        }

        public string Describe() => $"Card[{instanceIdOrName}]";
        public override string ToString() => Describe();

        public bool Equals(CardTargetRef other)
            => other != null && string.Equals(instanceIdOrName, other.instanceIdOrName, StringComparison.Ordinal);
        public override bool Equals(object obj) => Equals(obj as CardTargetRef);
        public override int GetHashCode() => (instanceIdOrName ?? "").GetHashCode();
    }

    [Serializable]
    public sealed class PlayerTargetRef : ITargetRef, IEquatable<PlayerTargetRef>
    {
        public BoardManager.Seat seat;
        public PlayerTargetRef() { }
        public PlayerTargetRef(BoardManager.Seat s) { seat = s; }

        public BoardManager.Seat Seat => seat;
        public bool IsPlayer => true;
        public bool TryResolveCard(BoardManager board, out Card card) { card = null; return false; }
        public string Describe() => $"Player[{(seat == BoardManager.Seat.P1 ? "P1" : "P2")}]";
        public override string ToString() => Describe();

        public bool Equals(PlayerTargetRef other) => other != null && other.seat == seat;
        public override bool Equals(object obj) => Equals(obj as PlayerTargetRef);
        public override int GetHashCode() => (int)seat;
    }

    // -------- Picker bridge (UI / AI) --------

    public sealed class TargetingRequest
    {
        public TargetSpec Spec;
        public BoardManager.Seat Requester;
        public IReadOnlyList<TargetCandidate> Candidates;
        public string Prompt => Spec?.prompt ?? "Select targets";

        public override string ToString()
            => $"{Prompt} (min={Spec?.min}, max={Spec?.max}, side={Spec?.side}, loc={Spec?.locations})";
    }

    public readonly struct TargetCandidate
    {
        public readonly Card Card;
        public readonly BoardManager.ZoneId Zone;
        public TargetCandidate(Card c, BoardManager.ZoneId z) { Card = c; Zone = z; }
        public override string ToString() => $"{Card?.Name} @ {Zone}";
    }

    public interface ITargetPicker
    {
        /// <summary>Return chosen targets (as refs) from the provided candidates. Must not mutate game state.</summary>
        IReadOnlyList<ITargetRef> PickTargets(TargetingRequest request, out string error);
    }

    /// <summary>Fallback picker that auto-selects the first N legal candidates (useful for bots/tests).</summary>
    public sealed class FirstNPicker : ITargetPicker
    {
        public IReadOnlyList<ITargetRef> PickTargets(TargetingRequest request, out string error)
        {
            error = "";
            if (request == null || request.Spec == null) return Array.Empty<ITargetRef>();
            var take = Mathf.Min(Mathf.Max(0, request.Spec.min), request.Candidates.Count);
            return request.Candidates.Take(take)
                .Select(c => (ITargetRef)new CardTargetRef(c.Card.InstanceId, c.Card.Controller))
                .ToList();
        }
    }

    // -------- Targeting service --------

    public sealed class TargetingService
    {
        private readonly BoardManager _board;
        private readonly DuelLogger _logger;
        private ITargetPicker _picker;

        public TargetingService(BoardManager board, DuelLogger logger, ITargetPicker picker = null)
        {
            _board  = board  ?? throw new ArgumentNullException(nameof(board));
            _logger = logger ?? new DuelLogger();
            _picker = picker ?? new FirstNPicker();
        }

        public void SetPicker(ITargetPicker picker) { if (picker != null) _picker = picker; }

        /// <summary>
        /// Build candidates, ask picker (UI/AI) to select, validate, return refs.
        /// </summary>
        public bool RequestTargets(TargetSpec spec, BoardManager.Seat requester, out List<ITargetRef> result, out string error)
        {
            result = new List<ITargetRef>();
            error = "";

            if (spec == null) { error = "Null TargetSpec"; return false; }

            // 1) Build candidates
            var candidates = EnumerateCandidates(spec, requester);

            // Early out if nothing is required
            if (spec.min == 0 && candidates.Count == 0 && !spec.allowPlayers) return true;

            // 2) Picker (UI/AI)
            var req = new TargetingRequest { Spec = spec, Requester = requester, Candidates = candidates };
            var chosen = _picker?.PickTargets(req, out error) ?? Array.Empty<ITargetRef>();
            if (!string.IsNullOrEmpty(error)) return false;

            // 3) Validate chosen
            if (!Validate(spec, requester, chosen, out error)) return false;

            result.AddRange(chosen);
            _logger.LogText("Targeting.Chosen",
                $"{chosen.Count} target(s): {string.Join(", ", chosen.Select(t => t.Describe()))}",
                data: $"req={req}", source: nameof(TargetingService));
            return true;
        }

        /// <summary>Validate already-chosen targets against a spec and current board state.</summary>
        public bool Validate(TargetSpec spec, BoardManager.Seat requester, IReadOnlyList<ITargetRef> targets, out string reason)
        {
            reason = "";

            if (spec == null) { reason = "Null TargetSpec"; return false; }
            int count = targets?.Count ?? 0;
            if (count < spec.min) { reason = $"Need at least {spec.min} target(s)"; return false; }
            if (count > spec.max) { reason = $"At most {spec.max} target(s)"; return false; }

            if (spec.requireDistinct)
            {
                var dedup = new HashSet<string>();
                foreach (var t in targets)
                {
                    var key = TargetKey(t);
                    if (!dedup.Add(key)) { reason = "Duplicate targets not allowed"; return false; }
                }
            }

            foreach (var t in targets)
            {
                if (t == null) { reason = "Null target"; return false; }

                if (t.IsPlayer)
                {
                    if (!spec.allowPlayers) { reason = "Players cannot be targeted by this effect"; return false; }
                    if (spec.playersMustBeOpponent && t.Seat == requester) { reason = "Must target opponent player"; return false; }
                    continue;
                }

                // Card target
                if (!t.TryResolveCard(_board, out var card) || card == null)
                { reason = "Target no longer exists"; return false; }

                if (!IsCardLegalForSpec(card, requester, spec))
                { reason = $"Target {card.Name} no longer legal"; return false; }
            }
            return true;
        }

        // -------- Candidate enumeration & per-card checks --------

        private List<TargetCandidate> EnumerateCandidates(TargetSpec spec, BoardManager.Seat requester)
        {
            var list = new List<TargetCandidate>(64);

            bool wantField = (spec.locations & TargetLocation.Field) != 0;
            bool wantMZ    = (spec.locations & TargetLocation.Monster) != 0 || wantField;
            bool wantST    = (spec.locations & TargetLocation.SpellTrap) != 0 || wantField;
            bool wantPZ    = (spec.locations & TargetLocation.Pendulum) != 0 || wantField;
            bool wantFZ    = (spec.locations & TargetLocation.FieldZone) != 0 || wantField;

            void addIfLegal(Card c, BoardManager.ZoneId z)
            {
                if (c == null) return;
                if (IsCardLegalForSpec(c, requester, spec))
                    list.Add(new TargetCandidate(c, z));
            }

            foreach (var side in SeatsForSide(requester, spec.side))
            {
                var z = _board.Zones[(int)side];

                // Monsters
                if (wantMZ)
                {
                    for (int i = 0; i < z.Monsters.Length; i++)
                    {
                        var c = z.Monsters[i].Top();
                        if (c != null) addIfLegal(c, new BoardManager.ZoneId(side, BoardManager.CardZone.Monster, i));
                    }
                }

                // Spells/Traps
                if (wantST)
                {
                    for (int i = 0; i < z.SpellsTraps.Length; i++)
                    {
                        var c = z.SpellsTraps[i].Top();
                        if (c != null) addIfLegal(c, new BoardManager.ZoneId(side, BoardManager.CardZone.SpellTrap, i));
                    }
                }

                // Pendulum
                if (wantPZ && z.Pendulum != null)
                {
                    for (int i = 0; i < z.Pendulum.Length; i++)
                    {
                        var c = z.Pendulum[i].Top();
                        if (c != null) addIfLegal(c, new BoardManager.ZoneId(side, BoardManager.CardZone.Pendulum, i));
                    }
                }

                // Field zone
                if (wantFZ && z.Field != null)
                {
                    var c = z.Field.Top();
                    if (c != null) addIfLegal(c, new BoardManager.ZoneId(side, BoardManager.CardZone.Field));
                }

                // Hand
                if ((spec.locations & TargetLocation.Hand) != 0)
                {
                    foreach (var c in z.Hand.RawList) addIfLegal(c, new BoardManager.ZoneId(side, BoardManager.CardZone.Hand));
                }

                // Graveyard
                if ((spec.locations & TargetLocation.Graveyard) != 0)
                {
                    foreach (var c in z.Graveyard.RawList) addIfLegal(c, new BoardManager.ZoneId(side, BoardManager.CardZone.Graveyard));
                }

                // Banished
                if ((spec.locations & TargetLocation.Banished) != 0)
                {
                    foreach (var c in z.Banished.RawList) addIfLegal(c, new BoardManager.ZoneId(side, BoardManager.CardZone.Banished));
                }

                // Deck / Extra — usually not targetable, but spec can ask for them.
                if ((spec.locations & TargetLocation.Deck) != 0)
                {
                    foreach (var c in z.MainDeck.RawList) addIfLegal(c, new BoardManager.ZoneId(side, BoardManager.CardZone.Deck));
                }
                if ((spec.locations & TargetLocation.ExtraDeck) != 0)
                {
                    foreach (var c in z.ExtraDeck.RawList) addIfLegal(c, new BoardManager.ZoneId(side, BoardManager.CardZone.ExtraDeck));
                }
            }

            return list;
        }

        private static IEnumerable<BoardManager.Seat> SeatsForSide(BoardManager.Seat requester, TargetSide side)
        {
            switch (side)
            {
                case TargetSide.Self:     yield return requester; break;
                case TargetSide.Opponent: yield return BoardManager.OpponentOf(requester); break;
                default:
                    yield return requester; yield return BoardManager.OpponentOf(requester); break;
            }
        }

        private static string TargetKey(ITargetRef t)
        {
            if (t is PlayerTargetRef pr) return $"P:{(int)pr.seat}";
            if (t is CardTargetRef cr)   return $"C:{cr.instanceIdOrName}";
            return t.Describe();
        }

        private static bool IsCardLegalForSpec(Card c, BoardManager.Seat requester, TargetSpec spec)
        {
            // Side
            if (spec.side != TargetSide.Both)
            {
                bool isOpp = c.Controller == BoardManager.OpponentOf(requester);
                bool wantOpp = spec.side == TargetSide.Opponent;
                if (isOpp != wantOpp) return false;
            }

            // Location
            if (!LocationMatches(c, spec.locations)) return false;

            // Type
            if (spec.cardType == CardTypeConstraint.MonsterOnly && !c.Def.IsMonster) return false;
            if (spec.cardType == CardTypeConstraint.SpellTrapOnly && c.Def.IsMonster) return false;

            // Face
            if (spec.face == FaceConstraint.FaceUp   && !c.IsFaceUp) return false;
            if (spec.face == FaceConstraint.FaceDown &&  c.IsFaceUp) return false;

            // Position
            if (spec.position != PositionConstraint.Any && c.IsOnField)
            {
                var isAtk = (c.Position == CardBattlePosition.Attack);
                if (spec.position == PositionConstraint.Attack  && !isAtk) return false;
                if (spec.position == PositionConstraint.Defense &&  isAtk) return false;
            }

            // Extra predicate
            if (spec.extraFilter != null && !spec.extraFilter(c)) return false;

            return true;
        }

        private static bool LocationMatches(Card c, TargetLocation locs)
        {
            var z = c.CurrentZone;
            bool has(TargetLocation f) => (locs & f) != 0;

            switch (z)
            {
                case BoardManager.CardZone.Monster:   return has(TargetLocation.Monster)   || has(TargetLocation.Field);
                case BoardManager.CardZone.SpellTrap: return has(TargetLocation.SpellTrap) || has(TargetLocation.Field);
                case BoardManager.CardZone.Pendulum:  return has(TargetLocation.Pendulum)  || has(TargetLocation.Field);
                case BoardManager.CardZone.Field:     return has(TargetLocation.FieldZone) || has(TargetLocation.Field);
                case BoardManager.CardZone.Hand:      return has(TargetLocation.Hand);
                case BoardManager.CardZone.Graveyard: return has(TargetLocation.Graveyard);
                case BoardManager.CardZone.Banished:  return has(TargetLocation.Banished);
                case BoardManager.CardZone.Deck:      return has(TargetLocation.Deck);
                case BoardManager.CardZone.ExtraDeck: return has(TargetLocation.ExtraDeck);
                default: return false;
            }
        }
    }
}
