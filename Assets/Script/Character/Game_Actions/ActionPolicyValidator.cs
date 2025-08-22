// HumanController.cs
// Translates player intentions (UI) into validated GameActions and enqueues them.

using System;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Runtime.Actions;
using YGO.Duel.Runtime.Actions;  // for ActionFactory / GameActionCodec (if you use them)
using Card = YGO.Duel.Cards.Card; // alias the canonical runtime card

namespace YGO.Duel.Runtime
{
    public sealed class ActionPolicyValidator : IGameActionValidator
{
    private readonly BoardManager _board;
    private readonly TurnManager _turns;
    private readonly RuleSet _rules;

    public ActionPolicyValidator(BoardManager board, TurnManager turns, RuleSet rules)
    {
        _board = board; _turns = turns; _rules = rules;
    }

    public bool Validate(GameAction action, out string reason)
    {
        reason = "";

        if (action == null) { reason = "Null action"; return false; }
        if (action.turnNumber != _turns.TurnNumber || action.phase != _turns.CurrentPhase)
        { reason = "Stale snapshot"; return false; }

        // Only allow the seat that claims to act (auth/ownership goes here)
        if (action.seat != _turns.CurrentPlayer && action.Type is ActionType.EndPhase or ActionType.EndTurn or ActionType.NormalSummon)
        { reason = "Not your turn"; return false; }

        switch (action.Type)
        {
            case ActionType.EndPhase:
                // e.g., chain must be empty (if you model chain state here)
                return true;

            case ActionType.EndTurn:
                return _turns.CurrentPhase == RuleSet.Phase.End;

            case ActionType.NormalSummon:
            {
                // Example: ask RuleSet with adapters
                var adapters = new PlayerRuleAdapters(_board, _turns, action.seat);
                int assumedLevel = 4; // replace with real runtime level if encoded in action
                bool ok = _rules.CanNormalSummon(adapters.Player, adapters.State, adapters.Board, assumedLevel);
                if (!ok) { reason = "Cannot Normal Summon now"; return false; }
                return true;
            }

            default:
                return true;
        }
    }

    public sealed class PlayerRuleAdapters
    {
        public readonly RuleSet.IRulePlayer Player;
        public readonly RuleSet.IRuleBoard Board;
        public readonly RuleSet.IRuleDuelState State;

        public PlayerRuleAdapters(BoardManager b, TurnManager t, BoardManager.Seat seat)
        {
            Player = new PlayerAdapter(b, t, seat);
            Board  = new BoardAdapter(b, seat);
            State  = new StateAdapter(t);
        }

        private sealed class PlayerAdapter : RuleSet.IRulePlayer
        {
            private readonly BoardManager _b; private readonly TurnManager _t; private readonly BoardManager.Seat _s;
            public PlayerAdapter(BoardManager b, TurnManager t, BoardManager.Seat s) { _b=b; _t=t; _s=s; }
            public bool NormalSummonUsedThisTurn { get => _b.Players[(int)_s].NormalSummonUsedThisTurn; set => _b.Players[(int)_s].NormalSummonUsedThisTurn = value; }
            public bool IsTurnPlayer => _t.CurrentPlayer == _s;
        }

        private sealed class BoardAdapter : RuleSet.IRuleBoard
        {
            private readonly BoardManager _b; private readonly BoardManager.Seat _s;
            public BoardAdapter(BoardManager b, BoardManager.Seat s) { _b=b; _s=s; }
            public bool HasFreeMonsterZone(RuleSet.IRulePlayer _) => _b.HasFreeMonsterZone(_s);
            public int CountTributableMonsters(RuleSet.IRulePlayer _) => _b.CountTributableMonsters(_s);
        }

        private sealed class StateAdapter : RuleSet.IRuleDuelState
        {
            private readonly TurnManager _t;
            public StateAdapter(TurnManager t) { _t=t; }
            public RuleSet.Phase CurrentPhase => _t.CurrentPhase;
            public int TurnNumber => _t.TurnNumber;
            public RuleSet.IRulePlayer CurrentPlayer => null;
            public bool IsChainEmpty => true; // integrate chain manager if needed
        }
    }

}

}
