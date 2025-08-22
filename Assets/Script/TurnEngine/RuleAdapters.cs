// RuleAdapters.cs
// Small adapters that let RuleSet read game state without depending on your concrete classes.

using YGO.Duel.Board;
using YGO.Duel.Rules;

namespace YGO.Duel.Runtime
{
    public static class RuleAdapters
    {
        /// <summary>
        /// Adapts TurnManager to RuleSet.IRuleDuelState (phase, turn no., active player, chain empty).
        /// </summary>
        public readonly struct DuelStateAdapter : RuleSet.IRuleDuelState
        {
            private readonly TurnManager _tm;

            public DuelStateAdapter(TurnManager tm) { _tm = tm; }

            public RuleSet.Phase CurrentPhase => _tm.CurrentPhase;
            public int TurnNumber => _tm.TurnNumber;
            public RuleSet.IRulePlayer CurrentPlayer => new RulePlayerAdapter(_tm.CurrentPlayer, _tm, _tm.GetBoardAdapter() is BoardAdapter ba ? ba.Board : null);
            public bool IsChainEmpty => _tm.IsChainEmpty;
        }

        /// <summary>
        /// Adapts BoardManager to RuleSet.IRuleBoard (slot counts / tribute counts).
        /// </summary>
        public readonly struct BoardAdapter : RuleSet.IRuleBoard
        {
            internal BoardManager Board { get; }
            public BoardAdapter(BoardManager board) { Board = board; }

            public bool HasFreeMonsterZone(RuleSet.IRulePlayer player)
            {
                var seat = (player as RulePlayerAdapter?)?.Seat ?? BoardManager.Seat.P1;
                return Board.HasFreeMonsterZone(seat);
            }

            public int CountTributableMonsters(RuleSet.IRulePlayer player)
            {
                var seat = (player as RulePlayerAdapter?)?.Seat ?? BoardManager.Seat.P1;
                return Board.CountTributableMonsters(seat);
            }
        }

        /// <summary>
        /// Adapts the current player view (NormalSummon flag + "is turn player").
        /// </summary>
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
                get => _board.Players[(int)Seat].NormalSummonUsedThisTurn;
                set => _board.Players[(int)Seat].NormalSummonUsedThisTurn = value;
            }

            public bool IsTurnPlayer => _tm.CurrentPlayer == Seat;
        }
    }
}
