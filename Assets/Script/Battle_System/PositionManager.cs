
// PositionManager.cs
// Handles battle position/face changes, per-turn restrictions, and attack flags (Unity/IL2CPP safe).

using System;
using System.Collections.Generic;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Runtime;
using Card = YGO.Duel.Cards.Card;

namespace YGO.Duel.Battle
{
    public sealed class PositionManager
    {
        private readonly BoardManager _board;
        private readonly DuelLogger _logger;
        

        private readonly Dictionary<Card, CardPosState> _state = new(256);
        private struct CardPosState
        {
            public BattlePosition Position;
            public bool FaceUp;
            public bool CanAttackThisTurn;
            public bool HasAttackedThisTurn;

            // NEW: turn-scoped origin flags
            public bool WasSummonedThisTurn;
            public bool WasSetThisTurn;

            // NEW: once-per-turn position change
            public bool ChangedPosThisTurn;
        }

// Track per-card fast lookups (optional convenience, we also mirror in struct)
        private readonly HashSet<Card> _changedThisTurn  = new();
        private readonly HashSet<Card> _attackedThisTurn = new();


        // Policy toggles
        public bool oncePerTurn = true;                // position can be changed only once per turn
        public bool disallowChangeAfterAttack = true;  // classic YGO: cannot change position after attacking

        public PositionManager(BoardManager board, DuelLogger logger)
        {
            _board  = board  ?? throw new ArgumentNullException(nameof(board));
            _logger = logger ?? new DuelLogger();
        }

        // ---------------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------------

        /// <summary>
        /// Request a battle position (and face) change for a monster currently on the field.
        /// Returns false if illegal at the moment; sets an error reason.
        /// </summary>
        public bool RequestPositionChange(Card card, BattlePosition to, bool faceUp, out string error)
        {
            error = "";
            if (card == null) { error = "Null card"; return false; }
            if (!TryFindMonsterZone(card, out var ownerSeat, out var mzIndex))
            { error = "Card is not in a Monster Zone"; return false; }

            var s = GetOrCreateState(card);

            // Prevent second change in same turn (once-per-turn) and after attacking (classic policy)
            if (oncePerTurn && (s.ChangedPosThisTurn || _changedThisTurn.Contains(card)))
            { error = "This monster already changed its battle position this turn"; return false; }

            if (disallowChangeAfterAttack && (s.HasAttackedThisTurn || _attackedThisTurn.Contains(card)))
            { error = "This monster has already attacked this turn"; return false; }

            // Writeback
            var fromPos = s.Position; var fromFace = s.FaceUp;
            s.Position = to;
            s.FaceUp = faceUp;
            s.ChangedPosThisTurn = true;
            _state[card] = s;
            _changedThisTurn.Add(card);

            // Mirror to runtime card
            card.SetPosition(
                to == BattlePosition.Attack ? Cards.CardBattlePosition.Attack : Cards.CardBattlePosition.Defense,
                faceUp: faceUp
            );

            _logger.LogText("Position.Change",
                $"{card?.Name}: {fromPos}/{(fromFace?"FU":"FD")} -> {to}/{(faceUp?"FU":"FD")}",
                source: nameof(PositionManager));

            return true;
        }
        
        

        /// <summary>Mark that the given monster has declared (at least one) attack this turn.</summary>
        public void MarkAttackUsed(Card card)
        {
            if (card == null) return;
            var s = GetOrCreateState(card);
            s.HasAttackedThisTurn = true;
            _state[card] = s;
            _attackedThisTurn.Add(card);
        }

        /// <summary>Clear the “has attacked” flag for the given monster (e.g., at turn start).</summary>
        public void ClearAttackUsed(Card card)
        {
            if (card == null) return;
            if (_state.TryGetValue(card, out var s))
            {
                s.HasAttackedThisTurn = false;
                _state[card] = s;
            }
            _attackedThisTurn.Remove(card);
        }

        /// <summary>Per-turn reset: allows position changes and attacks again.</summary>
        public void ResetPerTurnFlags()
        {
            _changedThisTurn.Clear();
            _attackedThisTurn.Clear();

            // Reset per-turn booleans for all cards we’re tracking.
            // Typical YGO: at start of your new turn, monsters can attack again (unless fresh-summon rules apply elsewhere).
            var keys = _state.Keys; // snapshot-safe enumeration in .NET is fine; but we can copy to list if needed
            var tmp = new List<Card>(keys);
            foreach (var c in tmp)
            {
                var s = _state[c];
                s.HasAttackedThisTurn = false;
                s.CanAttackThisTurn   = true; // default allow; apply “summoning sickness” elsewhere if you have that
                _state[c] = s;
            }
        }

        /// <summary>Read-only current state (falls back to ATK/FU if unknown).</summary>
        public (BattlePosition position, bool faceUp) GetState(Card card)
        {
            if (card != null && _state.TryGetValue(card, out var s))
                return (s.Position, s.FaceUp);
            return (BattlePosition.Attack, true);
        }

        // ---- Helpers used by CardBattlerAdapter ----

        public void SetCanAttackThisTurn(Card c, bool v)
        {
            if (c == null) return;
            var s = GetOrCreateState(c);
            s.CanAttackThisTurn = v;
            _state[c] = s;
        }

        public bool CanAttackThisTurn(Card c)
        {
            if (c == null) return false;
            if (_state.TryGetValue(c, out var s)) return s.CanAttackThisTurn;
            return true; // default “can”
        }

        public bool HasAttackedThisTurn(Card c)
        {
            if (c == null) return false;
            if (_state.TryGetValue(c, out var s)) return s.HasAttackedThisTurn;
            return false;
        }

        // ---------------------------------------------------------------------
        // Internals
        // ---------------------------------------------------------------------

        private CardPosState GetOrCreateState(Card card)
        {
            if (!_state.TryGetValue(card, out var s))
            {
                s = new CardPosState
                {
                    Position            = BattlePosition.Attack,
                    FaceUp              = true,
                    CanAttackThisTurn   = true,
                    HasAttackedThisTurn = false
                };
                _state[card] = s;
            }
            return s; // struct: caller must write back after mutation
        }

        private bool TryFindMonsterZone(Card card, out BoardManager.Seat seat, out int index)
        {
            foreach (BoardManager.Seat s in new[] { BoardManager.Seat.P1, BoardManager.Seat.P2 })
            {
                var mz = _board.Zones[(int)s].Monsters;
                for (int i = 0; i < mz.Length; i++)
                {
                    // New API: Top()
                    var topMethod = mz[i].GetType().GetMethod("Top");
                    if (topMethod != null && ReferenceEquals(topMethod.Invoke(mz[i], null), card))
                    { seat = s; index = i; return true; }

                    // Legacy: public field Card
                    var fld = mz[i].GetType().GetField("Card");
                    if (fld != null && ReferenceEquals(fld.GetValue(mz[i]), card))
                    { seat = s; index = i; return true; }
                }
            }
            seat = default; index = -1; return false;
        }
        
        public void MarkSummonedThisTurn(Card c)
        {
            if (c == null) return;
            var s = GetOrCreateState(c);
            s.WasSummonedThisTurn = true;
            _state[c] = s;
        }

        public void MarkSetThisTurn(Card c)
        {
            if (c == null) return;
            var s = GetOrCreateState(c);
            s.WasSetThisTurn = true;
            s.FaceUp = false; // ensure state mirrors runtime
            s.Position = BattlePosition.Defense; // typical set state
            _state[c] = s;
        }
        
        public bool CanChangePositionNow(Card card, RuleSet rules, TurnManager turns, out string why)
        {
            why = "";
            if (card == null) { why = "Null card"; return false; }
            if (!TryFindMonsterZone(card, out _, out _)) { why = "Not in a Monster Zone"; return false; }

            // Timing (Main1/Main2, chain empty, your turn)
            var player = new RuleAdapters.RulePlayerAdapter(card.Controller, turns, _board);
            var state  = new RuleAdapters.DuelStateAdapter(turns);
            if (!rules.IsMainPhaseOpen(state, player)) { why = "Not in an open Main Phase"; return false; }

            // Cannot change on the same turn it was Summoned/Set (classic YGO)
            var s = GetOrCreateState(card);
            if (s.WasSummonedThisTurn || s.WasSetThisTurn) { why = "Cannot change battle position this turn"; return false; }

            if (oncePerTurn && (s.ChangedPosThisTurn || _changedThisTurn.Contains(card)))
            { why = "Already changed position this turn"; return false; }

            if (disallowChangeAfterAttack && (s.HasAttackedThisTurn || _attackedThisTurn.Contains(card)))
            { why = "Already attacked this turn"; return false; }

            return true;
        }

        public bool CanFlipSummonNow(Card card, RuleSet rules, TurnManager turns, out string why)
        {
            why = "";
            if (card == null) { why = "Null card"; return false; }
            if (!TryFindMonsterZone(card, out _, out _)) { why = "Not in a Monster Zone"; return false; }

            // Must be face-down monster in DEF (typical), set in a previous turn
            var s = GetOrCreateState(card);
            if (s.FaceUp) { why = "Card is already face-up"; return false; }
            // if you want to strictly require DEF, uncomment:
            // if (s.Position != BattlePosition.Defense) { why = "Only face-down DEF can Flip Summon"; return false; }
            if (s.WasSetThisTurn) { why = "Cannot Flip Summon the turn it was Set"; return false; }

            // Timing
            var player = new RuleAdapters.RulePlayerAdapter(card.Controller, turns, _board);
            var state  = new RuleAdapters.DuelStateAdapter(turns);
            if (!rules.IsMainPhaseOpen(state, player)) { why = "Not in an open Main Phase"; return false; }

            // Treat Flip Summon as a position change for the once-per-turn constraint
            if (oncePerTurn && (s.ChangedPosThisTurn || _changedThisTurn.Contains(card)))
            { why = "Already changed position this turn"; return false; }

            if (disallowChangeAfterAttack && (s.HasAttackedThisTurn || _attackedThisTurn.Contains(card)))
            { why = "Already attacked this turn"; return false; }

            return true;
        }
        
        public void ResetPerTurnFlagsFor(BoardManager.Seat seat)
        {
            // global per-turn sets
            _changedThisTurn.Clear();
            _attackedThisTurn.Clear();

            // per-card flags
            var tmp = new List<Card>(_state.Keys);
            foreach (var c in tmp)
            {
                if (c.Controller != seat) continue;
                var s = _state[c];
                s.HasAttackedThisTurn = false;
                s.CanAttackThisTurn   = true;
                s.WasSummonedThisTurn = false;
                s.WasSetThisTurn      = false;
                s.ChangedPosThisTurn  = false;
                _state[c] = s;
            }
        }



        
    }
}