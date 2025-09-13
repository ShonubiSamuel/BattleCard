using System;
using YGO.Duel.Foundation;
using YGO.Duel.Chain;

namespace YGO.Duel.Runtime.Actions
{
    /// <summary>
    /// Resolves the top link on the chain (LIFO).
    /// Your IChainService should: pop the link, perform its effect, and return which link resolved.
    /// </summary>
    [Serializable]
    public sealed class ResolveChainAction : GameAction
    {
        public override ActionType Type => ActionType.Custom; // or ActionType.ResolveChain

        public override bool Validate(ActionContext ctx, out string reason)
        {
            reason = "";
            if (!ServiceLocator.TryGet<IChainManager>(out var chain) || chain == null)
            { reason = "Chain service missing"; return false; }

            if (chain.IsEmpty)
            { reason = "Chain is empty"; return false; }

            return true;
        }

        public override bool Execute(ActionContext ctx, out string error)
        {
            error = "";

            if (!ServiceLocator.TryGet<IChainManager>(out var chain) || chain == null)
            {
                error = "Chain service missing";
                return false;
            }

            if (!chain.ResolveTop(out var resolved) || resolved == null)
            {
                error = "Chain is empty";
                return false;
            }

            // Optional: tell the world the source effect finished (card-focused path)
            if (ServiceLocator.TryGet<EventBus>(out var bus) && bus != null)
            {
                if (resolved.IsCardSource && resolved.Source is YGO.Duel.Cards.Card srcCard)
                {
                    var effectLabel = resolved.Effect?.EffectName ?? resolved.ActivationSummary ?? "";
                    bus.RaiseCardEffectResolved(srcCard, effectLabel); // see EventBus overload note below
                }
                else
                {
                    // Generic path (no concrete Card source)
                    bus.RaiseChainResolved(resolved);  // ChainManager already does this, but harmless if duplicated
                }
            }

            var pretty = resolved.Effect?.EffectName ?? resolved.ActivationSummary ?? "Effect";
            ctx.Logger?.LogText("Action.ChainResolve", $"Resolved {pretty}",
                data:$"src={(resolved.IsCardSource && resolved.Source is YGO.Duel.Cards.Card c ? c.Name : resolved.SourceId)}",
                source:nameof(ResolveChainAction));

            return true;
        }
    }
}