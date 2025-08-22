using System;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Foundation;
using YGO.Duel.Runtime.Actions;

namespace YGO.Duel.Runtime
{
    // Shared shape for “agents” that request actions (human UI, AI, remote).
    public interface IPlayerAgent
    {
        BoardManager.Seat Seat { get; }
        bool RequestEndPhase();
        bool RequestPassPriority();
        bool RequestEndTurn();
        bool RequestNormalSummon(Card handMonster);
    }
    
    public interface IPlayerDirectory { IPlayerAgent Get(BoardManager.Seat seat); }

    public sealed class SimplePlayerDirectory : IPlayerDirectory
    {
        private readonly IPlayerAgent _p1, _p2;
        public SimplePlayerDirectory(IPlayerAgent p1, IPlayerAgent p2) { _p1=p1; _p2=p2; }
        public IPlayerAgent Get(BoardManager.Seat seat) => seat == BoardManager.Seat.P1 ? _p1 : _p2;
    }


    
    public sealed class PlayerActionHandler : IPlayerAgent
    {
        private readonly BoardManager _board;
        private readonly TurnManager _turns;
        private readonly DuelLogger _logger;
        private readonly ActionQueue _queue;

        public BoardManager.Seat Seat { get; }

        public PlayerActionHandler(BoardManager board, TurnManager turns, DuelLogger logger, ActionQueue queue, BoardManager.Seat seat)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _turns = turns ?? throw new ArgumentNullException(nameof(turns));
            _logger = logger ?? new DuelLogger();
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            Seat = seat;
        }

        private void Fill(GameAction a)
        {
            a.FillSnapshot(Seat, _turns); // seat, turnNumber, phase
        }

        public bool RequestEndPhase()
        {
            var a = new EndPhaseAction();
            Fill(a);
            return _queue.Enqueue(a, out _);
        }

        public bool RequestPassPriority()
        {
            var a = GameAction.PassPriority(Seat, _turns.TurnNumber, _turns.CurrentPhase);
            return _queue.Enqueue(a, out _);
        }

        public bool RequestEndTurn()
        {
            var a = GameAction.EndTurn(Seat, _turns.TurnNumber, _turns.CurrentPhase);
            return _queue.Enqueue(a, out _);
        }

        public bool RequestNormalSummon(Card handMonster)
        {
            if (handMonster == null) return false;

            // Find first free MZ (UI keeps selection; handler turns it into an action)
            int mzIndex = -1;
            var myMZ = _board.Zones[(int)Seat].Monsters;
            for (int i = 0; i < myMZ.Length; i++) if (myMZ[i].Top() == null) { mzIndex = i; break; }
            if (mzIndex < 0) return false;

            // Use a stable runtime GUID in real code; Name is placeholder here.
            var a = GameAction.NormalSummon(Seat, _turns.TurnNumber, _turns.CurrentPhase, handMonster.Name, mzIndex);
            return _queue.Enqueue(a, out _);
        }
    }

}