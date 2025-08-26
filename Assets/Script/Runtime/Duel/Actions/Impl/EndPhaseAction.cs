using System;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;

namespace YGO.Duel.Runtime.Actions
{
    [Serializable]
    public sealed class EndPhaseAction : GameAction
    {
        public override ActionType Type => ActionType.EndPhase;

        public override bool Execute(ActionContext ctx, out string error)
        {
            error = "";
            if (ctx.Turns == null) { error = "TurnManager not available"; return false; }
            ctx.Logger?.LogText("Action.EndPhase", $"Advance from {ctx.Turns.CurrentPhase}", source: nameof(EndPhaseAction));
            ctx.Turns.AdvancePhase();
            return true;
        }
    }
}