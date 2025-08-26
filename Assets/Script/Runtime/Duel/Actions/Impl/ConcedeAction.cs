// ConcedeAction.cs
// A player concedes the duel. Here we set LP to 0 and log; your higher-level system can end the match.

using System;
using YGO.Duel.Board;
using YGO.Duel.Foundation;

namespace YGO.Duel.Runtime.Actions
{
    [Serializable]
    public sealed class ConcedeAction : GameAction
    {
        public override ActionType Type => ActionType.Concede;

        public string reason;

        public override bool Validate(ActionContext ctx, out string outReason)
        {
            outReason = "";
            return true;
        }

        public override bool Execute(ActionContext ctx, out string error)
        {
            error = "";
            if (ctx.Board == null) { error = "Board missing"; return false; }

            var ps = ctx.Board.Players[(int)seat];
            int before = ps.LifePoints;
            ps.LifePoints = 0;

            ctx.Logger.LogText("Action.Concede", "Player concedes",
                data: $"seat={seat}; lp:{before}->0; reason={reason ?? ""}", source: nameof(ConcedeAction));

            // Optional: raise an event from a MatchManager to stop timers, tally score, etc.
            return true;
        }
    }
}