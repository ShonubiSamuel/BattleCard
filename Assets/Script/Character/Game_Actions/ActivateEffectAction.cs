// ActivateEffectAction.cs
// Announce activation of an effect. Chain building/resolution happens in your ChainManager.

using System;
using System.Collections.Generic;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;

namespace YGO.Duel.Runtime.Actions
{
    [Serializable]
    public sealed class ActivateEffectAction : GameAction
    {
        public override ActionType Type => ActionType.ActivateEffect;

        public string sourceInstanceId;          // card or skill id
        public string effectId;                  // which effect on the card (if multiple)
        public List<string> targetIds = new List<string>(); // targets locked at activation (optional)

        public override bool Validate(ActionContext ctx, out string reason)
        {
            // Minimal pre-checks. Deeper legality sits in Condition/Cost systems.
            reason = "";
            if (string.IsNullOrEmpty(sourceInstanceId)) { reason = "Missing sourceInstanceId"; return false; }
            if (ctx.Rules == null || ctx.Turns == null) return true; // offline unit test path

            // Simple check: spell speed > 1 may need a response window; leave to Chain/RuleSet.CanActivateEffect.
            return true;
        }

        public override bool Execute(ActionContext ctx, out string error)
        {
            error = "";
            if (ctx.Logger == null) ctx.Logger = new DuelLogger();

            // If you have a ChainManager, push a link here. Otherwise, just log.
            if (ServiceLocator.TryGet<object>(out var chainObj) && chainObj != null)
            {
                // You likely have a typed ChainManager; call your AddLink(...) with proper EffectHandle.
                // Here we only log to avoid coupling.
            }

            ctx.Logger.LogText("Action.ActivateEffect",
                $"Activate effect {effectId ?? "(default)"}",
                data: $"src={sourceInstanceId}; targets={string.Join(",", targetIds ?? new List<string>())}; seat={seat}",
                source: nameof(ActivateEffectAction));
            return true;
        }
    }
}
