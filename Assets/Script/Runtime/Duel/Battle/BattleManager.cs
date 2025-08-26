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

        // BattleManager.cs
        public bool TryDeclareAttack(IBattler attacker, IBattler targetOrNull, out string reason)
        {
            reason = "";
            if (attacker == null) { reason = "attacker is null"; return false; }
            if (!attacker.IsOnField) { reason = "attacker not on field"; return false; }
            if (!attacker.IsFaceUp) { reason = "attacker not face-up"; return false; }
            if (!attacker.CanAttackThisTurn) { reason = "attacker cannot attack this turn"; return false; }
            if (attacker.HasAttackedThisTurn) { reason = "attacker already attacked this turn"; return false; }

            if (targetOrNull == null)
            {
                if (!_directValidator.CanDirectAttack(attacker))
                {
                    reason = "direct attack not allowed (opponent controls a monster or effect forbids)";
                    return false;
                }
            }
            else
            {
                if (!targetOrNull.IsOnField) { reason = "target not on field"; return false; }
                if (!targetOrNull.IsAttackTargetable) { reason = "target not attack-targetable"; return false; }
                if (attacker.Controller == targetOrNull.Controller) { reason = "cannot attack your own monster"; return false; }
            }

            _triggers.RaiseAttackDeclared(attacker, targetOrNull);
            OnAttackDeclared?.Invoke(attacker, targetOrNull);
            return true;
        }

// keep existing API as a wrapper
        public bool DeclareAttack(IBattler attacker, IBattler targetOrNullForDirect)
            => TryDeclareAttack(attacker, targetOrNullForDirect, out _);


        public AttackOutcome ResolveDamageStep(IBattler attacker, IBattler targetOrNull, out int lpDamage, out DamageType dmgType)
        {
            lpDamage = 0; 
            dmgType  = DamageType.None;

            if (attacker == null || !attacker.IsOnField)
                return AttackOutcome.None;

            // If the target is face-down, flip it face-up (preserve its position) before calculation
            if (targetOrNull != null && !targetOrNull.IsFaceUp)
            {
                if (targetOrNull is CardBattlerAdapter cba)
                    cba.RevealForBattleIfNeeded(); // raises CardFaceChanged + updates visuals via PositionManager
            }

            // Timing/trigger hooks
            _triggers.RaiseBeforeDamageCalculation(attacker, targetOrNull);
            OnBeforeDamageCalculation?.Invoke(attacker, targetOrNull);

            // Core combat math
            var outcome = _calc.Compute(attacker, targetOrNull, out lpDamage, out dmgType);

            // Destroy as needed
            ApplyDestruction(attacker, targetOrNull, outcome);

            // Decide LP damage recipient based on outcome (and direct-attack case)
            BoardManager.Seat? victim = null;

            if (targetOrNull == null)
            {
                // Direct attack → opponent takes it
                victim = BoardManager.OpponentOf(attacker.Controller);
            }
            else
            {
                switch (outcome)
                {
                    case AttackOutcome.DefenderDestroyed:
                        // Attacker wins (ATK>ATK) or breaks DEF; piercing already reflected in lpDamage/dmgType
                        victim = targetOrNull.Controller;
                        break;

                    case AttackOutcome.AttackerDestroyed:
                        // Attacker loses (ATK<ATK) → attacker’s controller takes the difference
                        victim = attacker.Controller;
                        break;

                    case AttackOutcome.BothDestroyed:
                    case AttackOutcome.NoDestruction:
                    default:
                        victim = null; // No LP damage
                        break;
                }
            }

            if (lpDamage > 0 && victim.HasValue)
                attacker.InflictBattleDamage(lpDamage, victim.Value);

            // Post-step hooks
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
