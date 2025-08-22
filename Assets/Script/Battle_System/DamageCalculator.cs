// DamageCalculator.cs
// Pure combat math (ATK/DEF comparisons, position rules, piercing).

using System;

namespace YGO.Duel.Battle
{
    public sealed class DamageCalculator
    {
        /// <summary>
        /// Compute battle outcome. Returns destruction outcome and outputs LP damage and type.
        /// If target is null, treat as direct attack.
        /// </summary>
        public AttackOutcome Compute(IBattler attacker,
                                     IBattler targetOrNull,
                                     out int lpDamage,
                                     out DamageType damageType)
        {
            lpDamage = 0; damageType = DamageType.None;

            if (attacker == null) return AttackOutcome.None;

            // Direct attack
            if (targetOrNull == null)
            {
                lpDamage = Math.Max(0, attacker.ATK);
                damageType = lpDamage > 0 ? DamageType.Battle : DamageType.None;
                return AttackOutcome.NoDestruction;
            }

            // Targeted battle
            if (targetOrNull.Position == BattlePosition.Attack)
            {
                int a = attacker.ATK;
                int d = targetOrNull.ATK;

                if (a > d)
                {
                    lpDamage = a - d;
                    damageType = DamageType.Battle;
                    return AttackOutcome.DefenderDestroyed;
                }
                else if (a < d)
                {
                    lpDamage = d - a;
                    damageType = DamageType.Battle;
                    return AttackOutcome.AttackerDestroyed;
                }
                else
                {
                    // Equal ATK: both destroyed, no LP damage
                    return AttackOutcome.BothDestroyed;
                }
            }
            else // Defender in DEF
            {
                int a = attacker.ATK;
                int d = targetOrNull.DEF;

                if (a > d)
                {
                    // Attacker > DEF: Defender destroyed; LP damage only if piercing
                    if (attacker.HasPiercing)
                    {
                        lpDamage = a - d;
                        damageType = DamageType.Piercing;
                    }
                    return AttackOutcome.DefenderDestroyed;
                }
                else if (a < d)
                {
                    // Attacker < DEF: no destruction; attacker’s controller may take damage? (No—classic YGO says no LP loss here)
                    // Some custom rules inflict (DEF - ATK) to attacker’s controller—omit unless you enable that variant.
                    return AttackOutcome.NoDestruction;
                }
                else
                {
                    // Equal ATK and DEF: no destruction, no damage
                    return AttackOutcome.NoDestruction;
                }
            }
        }
    }
}
