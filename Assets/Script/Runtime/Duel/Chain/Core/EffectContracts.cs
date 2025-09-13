using System.Collections.Generic;
using YGO.Duel.Rules;

namespace YGO.Duel.Chain
{
    // Action that executes when a chain link resolves
    public interface IResolverAction { void Resolve(ResolveContext ctx); }

    // Effect “blueprint”: validated at activation time, produces a resolver for resolution.
    public interface IEffectHandle
    {
        string EffectName { get; }
        RuleSet.SpellSpeed Speed { get; }

        // Extra effect-specific gates (beyond RuleSet.CanActivateEffect).
        bool CheckAdditionalConditions(ConditionContext ctx, out string reason);

        // Costs to pay now (before link is created)
        IEnumerable<ICost> GetCosts(CostContext ctx);

        // Build the action that will run at resolution
        IResolverAction BuildResolveAction(ResolveContext ctx);
    }

    // If an effect has an OPT clause, expose this so the chain manager can mark it.
    public interface IOncePerTurn { bool ConsumedThisTurn { get; set; } }

 
}