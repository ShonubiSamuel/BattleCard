// TimingTable.cs
// Central map: engine events → RuleSet.Timing windows. Extensible with Register/Append APIs.

using System;
using System.Collections.Generic;

namespace YGO.Duel.Rules
{
    /// <summary>High-level engine events that may open response windows.</summary>
    public enum EngineEvent
    {
        OpenGameState,                  // generic window in Main/Battle/etc.
        NormalSummonSuccess,
        SpecialSummonSuccess,
        FlipSummonSuccess,
        AttackDeclared,
        BattleStepStart,
        DamageStepStart,
        BeforeDamageCalculation,
        DamageCalculation,
        AfterDamageCalculation,
        EndOfDamageStep,
        CardDestroyed,
        CardSentToGY,
        CardBanished,
        PhaseStart,
        PhaseEnd,
        ChainLinkResolved
    }

    /// <summary>
    /// Timing map → returns a sequence of RuleSet.Timing “moments” that are valid for a given engine event.
    /// You can override defaults via Register/Append.
    /// </summary>
    public sealed class TimingTable
    {
        private readonly Dictionary<EngineEvent, RuleSet.Timing[]> _map = new Dictionary<EngineEvent, RuleSet.Timing[]>();

        public TimingTable()
        {
            // —— sensible defaults (you can tweak freely) ——
            Register(EngineEvent.OpenGameState, new[] { RuleSet.Timing.OpenGameState });

            Register(EngineEvent.NormalSummonSuccess,  new[] { RuleSet.Timing.OnSummonSuccess, RuleSet.Timing.OpenGameState });
            Register(EngineEvent.SpecialSummonSuccess, new[] { RuleSet.Timing.OnSummonSuccess, RuleSet.Timing.OpenGameState });
            Register(EngineEvent.FlipSummonSuccess,    new[] { RuleSet.Timing.OnSummonSuccess, RuleSet.Timing.OpenGameState });

            Register(EngineEvent.AttackDeclared,       new[] { RuleSet.Timing.OnAttackDeclared });
            Register(EngineEvent.BattleStepStart,      new[] { RuleSet.Timing.OnBattleStepStart });
            Register(EngineEvent.DamageStepStart,      new[] { RuleSet.Timing.OnDamageStepStart });
            Register(EngineEvent.BeforeDamageCalculation, new[] { RuleSet.Timing.BeforeDamageCalc });
            Register(EngineEvent.DamageCalculation,    new[] { RuleSet.Timing.DuringDamageCalc }); // limited activations by rule
            Register(EngineEvent.AfterDamageCalculation,  new[] { RuleSet.Timing.AfterDamageCalc });
            Register(EngineEvent.EndOfDamageStep,      new[] { RuleSet.Timing.EndOfDamageStep, RuleSet.Timing.OpenGameState });

            Register(EngineEvent.CardDestroyed,        new[] { RuleSet.Timing.OnDestroyed });
            Register(EngineEvent.CardSentToGY,         new[] { RuleSet.Timing.OnSentToGY });
            Register(EngineEvent.CardBanished,         new[] { RuleSet.Timing.OnBanished });

            Register(EngineEvent.PhaseStart,           new[] { RuleSet.Timing.OnPhaseStart, RuleSet.Timing.OpenGameState });
            Register(EngineEvent.PhaseEnd,             new[] { RuleSet.Timing.OnPhaseEnd, RuleSet.Timing.OpenGameState });

            Register(EngineEvent.ChainLinkResolved,    new[] { RuleSet.Timing.OnChainLinkResolved, RuleSet.Timing.OpenGameState });
        }

        /// <summary>Replace the mapping for an event.</summary>
        public void Register(EngineEvent e, RuleSet.Timing[] timings)
        {
            _map[e] = timings ?? new RuleSet.Timing[0];
        }

        /// <summary>Append timings to an existing event (dedupes).</summary>
        public void Append(EngineEvent e, RuleSet.Timing[] timings)
        {
            if (timings == null || timings.Length == 0) return;

            RuleSet.Timing[] existing;
            if (!_map.TryGetValue(e, out existing)) { Register(e, timings); return; }

            var list = new List<RuleSet.Timing>(existing);
            for (int i = 0; i < timings.Length; i++)
            {
                var t = timings[i];
                if (!list.Contains(t)) list.Add(t);
            }
            _map[e] = list.ToArray();
        }

        /// <summary>Get the timing sequence for an event (empty array if none).</summary>
        public RuleSet.Timing[] GetTimingsFor(EngineEvent e)
        {
            RuleSet.Timing[] arr;
            if (_map.TryGetValue(e, out arr)) return arr;
            return new RuleSet.Timing[0];
        }
    }

    // // If your RuleSet doesn't yet declare Timing, here’s a minimal set.
    // // Remove this partial enum if you already have one defined elsewhere.
    // public partial class RuleSet
    // {
    //     public enum Timing
    //     {
    //         OpenGameState,
    //
    //         // Summon
    //         OnSummonSuccess,
    //
    //         // Battle flow
    //         OnAttackDeclared,
    //         OnBattleStepStart,
    //         OnDamageStepStart,
    //         BeforeDamageCalc,
    //         DuringDamageCalc,
    //         AfterDamageCalc,
    //         EndOfDamageStep,
    //
    //         // Lifecycle
    //         OnDestroyed,
    //         OnSentToGY,
    //         OnBanished,
    //         OnPhaseStart,
    //         OnPhaseEnd,
    //         OnChainLinkResolved
    //     }
    // }
}
