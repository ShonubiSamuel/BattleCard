// SummonValidator.cs
// Rule-aware checks for Normal/Set/Special Summons (no mutation).

using System;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Rules;

namespace YGO.Duel.Systems
{
    public sealed class SummonValidator
    {
        private readonly BoardManager _board;
        private readonly RuleSet _rules;
        private readonly Runtime.IChainState _chain; // optional, lets RuleSet see "IsChainEmpty"

        public SummonValidator(BoardManager board, RuleSet rules, Runtime.IChainState chainState = null)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _chain = chainState;
        }

        /// <summary>True if a Normal Summon from hand into MZ[index] is legal right now (no mutation).</summary>
        public bool CanNormalSummon(BoardManager.Seat seat, Card handCard, int mzIndex, Runtime.TurnManager turns, out string reason)
        {
            reason = "";
            if (handCard == null) { reason = "No card"; return false; }
            if (!_board.IsBuilt)  { reason = "Board not built"; return false; }
            if (turns == null)    { reason = "No TurnManager"; return false; }
            if (mzIndex < 0 || mzIndex >= _board.Zones[(int)seat].Monsters.Length)
            { reason = "Invalid zone index"; return false; }

            // Must be a monster in the acting player's hand
            var zones = _board.Zones[(int)seat];
            if (!handCard.IsMonsterRuntime) { reason = "Not a monster"; return false; }
            if (!zones.Hand.Contains(handCard)) { reason = "Card not in hand"; return false; }

            // Slot must be empty
            var slot = _board.Zones[(int)seat].Monsters[mzIndex];
            if (!slot.IsEmpty && slot.Top() != null) { reason = "Monster Zone occupied"; return false; }

            // RuleSet timing/phase/etc.
            var adapters = new YGO.Duel.Runtime.ActionPolicyValidator.PlayerRuleAdapters(_board, turns, seat);
            int req = _rules.GetRequiredTributes(handCard.Level);
            if (req > 0 && _board.CountTributableMonsters(seat) < req)
            { reason = "Not enough tributes"; return false; }

            if (!_rules.CanNormalSummon(adapters.Player, adapters.State, adapters.Board, handCard.Level))
            { reason = "RuleSet timing or once-per-turn"; return false; }

            return true;
        }
    }
}
