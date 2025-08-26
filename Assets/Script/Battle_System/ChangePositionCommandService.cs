// // ChangePositionCommandService.cs (new)
// using YGO.Duel.Board;
// using YGO.Duel.Battle;
// using YGO.Duel.Foundation;
// using YGO.Duel.Runtime;
// using YGO.Duel.Runtime.Actions;
// using Card = YGO.Duel.Cards.Card;
//
// // IChangePositionCommandService.cs (new)
// using YGO.Duel.Battle;
// using YGO.Duel.Cards;
// using YGO.Duel.Board;
//
// public interface IChangePositionCommandService
// {
//     bool TryChangePosition(Card card, BattlePosition to, bool faceUp, out string error);
// }
//
// public sealed class ChangePositionCommandService : IChangePositionCommandService
// {
//     private readonly ActionQueue _queue;
//     private readonly TurnManager _turns;
//     private readonly ICardIndex  _index;
//     private readonly DuelLogger  _log;
//
//     public ChangePositionCommandService(ActionQueue q, TurnManager t, ICardIndex idx, DuelLogger log)
//     { _queue = q; _turns = t; _index = idx; _log = log ?? new DuelLogger(); }
//
//     public bool TryChangePosition(Card card, BattlePosition to, bool faceUp, out string error)
//     {
//         error = "";
//         if (card == null || _turns == null || _queue == null) { error = "system unavailable"; return false; }
//         var id = (_index != null) ? _index.GetId(card) : card?.InstanceId;
//         if (string.IsNullOrEmpty(id)) { error = "id missing"; return false; }
//
//         var a = ActionFactory.ChangePosition(card.Controller, _turns, id, to == BattlePosition.Attack, faceUp);
//         if (_queue.Enqueue(a, out var err)) return true;
//
//         error = err;
//         _log.LogText("Cmd.ChangePos.Fail", err, source: nameof(ChangePositionCommandService));
//         return false;
//     }
// }