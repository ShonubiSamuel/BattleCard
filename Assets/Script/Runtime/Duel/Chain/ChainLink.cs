// ChainLink.cs
// Immutable snapshot of a single link in the chain: who activated, what effect, costs paid, targets locked.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // << for ReadOnlyCollection<T>
using YGO.Duel.Board;
using YGO.Duel.Rules;

namespace YGO.Duel.Chain
{
    public interface ITargetRef
    {
        string DebugName { get; }
        bool IsStillValid();
        object Raw { get; }
    }

    [Serializable]
    public sealed class CostReceipt
    {
        public string Description;
        public int Amount;
        public List<string> CardNames;

        public override string ToString()
        {
            // Avoid target-typed new() for older C# versions
            var list = CardNames ?? new List<string>();
            return $"{Description} (Amount={Amount}, Cards=[{string.Join(", ", list)}])";
        }
    }

    public sealed class ChainLink
    {
        // Provide a single static instance for “empty read-only list” so both sides have same type
        private static readonly ReadOnlyCollection<ITargetRef> EmptyTargets =
            new List<ITargetRef>(0).AsReadOnly();

        private static readonly ReadOnlyCollection<CostReceipt> EmptyCosts =
            new List<CostReceipt>(0).AsReadOnly();

        public readonly int Index;                         // 1-based (top = newest)
        public readonly BoardManager.Seat Activator;
        public readonly object Source;                     // card or skill that created the effect
        public readonly IEffectHandle Effect;
        public readonly RuleSet.SpellSpeed Speed;
        public readonly IReadOnlyList<ITargetRef> Targets; // locked at activation
        public readonly IReadOnlyList<CostReceipt> Costs;  // receipts of costs paid
        public readonly DateTime TimeAddedUtc;

        public ChainLink(
            int index,
            BoardManager.Seat activator,
            object source,
            IEffectHandle effect,
            RuleSet.SpellSpeed speed,
            List<ITargetRef> targets,
            List<CostReceipt> costs)
        {
            Index = index;
            Activator = activator;
            Source = source;
            Effect = effect;
            Speed = speed;

            // Use the same concrete type on both branches (ReadOnlyCollection<T>) to avoid CS0172
            Targets = (targets != null && targets.Count > 0) ? targets.AsReadOnly() : EmptyTargets;
            Costs   = (costs   != null && costs.Count   > 0) ? costs.AsReadOnly()   : EmptyCosts;

            TimeAddedUtc = DateTime.UtcNow;
        }

        public override string ToString()
            => $"#{Index} {Effect?.EffectName ?? "Effect"} by {Activator} [{Speed}] Targets={Targets.Count}";
    }

    // Effect contracts (unchanged)
    public interface IResolverAction { void Resolve(ResolveContext ctx); }

    public interface IEffectHandle
    {
        string EffectName { get; }
        RuleSet.SpellSpeed Speed { get; }
        bool CheckAdditionalConditions(ConditionContext ctx, out string reason);
        IEnumerable<ICost> GetCosts(CostContext ctx);
        IResolverAction BuildResolveAction(ResolveContext ctx);
    }

    public interface IOncePerTurn { bool ConsumedThisTurn { get; set; } }

    public readonly struct ConditionContext
    {
        public readonly BoardManager Board;
        public readonly BoardManager.Seat Activator;
        public readonly RuleSet.IRuleDuelState DuelState;
        public readonly RuleSet RuleSet;

        public ConditionContext(BoardManager board, BoardManager.Seat activator, RuleSet.IRuleDuelState state, RuleSet rules)
        {
            Board = board; Activator = activator; DuelState = state; RuleSet = rules;
        }
    }

    public readonly struct CostContext
    {
        public readonly BoardManager Board;
        public readonly BoardManager.Seat Activator;
        public readonly object Source;

        public CostContext(BoardManager board, BoardManager.Seat activator, object source)
        {
            Board = board; Activator = activator; Source = source;
        }
    }

    public readonly struct ResolveContext
    {
        public readonly BoardManager Board;
        public readonly BoardManager.Seat Activator;
        public readonly object Source;
        public readonly IReadOnlyList<ITargetRef> Targets;

        public ResolveContext(BoardManager board, BoardManager.Seat activator, object source, IReadOnlyList<ITargetRef> targets)
        {
            Board = board; Activator = activator; Source = source; Targets = targets;
        }
    }
}
