using System;
using YGO.Duel.Board;
using YGO.Duel.Battle;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Runtime;
using Card = YGO.Duel.Cards.Card;

namespace YGO.Duel.Runtime.Actions
{
    [Serializable]
    public sealed class ChangePositionAction : GameAction
    {
        public override ActionType Type => ActionType.ChangePosition;

        // ---- Back-compat fields (either can be filled by older callers) ----
        public string cardId;                    // old name
        public string monsterId;                 // new name

        public BattlePosition toPosition;        // old name
        public BattlePosition to;                // new name

        // Optional legacy: some callers tried to flip with this
        public bool faceUp = true;

        private string IdNorm => string.IsNullOrEmpty(monsterId) ? cardId : monsterId;
        private BattlePosition ToNorm => to != 0 ? to : toPosition;

        public override bool Validate(ActionContext ctx, out string reason)
        {
            reason = "";
            if (ctx?.Board == null || ctx?.Turns == null || ctx?.Rules == null) { reason = "Context missing"; return false; }

            var id = IdNorm;
            if (string.IsNullOrEmpty(id)) { reason = "Missing card id"; return false; }

            var card = ActionUtil.ResolveCard(ctx, id, seat, out reason);
            if (card == null) return false;
            if (card.Controller != seat) { reason = "Not your card"; return false; }
            if (card.CurrentZone != BoardManager.CardZone.Monster) { reason = "Not on field"; return false; }

            if (!ServiceLocator.TryGet<PositionManager>(out var pm) || pm == null)
            { reason = "PositionManager missing"; return false; }

            var wantFaceUp = faceUp;           // caller’s desired face
            var curFaceUp  = card.IsFaceUp;
            var wantPos    = ToNorm;

            // If caller tries to flip FD -> FU (Flip Summon timing)
            if (!curFaceUp && wantFaceUp)
                return pm.CanFlipSummonNow(card, ctx.Rules, ctx.Turns, out reason);

            // Otherwise, pure ATK/DEF change (maintain current face)
            return pm.CanChangePositionNow(card, ctx.Rules, ctx.Turns, out reason);
        }

        public override bool Execute(ActionContext ctx, out string error)
        {
            error = "";
            if (!Validate(ctx, out error)) return false;

            var card = ActionUtil.ResolveCard(ctx, IdNorm, seat, out error);
            if (card == null) return false;

            if (!ServiceLocator.TryGet<PositionManager>(out var pm) || pm == null)
            { error = "PositionManager missing"; return false; }

            var wantFaceUp = faceUp;
            var curFaceUp  = card.IsFaceUp;
            var wantPos    = ToNorm;

            // Flip Summon path: FD -> FU ATK
            if (!curFaceUp && wantFaceUp)
            {
                if (!pm.RequestPositionChange(card, BattlePosition.Attack, faceUp: true, out error))
                    return false;

                // Classic: after Flip Summon, that monster cannot attack this turn
                pm.SetCanAttackThisTurn(card, false);

                if (ServiceLocator.TryGet<EventBus>(out var bus) && bus != null)
                {
                    bus.RaiseCardFaceChanged(card, isFaceUp: true, FaceChangeReason.Manual);
                    bus.RaiseSummoned(card, seat, SummonType.Flip, card.ZoneIndex);
                }

                ctx.Logger?.LogText("Action.FlipSummon", $"Flip Summon {card.Name}", source: nameof(ChangePositionAction));
                return true;
            }

            // Regular position change (keep current face)
            var faceToKeep = curFaceUp;
            if (!pm.RequestPositionChange(card, wantPos, faceUp: faceToKeep, out error))
                return false;

            // After a manual position change, cannot attack this turn (classic)
            pm.SetCanAttackThisTurn(card, false);

            ctx.Logger?.LogText("Action.ChangePos", $"{card.Name} -> {wantPos}/{(faceToKeep ? "FU" : "FD")}",
                source: nameof(ChangePositionAction));
            return true;
        }
    }
}
