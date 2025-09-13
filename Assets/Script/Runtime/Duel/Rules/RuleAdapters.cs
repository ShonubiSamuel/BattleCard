// RuleAdapters.cs
using YGO.Duel.Board;
using YGO.Duel.Rules;

namespace YGO.Duel.Runtime
{
    public static class RuleAdapters
    {
        /// Adapts TurnManager to RuleSet.IRuleDuelState.
        public readonly struct DuelStateAdapter : RuleSet.IRuleDuelState
        {
            private readonly TurnManager _tm;
            private readonly BoardManager _board;
            private readonly IChainState _chain;

            public DuelStateAdapter(TurnManager tm, BoardManager board = null, IChainState chain = null)
            {
                _tm = tm;
                _board = board;
                _chain = chain;
            }

            // If _tm is null, return conservative safe defaults.
            public RuleSet.Phase CurrentPhase => _tm != null ? _tm.CurrentPhase : RuleSet.Phase.Main1;
            public int TurnNumber             => _tm != null ? _tm.TurnNumber   : 0;

            public RuleSet.IRulePlayer CurrentPlayer
            {
                get
                {
                    var seat = _tm != null ? _tm.CurrentPlayer : BoardManager.Seat.P1;
                    var board = _board ?? (_tm?.GetBoardAdapter() is BoardAdapter ba ? ba.Board : null);
                    return new RulePlayerAdapter(seat, _tm, board);
                }
            }

            public bool IsChainEmpty => _chain?.IsChainEmpty ?? (_tm != null ? _tm.IsChainEmpty : true);
        }

        /// Adapts BoardManager to RuleSet.IRuleBoard.
        public readonly struct BoardAdapter : RuleSet.IRuleBoard
        {
            internal BoardManager Board { get; }
            public BoardAdapter(BoardManager board) { Board = board; }

            public bool HasFreeMonsterZone(RuleSet.IRulePlayer player)
            {
                if (Board == null) return false;
                var seat = (player as RulePlayerAdapter?)?.Seat ?? BoardManager.Seat.P1;
                return Board.HasFreeMonsterZone(seat);
            }

            public int CountTributableMonsters(RuleSet.IRulePlayer player)
            {
                if (Board == null) return 0;
                var seat = (player as RulePlayerAdapter?)?.Seat ?? BoardManager.Seat.P1;
                return Board.CountTributableMonsters(seat);
            }
        }

        /// Adapts the current player view (NormalSummon flag + "is turn player").
        public struct RulePlayerAdapter : RuleSet.IRulePlayer
        {
            internal BoardManager.Seat Seat { get; }
            private readonly TurnManager _tm;
            private readonly BoardManager _board;

            public RulePlayerAdapter(BoardManager.Seat seat, TurnManager tm, BoardManager board)
            {
                Seat = seat; _tm = tm; _board = board;
            }

            public bool NormalSummonUsedThisTurn
            {
                get
                {
                    if (_board == null) return false;
                    var p = _board.Players[(int)Seat];
                    return p != null && p.NormalSummonUsedThisTurn;
                }
                set
                {
                    if (_board == null) return;
                    var p = _board.Players[(int)Seat];
                    if (p != null) p.NormalSummonUsedThisTurn = value;
                }
            }

            public bool IsTurnPlayer => _tm != null && _tm.CurrentPlayer == Seat;
        }
    }
}