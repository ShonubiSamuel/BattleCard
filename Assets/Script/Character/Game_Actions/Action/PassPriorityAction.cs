using System;
using YGO.Duel.Foundation;

namespace YGO.Duel.Runtime.Actions
{
    /// <summary>Lightweight priority pass. If no priority service is present, we log and succeed.</summary>
    public interface IPriorityService
    {
        bool PassPriority(out string error);
    }

    [Serializable]
    public sealed class PassPriorityAction : GameAction
    {
        public override ActionType Type => ActionType.PassPriority;

        public override bool Execute(ActionContext ctx, out string error)
        {
            error = "";
            if (ServiceLocator.TryGet<IPriorityService>(out var pri) && pri != null)
            {
                var ok = pri.PassPriority(out error);
                if (ok) ctx.Logger?.LogText("Action.PassPriority", "Priority passed", source: nameof(PassPriorityAction));
                return ok;
            }

            // No priority system wired yet — treat as no-op success for SP/bring-up
            ctx.Logger?.LogText("Action.PassPriority", "(No PriorityService) noop", source: nameof(PassPriorityAction));
            return true;
        }
    }
}