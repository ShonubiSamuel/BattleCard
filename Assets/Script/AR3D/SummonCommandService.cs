// using YGO.Duel.Board;
// using YGO.Duel.Cards;
// using YGO.Duel.Foundation;
// using YGO.Duel.Runtime;
// using YGO.Duel.Runtime.Actions;
//
//
// public interface ISummonCommandService
// {
//     /// <summary>Enqueue a Normal Summon (face-up ATK) into MZ[slot]. Returns false + error if enqueue failed.</summary>
//     bool TryNormalSummon(Card card, BoardManager.Seat seat, int mzIndex, out string error);
//
//     /// <summary>Enqueue a Set Monster (face-down DEF) into MZ[slot]. Returns false + error if enqueue failed.</summary>
//     bool TrySetMonster(Card card, BoardManager.Seat seat, int mzIndex, out string error);
// }
// public sealed class SummonCommandService : ISummonCommandService
// {
//     private readonly ActionQueue _queue;
//     private readonly TurnManager _turns;
//     private readonly ICardIndex  _index;
//     private readonly DuelLogger  _log;
//
//     public SummonCommandService(ActionQueue q, TurnManager t, ICardIndex idx, DuelLogger log = null)
//     {
//         _queue = q; _turns = t; _index = idx; _log = log ?? new DuelLogger();
//     }
//
//     public bool TryNormalSummon(Card card, BoardManager.Seat seat, int mzIndex, out string error)
//     {
//         error = "";
//         if (card == null || _queue == null || _turns == null) { error = "Services missing"; return false; }
//
//         var id = _index != null ? _index.GetId(card) : card.InstanceId;
//         var a  = ActionFactory.NormalSummon(seat, _turns, id, mzIndex);
//         if (_queue.Enqueue(a, out error))
//         {
//             _log?.LogText("Summon.Enqueue.NS", $"NS {card.Name} -> MZ[{mzIndex}] P{(seat==BoardManager.Seat.P1?1:2)}",
//                 source: nameof(SummonCommandService));
//             return true;
//         }
//         return false;
//     }
//
//     public bool TrySetMonster(Card card, BoardManager.Seat seat, int mzIndex, out string error)
//     {
//         error = "";
//         if (card == null || _queue == null || _turns == null) { error = "Services missing"; return false; }
//
//         var id = _index != null ? _index.GetId(card) : card.InstanceId;
//         var a  = ActionFactory.SetToMonster(seat, _turns, id, mzIndex);
//         if (_queue.Enqueue(a, out error))
//         {
//             _log?.LogText("Summon.Enqueue.SetM", $"Set {card.Name} -> MZ[{mzIndex}] P{(seat==BoardManager.Seat.P1?1:2)}",
//                 source: nameof(SummonCommandService));
//             return true;
//         }
//         return false;
//     }
// }