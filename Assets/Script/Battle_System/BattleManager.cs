using System;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using Card = YGO.Duel.Cards.Card;

namespace YGO.Duel.Battle
{
    public enum BattlePosition { Attack, Defense }
    public enum AttackOutcome { None, AttackerDestroyed, DefenderDestroyed, BothDestroyed, NoDestruction }
    public enum DamageType { None, Battle, Effect, Piercing }

    public interface IBattler
    {
        string Name { get; }
        BoardManager.Seat Controller { get; }
        bool CanAttackThisTurn { get; set; }
        bool HasAttackedThisTurn { get; set; }
        bool IsOnField { get; }
        bool IsFaceUp { get; }
        bool IsAttackTargetable { get; }
        bool IsDirectAttackAllowed { get; }
        bool HasPiercing { get; }
        int ATK { get; }
        int DEF { get; }
        BattlePosition Position { get; set; }

        void DestroyByBattle();
        void SendToGraveyard();
        void InflictBattleDamage(int amount, BoardManager.Seat playerDamaged);
        void AfterDamageStep();
    }

    public sealed class BattleManager
    {
        private readonly DamageCalculator _calc;
        private readonly DirectAttackValidator _directValidator;
        private readonly BattleTriggerSystem _triggers;

        public BattleManager(DamageCalculator calc, DirectAttackValidator directValidator, BattleTriggerSystem triggers)
        {
            _calc = calc ?? throw new ArgumentNullException(nameof(calc));
            _directValidator = directValidator ?? throw new ArgumentNullException(nameof(directValidator));
            _triggers = triggers ?? throw new ArgumentNullException(nameof(triggers));
        }

        public event Action<IBattler, IBattler> OnAttackDeclared;
        public event Action<IBattler, IBattler> OnBeforeDamageCalculation;
        public event Action<IBattler, IBattler, AttackOutcome, int, DamageType> OnAfterDamageStep;

        public bool DeclareAttack(IBattler attacker, IBattler targetOrNullForDirect)
        {
            if (attacker == null || !attacker.IsOnField || !attacker.IsFaceUp) return false;
            if (!attacker.CanAttackThisTurn || attacker.HasAttackedThisTurn) return false;

            if (targetOrNullForDirect == null)
            {
                if (!_directValidator.CanDirectAttack(attacker)) return false;
            }
            else
            {
                if (!targetOrNullForDirect.IsOnField || !targetOrNullForDirect.IsAttackTargetable) return false;
                if (attacker.Controller == targetOrNullForDirect.Controller) return false;
            }

            _triggers.RaiseAttackDeclared(attacker, targetOrNullForDirect);
            OnAttackDeclared?.Invoke(attacker, targetOrNullForDirect);
            
            return true;
        }

        public AttackOutcome ResolveDamageStep(IBattler attacker, IBattler targetOrNull, out int lpDamage, out DamageType dmgType)
        {
            lpDamage = 0; dmgType = DamageType.None;
            if (attacker == null || !attacker.IsOnField) return AttackOutcome.None;

            _triggers.RaiseBeforeDamageCalculation(attacker, targetOrNull);
            OnBeforeDamageCalculation?.Invoke(attacker, targetOrNull);

            var outcome = _calc.Compute(attacker, targetOrNull, out lpDamage, out dmgType);

            ApplyDestruction(attacker, targetOrNull, outcome);

            if (lpDamage > 0 && targetOrNull != null)
            {
                var damaged = targetOrNull.Controller;
                attacker.InflictBattleDamage(lpDamage, damaged);
            }
            else if (lpDamage > 0 && targetOrNull == null)
            {
                var damaged = BoardManager.OpponentOf(attacker.Controller);
                attacker.InflictBattleDamage(lpDamage, damaged);
            }

            attacker.AfterDamageStep();
            targetOrNull?.AfterDamageStep();

            attacker.HasAttackedThisTurn = true;

            _triggers.RaiseAfterDamageStep(attacker, targetOrNull, outcome, lpDamage, dmgType);
            OnAfterDamageStep?.Invoke(attacker, targetOrNull, outcome, lpDamage, dmgType);

            return outcome;
        }

        private static void ApplyDestruction(IBattler attacker, IBattler targetOrNull, AttackOutcome outcome)
        {
            switch (outcome)
            {
                case AttackOutcome.AttackerDestroyed:
                    attacker.DestroyByBattle();
                    break;
                case AttackOutcome.DefenderDestroyed:
                    targetOrNull?.DestroyByBattle();
                    break;
                case AttackOutcome.BothDestroyed:
                    attacker.DestroyByBattle();
                    targetOrNull?.DestroyByBattle();
                    break;
            }
        }
    }
}
