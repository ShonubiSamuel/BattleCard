// ResolveDamageStepAction.cs
using System;
using YGO.Duel.Board;
using YGO.Duel.Battle;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using Card = YGO.Duel.Cards.Card;

namespace YGO.Duel.Runtime.Actions
{
    [Serializable]
    public sealed class ResolveDamageStepAction : GameAction
    {
        public override ActionType Type => ActionType.Custom; // or add a dedicated enum value
        public string attackerId;
        public string targetId; // null/empty => direct

        public override bool Validate(ActionContext ctx, out string reason)
        {
            reason = "";
            if (ctx?.Turns?.CurrentPhase != RuleSet.Phase.Battle) { reason = "Not in Battle Phase"; return false; }
            return true;
        }

        public override bool Execute(ActionContext ctx, out string error)
        {
            error = "";
            if (!ServiceLocator.TryGet<BattleManager>(out var battle) || battle == null)
            { error = "No BattleManager"; return false; }

            var atkCard = ActionUtil.ResolveCard(ctx, attackerId, seat, out error);
            if (atkCard == null) return false;

            Card tgtCard = null;
            if (!string.IsNullOrEmpty(targetId))
            {
                tgtCard = ActionUtil.ResolveCard(ctx, targetId, BoardManager.OpponentOf(seat), out error);
                if (tgtCard == null) return false;
            }

            if (!ServiceLocator.TryGet<IBattlerResolver>(out var resolver) || resolver == null)
            { error = "No IBattlerResolver"; return false; }

            var atk = resolver.Resolve(atkCard);
            var tgt = (tgtCard != null) ? resolver.Resolve(tgtCard) : null;
            if (atk == null) { error = "Resolve attacker failed"; return false; }

            battle.ResolveDamageStep(atk, tgt, out var lp, out var type);
            return true;
        }
    }

}
