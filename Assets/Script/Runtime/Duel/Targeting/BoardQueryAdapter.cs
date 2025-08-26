using YGO.Duel.Battle;
using YGO.Duel.Board;

namespace Script.Board_zones
{
    public sealed class BoardQueryAdapter : IBoardQuery
    {
        private readonly BoardManager _board;
        public BoardQueryAdapter(BoardManager board) { _board = board; }

        public bool OpponentControlsAnyMonsters(BoardManager.Seat seat)
        {
            var opp = BoardManager.OpponentOf(seat);
            var mz  = _board.Zones[(int)opp].Monsters;
            for (int i = 0; i < mz.Length; i++)
                if (mz[i].Top() != null) return true;
            return false;
        }
    }
}