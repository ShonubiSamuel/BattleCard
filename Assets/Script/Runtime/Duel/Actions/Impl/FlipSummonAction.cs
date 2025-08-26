using System;
using YGO.Duel.Battle;
using YGO.Duel.Board;
using YGO.Duel.Foundation;

namespace YGO.Duel.Runtime.Actions
{
    [Serializable]
    public sealed class FlipSummonAction : GameAction
    {
        public override ActionType Type => ActionType.Custom; // or add a new enum if you prefer
        public string monsterId;

        public override bool Validate(ActionContext ctx, out string reason)
        {
            reason = "";
            if (ctx?.Board == null || ctx?.Turns == null || ctx?.Rules == null) { reason = "Ctx missing"; return false; }

            var card = ActionUtil.ResolveCard(ctx, monsterId, seat, out reason);
            if (card == null) return false;
            if (card.Controller != seat) { reason = "Not your card"; return false; }
            if (card.CurrentZone != BoardManager.CardZone.Monster) { reason = "Not on field"; return false; }

            if (!ServiceLocator.TryGet<PositionManager>(out var pm) || pm == null) { reason = "PositionManager missing"; return false; }
            return pm.CanFlipSummonNow(card, ctx.Rules, ctx.Turns, out reason);
        }

        public override bool Execute(ActionContext ctx, out string error)
        {
            error = "";
            if (!Validate(ctx, out error)) return false;

            var card = ActionUtil.ResolveCard(ctx, monsterId, seat, out error);
            if (card == null) return false;

            if (!ServiceLocator.TryGet<PositionManager>(out var pm) || pm == null)
            { error = "PositionManager missing"; return false; }

            // Face-down → face-up ATK
            if (!pm.RequestPositionChange(card, YGO.Duel.Battle.BattlePosition.Attack, faceUp: true, out error))
                return false;

            // After a Flip Summon, monster cannot attack this turn
            pm.SetCanAttackThisTurn(card, false);

            // Raise Flip Summon + face change events
            if (ServiceLocator.TryGet<EventBus>(out var bus) && bus != null)
            {
                var mzIndex = card.ZoneIndex;
                bus.RaiseCardFaceChanged(card, isFaceUp: true, FaceChangeReason.Manual);
                bus.RaiseSummoned(card, seat, SummonType.Flip, mzIndex);
            }

            ctx.Logger?.LogText("Action.FlipSummon", $"Flip Summon {card.Name}", source: nameof(FlipSummonAction));
            return true;
        }
    }
}
