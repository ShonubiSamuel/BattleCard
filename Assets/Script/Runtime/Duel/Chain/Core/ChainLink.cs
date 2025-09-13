using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using YGO.Duel.Board;
using YGO.Duel.Chain.YGO.Duel.Chain;
using YGO.Duel.Rules;

namespace YGO.Duel.Chain
{
    public sealed class ChainLink
    {
        private static readonly ReadOnlyCollection<ITargetRef> EmptyTargets =
            new List<ITargetRef>(0).AsReadOnly();
        private static readonly ReadOnlyCollection<CostReceipt> EmptyCosts =
            new List<CostReceipt>(0).AsReadOnly();

        public readonly int Index;                         // 1-based (top = newest)
        public readonly BoardManager.Seat Activator;
        public readonly object Source;                     // card or system
        public readonly string SourceId;                   // stable id (card instance id, etc.)
        public readonly bool IsCardSource;                 // convenience
        public readonly IEffectHandle Effect;
        public readonly RuleSet.SpellSpeed Speed;
        public readonly RuleSet.Timing ActivationTiming;   // captured at add time
        public readonly string ActivationSummary;          // baked short text for logs/UI
        public readonly IReadOnlyList<ITargetRef> Targets; // locked at activation
        public readonly IReadOnlyList<CostReceipt> Costs;  // costs paid to create link
        public readonly DateTime TimeAddedUtc;

        public ChainLink(
            int index,
            BoardManager.Seat activator,
            object source,
            string sourceId,
            bool isCardSource,
            IEffectHandle effect,
            RuleSet.SpellSpeed speed,
            RuleSet.Timing timing,
            string activationSummary,
            List<ITargetRef> targets,
            List<CostReceipt> costs)
        {
            Index = index;
            Activator = activator;
            Source = source;
            SourceId = sourceId ?? string.Empty;
            IsCardSource = isCardSource;
            Effect = effect;
            Speed = speed;
            ActivationTiming = timing;
            ActivationSummary = activationSummary ?? (effect?.EffectName ?? "Effect");
            Targets = (targets != null && targets.Count > 0) ? targets.AsReadOnly() : EmptyTargets;
            Costs   = (costs   != null && costs.Count   > 0) ? costs.AsReadOnly()   : EmptyCosts;
            TimeAddedUtc = DateTime.UtcNow;
        }

        public override string ToString()
            => $"#{Index} {ActivationSummary} by {Activator} [{Speed}] Targets={Targets.Count}";
    }
}