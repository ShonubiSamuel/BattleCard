// Example: "Pay 1000 LP; draw 1 card" (Spell Speed 1, Main Phase)
public sealed class Pay1000Draw1 : YGO.Duel.Chain.IEffectHandle, YGO.Duel.Chain.IOncePerTurn
{
    public string EffectName => "Pay 1000 LP; Draw 1";
    public YGO.Duel.Rules.RuleSet.SpellSpeed Speed => YGO.Duel.Rules.RuleSet.SpellSpeed.One;
    public bool ConsumedThisTurn { get; set; }

    public bool CheckAdditionalConditions(YGO.Duel.Chain.ConditionContext ctx, out string reason)
    {
        // Example: must be open main phase (RuleSet already checks this for Speed 1),
        // could add board-specific extra checks here.
        reason = ""; return true;
    }

    public System.Collections.Generic.IEnumerable<YGO.Duel.Chain.ICost> GetCosts(YGO.Duel.Chain.CostContext ctx)
    {
        yield return new YGO.Duel.Chain.PayLifeCost(1000);
    }

    public YGO.Duel.Chain.IResolverAction BuildResolveAction(YGO.Duel.Chain.ResolveContext ctx)
    {
        return new DrawOneAction();
    }

    private sealed class DrawOneAction : YGO.Duel.Chain.IResolverAction
    {
        public void Resolve(YGO.Duel.Chain.ResolveContext ctx)
        {
            ctx.Board.DrawOne(ctx.Activator);
        }
    }
}