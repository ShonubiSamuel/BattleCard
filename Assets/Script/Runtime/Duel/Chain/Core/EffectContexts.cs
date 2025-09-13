using YGO.Duel.Board;
using YGO.Duel.Chain.YGO.Duel.Chain;
using YGO.Duel.Rules;

namespace YGO.Duel.Chain
{
    public readonly struct ConditionContext
    {
        public readonly BoardManager Board;
        public readonly BoardManager.Seat Activator;
        public readonly RuleSet.IRuleDuelState DuelState;
        public readonly RuleSet RuleSet;

        public ConditionContext(BoardManager board, BoardManager.Seat activator, RuleSet.IRuleDuelState state, RuleSet rules)
        { Board = board; Activator = activator; DuelState = state; RuleSet = rules; }
    }

    public readonly struct CostContext
    {
        public readonly BoardManager Board;
        public readonly BoardManager.Seat Activator;
        public readonly object Source;

        public CostContext(BoardManager board, BoardManager.Seat activator, object source)
        { Board = board; Activator = activator; Source = source; }
    }

    public readonly struct ResolveContext
    {
        public readonly BoardManager Board;
        public readonly BoardManager.Seat Activator;
        public readonly object Source; // card or system
        public readonly System.Collections.Generic.IReadOnlyList<ITargetRef> Targets;

        public ResolveContext(BoardManager board, BoardManager.Seat activator, object source, System.Collections.Generic.IReadOnlyList<ITargetRef> targets)
        { Board = board; Activator = activator; Source = source; Targets = targets; }
    }
}