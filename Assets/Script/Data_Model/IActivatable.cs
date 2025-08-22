// Effects.Contracts.cs
// Decoupled interfaces used by effect authors & the chain/activation pipeline.

using System.Collections.Generic;
using YGO.Duel.Board;
using YGO.Duel.Chain;   // for ConditionContext, CostContext, ResolveContext, ICost, IResolverAction, ITargetRef
using YGO.Duel.Rules;

namespace YGO.Duel.Effects
{
    /// <summary>
    /// A source that can expose one or more activatable effects (e.g., a card with multiple activations).
    /// </summary>
    public interface IActivatable
    {
        /// <summary>Return handles the UI can show as activation options.</summary>
        IEnumerable<IEffectHandle> GetActivationHandles(BoardManager.Seat controller);
    }

    /// <summary>Composable condition predicate.</summary>
    public interface ICondition
    {
        /// <summary>True if the condition is met; otherwise sets a reason.</summary>
        bool Evaluate(ConditionContext ctx, out string reason);
    }

    /// <summary>Composable target-selection policy.</summary>
    public interface ITargetSelector
    {
        /// <summary>Number of targets required (min..max). Use (1,1) for single-target, (0,0) for no target.</summary>
        (int min, int max) RequiredCount { get; }

        /// <summary>Produce legal target candidates given current board/timing.</summary>
        IEnumerable<ITargetRef> EnumerateCandidates(ConditionContext ctx);

        /// <summary>Validate a concrete selection made by the player/UI.</summary>
        bool IsSelectionValid(ConditionContext ctx, IReadOnlyList<ITargetRef> chosen, out string reason);
    }

    /// <summary>
    /// Marker for effects that can be negated. YGO distinguishes negating the activation vs negating the effect.
    /// </summary>
    public interface ICanBeNegated
    {
        /// <summary>Whether the activation itself can be negated (e.g., Counter Traps).</summary>
        bool IsActivationNegatable { get; }

        /// <summary>Whether the effect can be negated after activation resolves (continuous/lingering)</summary>
        bool IsEffectNegatable { get; }
    }

    /// <summary>
    /// Supplies timing context (spell speed and optional custom tagging) to the RuleSet.
    /// Usually implemented by an EffectHandle (you already expose Speed there).
    /// </summary>
    public interface ITimingProvider
    {
        RuleSet.SpellSpeed SpellSpeed { get; }
        /// <summary>Optional: finer-grained tag for timing windows (e.g., "OnAttackDeclared").</summary>
        RuleSet.Timing TimingHint { get; }
    }

    // NOTE:
    // - ICost is defined in Chain/CostSystem.cs (do not redefine).
    // - IResolverAction is defined in Chain/ChainLink.cs (do not redefine).
}
