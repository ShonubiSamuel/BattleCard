using System.Collections.Generic;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Chain;
using YGO.Duel.Effects;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;

[CreateAssetMenu(fileName = "Fissure", menuName = "YGO/Effects/Blueprints/Basic/Fissure")]
public sealed class FissureBlueprint : EffectBlueprintBase
{
    [Header("Presentation")]
    public string displayName = "Fissure";

    public override IEffectHandle BuildHandle(Card source, string effectId = "")
    {
        (bool, string) Condition(ConditionContext ctx)
        {
            // Can only activate if opponent controls a monster
            var opp = BoardManager.OpponentOf(ctx.Activator);
            if (!ctx.Board.OpponentControlsAnyMonsters(ctx.Activator))
                return (false, "Opponent controls no monsters");
            return (true, "");
        }

        IEnumerable<ICost> Costs(CostContext ctx) => System.Array.Empty<ICost>();

        IResolverAction Resolver(ResolveContext ctx) => new ResolveActionImpl(this, ctx);

        return new ScriptedEffectHandle(displayName, declaredSpeed, Condition, Costs, Resolver);
    }

    private sealed class ResolveActionImpl : IResolverAction
    {
        private readonly FissureBlueprint _bp;
        private readonly ResolveContext _ctx;

        public ResolveActionImpl(FissureBlueprint bp, ResolveContext ctx)
        {
            _bp = bp;
            _ctx = ctx;
        }

        public void Resolve(ResolveContext ctx)
        {
            var board = ctx.Board;
            var me = ctx.Activator;
            var opp = BoardManager.OpponentOf(me);

            var oppMonsters = new List<Card>();
            foreach (var mz in board.Zones[(int)opp].Monsters)
            {
                var c = mz.Top();
                if (c != null) oppMonsters.Add(c);
            }

            if (oppMonsters.Count == 0) return;

            // Find lowest ATK monster
            Card lowest = null;
            int lowestAtk = int.MaxValue;

            foreach (var c in oppMonsters)
            {
                int atk = c.Def?.baseATK ?? 0;
                if (atk < lowestAtk)
                {
                    lowestAtk = atk;
                    lowest = c;
                }
            }

            if (lowest != null)
            {
                var log = ServiceLocator.TryGet<DuelLogger>(out var l) ? l : null;
                EffectOps.TryDestroy(board, log, lowest);
            }
        }
    }
}