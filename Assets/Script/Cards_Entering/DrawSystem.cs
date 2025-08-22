// DrawSystem.cs
// Turn draw + effect-driven draws with optional replacement hooks.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Runtime;

namespace YGO.Duel.Systems
{
    public enum DrawReason { TurnStart, Effect, Cost, Replacement, Rule }

    /// <summary>Context passed to draw replacement hooks.</summary>
    public readonly struct DrawContext
    {
        public readonly BoardManager Board;
        public readonly BoardManager.Seat Seat;
        public readonly int Count;
        public readonly DrawReason Reason;
        public readonly RuleSet RuleSet;
        public readonly TurnManager Turns;
        public DrawContext(BoardManager b, BoardManager.Seat s, int c, DrawReason r, RuleSet rs, TurnManager tm)
        { Board = b; Seat = s; Count = c; Reason = r; RuleSet = rs; Turns = tm; }
    }

    /// <summary>Optional: register these to replace a draw with something else (mill, add specific card, etc.).</summary>
    public interface IDrawReplacement
    {
        /// <summary>Return true if you handled the draw (partially or fully). Put drawn cards into 'result'.</summary>
        bool TryReplace(DrawContext ctx, out List<Card> result, out string info);
    }

    public sealed class DrawSystem
    {
        private readonly BoardManager _board;
        private readonly DuelLogger   _logger;
        private readonly RuleSet      _rules;
        private readonly TurnManager  _turns;
        private readonly DeterministicRng _rng;
        private readonly bool _autoHookTurnStart;
        private bool _hooked;

        private readonly List<IDrawReplacement> _replacements = new(8);

        public event Action<BoardManager.Seat, IReadOnlyList<Card>, DrawReason> OnDrew;

        public DrawSystem(BoardManager board,
            DuelLogger logger,
            RuleSet rules,
            TurnManager turns,
            DeterministicRng rng = null,
            bool autoHookTurnStart = false)   // <-- default: NO auto hook
        {
            _board  = board  ?? throw new ArgumentNullException(nameof(board));
            _logger = logger ?? new DuelLogger();
            _rules  = rules  ?? ScriptableObject.CreateInstance<RuleSet>();
            _turns  = turns; // optional
            _rng    = rng;

            _autoHookTurnStart = autoHookTurnStart;
            if (_turns != null && _autoHookTurnStart)
            {
                _turns.OnTurnStarted += HandleTurnStarted;
                _hooked = true;
            }
        }
        
        // If you ever want to toggle it at runtime:
        public void SetAutoTurnStartDraw(bool enable)
        {
            if (_turns == null || enable == _hooked) return;
            if (enable) { _turns.OnTurnStarted += HandleTurnStarted; _hooked = true; }
            else        { _turns.OnTurnStarted -= HandleTurnStarted; _hooked = false; }
        }

      

        public void RegisterReplacement(IDrawReplacement repl)
        {
            if (repl != null && !_replacements.Contains(repl)) _replacements.Add(repl);
        }

        public void UnregisterReplacement(IDrawReplacement repl)
        {
            if (repl != null) _replacements.Remove(repl);
        }

        // --- Turn start draw step (called via event) ---
        // --- Turn start draw (only if autoHookTurnStart=true) ---
        private void HandleTurnStarted(BoardManager.Seat seat, int turnNumber)
        {
            // If you prefer to still *skip* drawing on turn 1 by rule, you can keep this gate,
            // but since we're disabling auto, this path is not used unless you enable it.
            if (turnNumber == 1 && !_rules.ShouldFirstTurnDraw())
            {
                _logger.LogText("Draw.Skip", "First-turn draw skipped by rules",
                    data: $"seat=P{(seat==BoardManager.Seat.P1?1:2)}; turn={turnNumber}", source: nameof(DrawSystem));
                return;
            }

            Draw(seat, 1, DrawReason.TurnStart, out _);
        }

        /// <summary>Draw n cards (with replacement hooks). Returns false only if *no* card could be drawn/produced.</summary>
        public bool Draw(BoardManager.Seat seat, int count, DrawReason reason, out List<Card> drawn)
        {
            drawn = new List<Card>(Mathf.Max(0, count));
            if (count <= 0) return true;

            var ctx = new DrawContext(_board, seat, count, reason, _rules, _turns);

            // 1) Try replacements
            int remaining = count;
            foreach (var repl in _replacements)
            {
                if (remaining <= 0) break;

                if (repl.TryReplace(ctx, out var produced, out var info) && produced != null && produced.Count > 0)
                {
                    drawn.AddRange(produced);
                    remaining = Mathf.Max(0, remaining - produced.Count);
                    _logger.LogText("Draw.Replace", $"Replacement produced {produced.Count}",
                        data: $"seat=P{(seat==BoardManager.Seat.P1?1:2)}; info={info}", source: repl.GetType().Name);
                }
            }

            // 2) Default draw from deck for whatever remains
            var zones = _board.Zones[(int)seat];
            for (int i = 0; i < remaining; i++)
            {
                var c = zones.MainDeck.PopTop();
                if (c == null)
                {
                    _logger.LogText("Draw.EmptyDeck", "Cannot draw — deck empty",
                        data: $"seat=P{(seat==BoardManager.Seat.P1?1:2)}", source: nameof(DrawSystem));
                    break;
                }

                zones.Hand.Add(c);
                c.CurrentZone = BoardManager.CardZone.Hand;
                c.SetController(seat);          // ← use method, don’t assign directly
                c.ZoneIndex   = -1;
                drawn.Add(c);
            }

            if (drawn.Count > 0)
            {
                _logger.LogText("Draw.Result", $"Drew {drawn.Count}",
                    data: $"seat=P{(seat==BoardManager.Seat.P1?1:2)}; reason={reason}", source: nameof(DrawSystem));
                OnDrew?.Invoke(seat, drawn, reason);

                // EventBus (correct namespace + helper)
                if (ServiceLocator.TryGet<EventBus>(out var bus) && bus != null)
                    bus.RaiseCardsDrawn(seat, drawn, reason.ToString());

                return true;
            }

            return false;
        }
    }
}
