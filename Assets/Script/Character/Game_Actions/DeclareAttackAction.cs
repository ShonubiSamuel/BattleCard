using System;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Battle;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using Card = YGO.Duel.Cards.Card;

namespace YGO.Duel.Runtime.Actions
{
    [Serializable]
    public sealed class DeclareAttackAction : GameAction
    {
        public override ActionType Type => ActionType.DeclareAttack;

        public string attackerId;
        public string targetId; // null/empty => direct

        public override bool Validate(ActionContext ctx, out string reason)
        {
            reason = "";
            if (string.IsNullOrEmpty(attackerId)) { reason = "Missing attackerId"; return false; }
            if (ctx.Turns != null)
            {
                if (ctx.Turns.CurrentPhase != RuleSet.Phase.Battle) { reason = "Not in Battle Phase"; return false; }
                if (ctx.Turns.CurrentPlayer != seat) { reason = "Not your turn"; return false; }
            }
            return true; // deeper legality handled by BattleManager
        }

        // DeclareAttackAction.cs
        public override bool Execute(ActionContext ctx, out string error)
        {
            
            Debug.Log("execite");
            error = "";

            var atkCard = ActionUtil.ResolveCard(ctx, attackerId, seat, out error);
            if (atkCard == null) return false;

            Card tgtCard = null;
            if (!string.IsNullOrEmpty(targetId))
            {
                tgtCard = ActionUtil.ResolveCard(ctx, targetId, BoardManager.OpponentOf(seat), out error);
                if (tgtCard == null) return false;
            }

            if (!ServiceLocator.TryGet<BattleManager>(out var battle) || battle == null)
            {
                ctx.Logger.LogText("Action.Attack", "(No BattleManager) Declare noop",
                    data: $"attacker={attackerId}; target={(tgtCard!=null?targetId:"(direct)")}",
                    source: nameof(DeclareAttackAction));
                return true;
            }

            IBattler atk = null, tgt = null;
            if (ServiceLocator.TryGet<IBattlerResolver>(out var resolver) && resolver != null)
            {
                atk = resolver.Resolve(atkCard);
                if (tgtCard != null) tgt = resolver.Resolve(tgtCard);
            }
            if (atk == null) { error = "Could not resolve attacker to IBattler"; return false; }

            if (!battle.DeclareAttack(atk, tgt))
            {
                error = "BattleManager rejected attack";
                return false;
            }

            // NOTE: Do NOT resolve damage here anymore.
            ctx.Logger.LogText("Action.Attack", "Attack declared",
                data: $"attacker={attackerId}; target={(tgtCard!=null?targetId:"(direct)")}",
                source: nameof(DeclareAttackAction));

            return true;
        }

    }
}
