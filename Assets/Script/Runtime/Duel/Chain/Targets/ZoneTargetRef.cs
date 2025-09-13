using YGO.Duel.Board;
using YGO.Duel.Chain.YGO.Duel.Chain;

namespace YGO.Duel.Chain
{
    /// Target reference for a specific zone slot (e.g., P1.MZ[2])
    public sealed class ZoneTargetRef : ITargetRef
    {
        public string Id { get; }
        public string DebugName { get; }
        public object Raw { get; private set; }

        private readonly BoardManager _board;
        private readonly BoardManager.Seat _seat;
        private readonly BoardManager.CardZone _zone;
        private readonly int _index;

        public ZoneTargetRef(BoardManager board, BoardManager.Seat seat, BoardManager.CardZone zone, int index)
        {
            _board = board; _seat = seat; _zone = zone; _index = index;
            Id = $"{seat}:{zone}:{index}";
            DebugName = $"P{(seat==BoardManager.Seat.P1?1:2)}.{zone}[{index}]";
        }

        public bool IsStillValid()
        {
            // Example policy: valid if slot still exists; some effects require “occupied”.
            var zones = _board.Zones[(int)_seat];
            object top = null;
            switch (_zone)
            {
                case BoardManager.CardZone.Monster:     top = zones.Monsters[_index].Top(); break;
                case BoardManager.CardZone.SpellTrap:   top = zones.SpellsTraps[_index].Top(); break;
                default: return true; // adjust per your needs
            }
            Raw = top;
            return true; // caller can also inspect Raw if they need “occupied-only” semantics
        }
    }
}