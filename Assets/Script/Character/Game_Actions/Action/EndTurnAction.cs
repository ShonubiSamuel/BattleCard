using System;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;

namespace YGO.Duel.Runtime.Actions
{
    [Serializable]
    public sealed class EndTurnAction : GameAction
    {
        public override ActionType Type => ActionType.EndTurn;

        public override bool Execute(ActionContext ctx, out string error)
        {
            error = "";
            if (ctx.Turns == null) { error = "TurnManager not available"; return false; }

            var startTurn  = ctx.Turns.TurnNumber;
            var safety     = 12; // plenty to roll End -> Draw

            ctx.Logger?.LogText("Action.EndTurn", $"Requested from {ctx.Turns.CurrentPhase}", source: nameof(EndTurnAction));

            while (safety-- > 0)
            {
                var beforeTurn  = ctx.Turns.TurnNumber;
                var beforePhase = ctx.Turns.CurrentPhase;
                ctx.Turns.AdvancePhase();

                // Turn number increased => new turn started
                if (ctx.Turns.TurnNumber > startTurn)
                    return true;

                // If we were at End and now Draw, new turn started as well
                if (beforePhase == RuleSet.Phase.End && ctx.Turns.CurrentPhase == RuleSet.Phase.Draw && ctx.Turns.TurnNumber == startTurn + 1)
                    return true;
            }

            error = "Failed to advance to next turn (safety tripped)";
            return false;
        }
    }
}