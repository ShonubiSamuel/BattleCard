// BattleTriggerSystem.cs
using System;
using YGO.Duel.Board;
using YGO.Duel.Foundation;

namespace YGO.Duel.Battle
{
    public sealed class BattleTriggerSystem
    {
        private readonly DuelLogger _logger;
        private readonly EventBus _bus;
        private readonly PositionManager _pos; // optional for side-effects

        public BattleTriggerSystem(DuelLogger logger, PositionManager pos, EventBus bus)
        {
            _logger = logger ?? new DuelLogger();
            _pos    = pos;
            _bus    = bus;
        }

        public void RaiseAttackDeclared(IBattler attacker, IBattler target)
        {
            _logger.LogText("Battle.AttackDeclared",
                $"{attacker?.Name} → {(target!=null ? target.Name : "Direct")}",
                source: nameof(BattleTriggerSystem));
            _bus?.RaiseAttackDeclared(attacker, target);
        }

        public void RaiseBeforeDamageCalculation(IBattler attacker, IBattler target)
        {
            _logger.LogText("Battle.BeforeDamageCalc",
                $"{attacker?.Name} vs {(target!=null ? target.Name : "Direct")}",
                source: nameof(BattleTriggerSystem));
            // If you open a priority window here, call into your PriorityManager.
        }

        public void RaiseAfterDamageStep(IBattler attacker, IBattler target,
            AttackOutcome outcome, int lpDamage, DamageType dmgType)
        {
            _logger.LogText("Battle.AfterDamageStep",
                $"{outcome} dmg={lpDamage} type={dmgType}",
                source: nameof(BattleTriggerSystem));

            if (lpDamage > 0 && attacker != null)
            {
                var victim = target != null
                    ? target.Controller
                    : BoardManager.OpponentOf(attacker.Controller);
                _bus?.RaiseBattleDamage(victim, lpDamage);
            }
        }
    }
}