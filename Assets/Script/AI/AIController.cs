// AIController.cs
// Simple rule-based AI: try Normal Summon (no tribute) → End Phase if nothing to do.
// Extend with your battle/chain logic or swap in MCTS later.

using System;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Runtime.Actions;

namespace YGO.Duel.Runtime
{
    public sealed class AIController
    {
        private readonly BoardManager _board;
        private readonly TurnManager _turns;
        private readonly RuleSet _rules;
        private readonly DuelLogger _logger;
        private readonly ActionQueue _queue;

        public BoardManager.Seat Seat { get; private set; }

        public AIController(BoardManager board, TurnManager turns, RuleSet rules, DuelLogger logger, ActionQueue queue, BoardManager.Seat seat)
        {
            _board  = board  ?? throw new ArgumentNullException(nameof(board));
            _turns  = turns  ?? throw new ArgumentNullException(nameof(turns));
            _rules  = rules  ?? throw new ArgumentNullException(nameof(rules));
            _logger = logger ?? new DuelLogger();
            _queue  = queue  ?? throw new ArgumentNullException(nameof(queue));
            Seat    = seat;
        }

        /// <summary>
        /// Call this when it’s the AI’s turn and you want it to act once (e.g., on Main1).
        /// Returns true if an action was enqueued.
        /// </summary>
        public bool Think()
        {
            // 1) Try a no-tribute Normal Summon if legal.
            if (_turns.CurrentPlayer == Seat &&
                (_turns.CurrentPhase == RuleSet.Phase.Main1 || _turns.CurrentPhase == RuleSet.Phase.Main2))
            {
                var hand = _board.Zones[(int)Seat].Hand;
                for (int i = 0; i < hand.Count; i++)
                {
                    var c = hand.Top(); // hand is list-backed; you could index into raw if you expose it
                    c = (i < hand.Count ? ((YGO.Duel.Zones.ListZoneBase)hand).RawList[i] : c); // safe raw access if available

                    if (c == null) continue;
                    int level = Math.Max(1, c.Level);
                    int reqTributes = _rules.GetRequiredTributes(level);
                    if (reqTributes > 0) continue; // keep it simple: only summon no-tribute monsters

                    // Ask rules
                    var adapters = new ActionPolicyValidator.PlayerRuleAdapters(_board, _turns, Seat); // reuse tiny adapters
                    if (_rules.CanNormalSummon(adapters.Player, adapters.State, adapters.Board, level))
                    {
                        int mzIndex = FirstFreeMonsterZone(Seat);
                        if (mzIndex >= 0)
                        {
                            var a = GameAction.NormalSummon(Seat, _turns.TurnNumber, _turns.CurrentPhase, c.Name, mzIndex);
                            if (_queue.Enqueue(a, out _))
                            {
                                _logger.LogText("AI.NormalSummon", $"AI normal summons {c.Name} at MZ[{mzIndex}]", source: nameof(AIController));
                                return true;
                            }
                        }
                    }
                }
            }

            // 2) Otherwise, if it's our turn and chain is empty, end phase to move the game along.
            if (_turns.CurrentPlayer == Seat)
            {
                var a = GameAction.EndPhase(Seat, _turns.TurnNumber, _turns.CurrentPhase);
                if (_queue.Enqueue(a, out _))
                {
                    _logger.LogText("AI.EndPhase", "AI ends phase", source: nameof(AIController));
                    return true;
                }
            }

            // 3) Default: pass priority (useful in response windows)
            var pass = GameAction.PassPriority(Seat, _turns.TurnNumber, _turns.CurrentPhase);
            if (_queue.Enqueue(pass, out _))
            {
                _logger.LogText("AI.PassPriority", "AI passes priority", source: nameof(AIController));
                return true;
            }

            return false;
        }

        private int FirstFreeMonsterZone(BoardManager.Seat seat)
        {
            var mz = _board.Zones[(int)seat].Monsters;
            for (int i = 0; i < mz.Length; i++)
                if (mz[i].Top() == null || mz[i].Card == null) return i;
            return -1;
        }
    }
}
