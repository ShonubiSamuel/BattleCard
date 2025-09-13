using System.Collections.Generic;
using YGO.Duel.Rules;

namespace YGO.Duel.Chain
{
    public sealed class NoopEffectHandle : IEffectHandle
    {
        public string EffectName { get; }
        public RuleSet.SpellSpeed Speed { get; }

        public NoopEffectHandle(string name, RuleSet.SpellSpeed speed)
        { EffectName = name ?? "Effect"; Speed = speed; }

        public bool CheckAdditionalConditions(ConditionContext ctx, out string reason)
        { reason = ""; return true; }

        public IEnumerable<ICost> GetCosts(CostContext ctx)
        { yield break; }

        public IResolverAction BuildResolveAction(ResolveContext ctx)
        { return null; } // resolves to nothing
    }
}