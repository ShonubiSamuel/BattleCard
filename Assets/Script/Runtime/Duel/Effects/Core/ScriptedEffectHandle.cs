// Scripts/Runtime/Duel/Effects/Core/ScriptedEffectHandle.cs
using System;
using System.Collections.Generic;
using YGO.Duel.Chain;
using YGO.Duel.Rules;

namespace YGO.Duel.Effects
{
    /// Composable IEffectHandle built from three lambdas.
    public sealed class ScriptedEffectHandle : IEffectHandle
    {
        public string EffectName { get; }
        public RuleSet.SpellSpeed Speed { get; }

        private readonly Func<ConditionContext, (bool ok, string why)> _cond;
        private readonly Func<CostContext, IEnumerable<ICost>> _costs;
        private readonly Func<ResolveContext, IResolverAction> _resolver;

        public ScriptedEffectHandle(
            string name,
            RuleSet.SpellSpeed speed,
            Func<ConditionContext, (bool, string)> condition,
            Func<CostContext, IEnumerable<ICost>> costs,
            Func<ResolveContext, IResolverAction> resolver)
        {
            EffectName = string.IsNullOrWhiteSpace(name) ? "Effect" : name;
            Speed = speed;
            _cond = condition ?? (_ => (true, ""));
            _costs = costs ?? (_ => Array.Empty<ICost>());
            _resolver = resolver ?? (_ => null);
        }

        public bool CheckAdditionalConditions(ConditionContext ctx, out string reason)
        {
            var (ok, why) = _cond(ctx);
            reason = why ?? "";
            return ok;
        }

        public IEnumerable<ICost> GetCosts(CostContext ctx) => _costs(ctx);

        public IResolverAction BuildResolveAction(ResolveContext ctx) => _resolver(ctx);
    }
}