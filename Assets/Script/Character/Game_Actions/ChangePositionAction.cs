// ChangePositionAction.cs
// Requests a battle position change (e.g., ATK ↔ DEF). PositionManager enforces once-per-turn, etc.

using System;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Battle;    // BattlePosition, PositionManager (from your battle system)
using YGO.Duel.Foundation;

namespace YGO.Duel.Runtime.Actions
{
    [Serializable]
    public sealed class ChangePositionAction : GameAction
    {
        public override ActionType Type => ActionType.ChangePosition;

        public string cardId;
        public BattlePosition toPosition; // Attack or Defense
        public bool faceUp = true;        // for flips (if supported by your runtime)

        public override bool Validate(ActionContext ctx, out string reason)
        {
            reason = "";
            if (string.IsNullOrEmpty(cardId)) { reason = "Missing cardId"; return false; }
            // Defer "once per turn" / timing to PositionManager.
            return true;
        }

        public override bool Execute(ActionContext ctx, out string error)
        {
            error = "";
            var c = ActionUtil.ResolveCard(ctx, cardId, seat, out error);
            if (c == null) return false;

            if (!ServiceLocator.TryGet<PositionManager>(out var pos) || pos == null)
            {
                ctx.Logger.LogText("Action.ChangePos", $"(No PositionManager) Request position change",
                    data: $"card={cardId}; to={toPosition}; faceUp={faceUp}", source: nameof(ChangePositionAction));
                return true;
            }

            var ok = pos.RequestPositionChange(c, toPosition, faceUp, out error);
            if (ok)
            {
                ctx.Logger.LogText("Action.ChangePos", $"Position change",
                    data: $"card={cardId}; to={toPosition}; faceUp={faceUp}", source: nameof(ChangePositionAction));
            }
            return ok;
        }
    }
}