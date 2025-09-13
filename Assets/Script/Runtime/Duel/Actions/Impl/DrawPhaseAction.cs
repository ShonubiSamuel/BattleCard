// Assets/Script/Runtime/Duel/Actions/Impl/DrawPhaseAction.cs
using System;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Systems;

namespace YGO.Duel.Runtime.Actions
{
    /// <summary>
    /// Perform the draw(s) appropriate for the Draw Phase.
    /// This action ONLY draws; phase advancement is done by the caller (e.g., PhaseRibbon).
    /// </summary>
    [Serializable]
    public sealed class DrawPhaseAction : GameAction
    {
        public override ActionType Type => ActionType.Custom; // or add a new enum if you have it

        public override bool Validate(ActionContext ctx, out string reason)
        {
            reason = "";
            if (ctx?.Turns == null || ctx?.Rules == null) { reason = "Missing turns/rules"; return false; }
            if (ctx.Turns.CurrentPhase != RuleSet.Phase.Draw) { reason = "Not Draw Phase"; return false; }
            return true;
        }

        public override bool Execute(ActionContext ctx, out string error)
        {
            error = "";
            if (!Validate(ctx, out error)) return false;

            // Respect first-turn draw rule
            var isFirstTurnNoDraw = (ctx.Turns.TurnNumber == 1) && !ctx.Rules.ShouldFirstTurnDraw();
            if (isFirstTurnNoDraw)
            {
                ctx.Logger?.LogText("Draw.Skip", "First-turn draw skipped by rules",
                    data:$"seat=P{(ctx.Turns.CurrentPlayer==BoardManager.Seat.P1?1:2)}; turn={ctx.Turns.TurnNumber}",
                    source:nameof(DrawPhaseAction));
                return true; // success (no draw)
            }

            if (!ServiceLocator.TryGet(out DrawSystem draws) || draws == null)
            {
                error = "DrawSystem missing";
                return false;
            }

            var seat = ctx.Turns.CurrentPlayer;
            if (!draws.Draw(seat, 1, DrawReason.TurnStart, out _))
            {
                // Drawing failed (e.g., empty deck) — still succeed so the game can continue
                ctx.Logger?.LogText("Draw.Failed", "No card drawn (deck empty?)",
                    data:$"seat=P{(seat==BoardManager.Seat.P1?1:2)}", source:nameof(DrawPhaseAction));
            }
            return true;
        }
    }
}