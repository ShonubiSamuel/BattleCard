// Scripts/Runtime/Duel/Effects/Blueprints/RaigekiBlueprint.cs
using System.Collections.Generic;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Chain;
using YGO.Duel.Effects;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;

[CreateAssetMenu(fileName = "Raigeki", menuName = "YGO/Effects/Blueprints/Basic/Raigeki")]
public sealed class RaigekiBlueprint : EffectBlueprintBase
{
    [Header("Rules")]
    [Tooltip("Classic Raigeki affects face-up monsters; many engines just destroy all. Leave true for face-up only.")]
    public bool faceUpOnly = true;

    [Header("Presentation")]
    public string displayName = "Raigeki";

    [SerializeField] private RuleSet.SpellSpeed declaredSpeed = RuleSet.SpellSpeed.One;
    public RuleSet.SpellSpeed DeclaredSpeed => declaredSpeed;

    public override IEffectHandle BuildHandle(Card source, string effectId = "")
    {
        (bool, string) Condition(ConditionContext ctx) => (true, "");
        System.Collections.Generic.IEnumerable<ICost> Costs(CostContext ctx) => System.Array.Empty<ICost>();
        IResolverAction Resolver(ResolveContext ctx) => new ResolveActionImpl(this);

        return new ScriptedEffectHandle(displayName, declaredSpeed, Condition, Costs, Resolver);
    }

    private sealed class ResolveActionImpl : IResolverAction
    {
        private readonly RaigekiBlueprint _bp;
        public ResolveActionImpl(RaigekiBlueprint bp) { _bp = bp; }

        public void Resolve(ResolveContext ctx)
        {
            var board = ctx.Board;
            var log = ServiceLocator.TryGet<DuelLogger>(out var l) ? l : null;

            var opp = BoardManager.OpponentOf(ctx.Activator);
            var mons = GatherMonsters(board, opp, _bp.faceUpOnly);
            EffectOps.DestroyCards(board, log, mons);
        }

        private static IEnumerable<Card> GatherMonsters(BoardManager board, BoardManager.Seat seat, bool faceUpOnly)
        {
            var arr = board?.Zones[(int)seat]?.Monsters;
            if (arr == null) yield break;
            for (int i = 0; i < arr.Length; i++)
            {
                var c = arr[i].Top();
                if (c == null) c = arr[i].Card; // legacy
                if (c != null && (!faceUpOnly || c.IsFaceUp)) yield return c;
            }
        }
    }
}