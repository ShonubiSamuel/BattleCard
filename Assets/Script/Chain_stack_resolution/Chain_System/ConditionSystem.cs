// ConditionSystem.cs
// Centralized legality checks: timing (RuleSet), once-per-turn, and effect-specific board conditions.

using System;
using YGO.Duel.Board;
using YGO.Duel.Rules;

namespace YGO.Duel.Chain
{
    public sealed class ConditionSystem
    {
        /// <summary>
        /// Delegates to the effect for extra board/state predicates beyond RuleSet timing.
        /// </summary>
        public bool CheckAdditional(IEffectHandle effect, ConditionContext ctx, out string reason)
        {
            reason = "";
            if (effect == null) { reason = "No effect."; return false; }
            return effect.CheckAdditionalConditions(ctx, out reason);
        }

        /// <summary>
        /// Enforce "once per turn" if the effect advertises IOncePerTurn.
        /// </summary>
        public bool CheckOncePerTurn(IEffectHandle effect, out string reason)
        {
            reason = "";
            if (effect is IOncePerTurn opt && opt.ConsumedThisTurn)
            {
                reason = "This effect has already been used this turn.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Mark the "once per turn" flag after a link is successfully added.
        /// </summary>
        public void MarkOncePerTurn(IEffectHandle effect)
        {
            if (effect is IOncePerTurn opt) opt.ConsumedThisTurn = true;
        }

        // ----------------- Helper: quick timing oracle (optional external use) -----------------

        /// <summary>
        /// One-shot timing check using RuleSet (spell speed + phase + chain window).
        /// </summary>
        public static bool CheckTiming(RuleSet rules,
                                       RuleSet.SpellSpeed speed,
                                       RuleSet.IRuleDuelState state,
                                       RuleSet.Timing timing,
                                       bool isControllerTurn)
        {
            return rules.CanActivateEffect(speed, state, timing, isControllerTurn);
        }
    }
}
