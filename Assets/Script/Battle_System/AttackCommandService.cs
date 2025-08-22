using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Runtime;
using YGO.Duel.Runtime.Actions;
using Card = YGO.Duel.Cards.Card;


public interface IAttackCommandService
{
    /// Try to declare an attack (target null => direct).
    bool TryDeclareAttack(YGO.Duel.Cards.Card attacker, YGO.Duel.Cards.Card targetOrNull);
}


public sealed class AttackCommandService : IAttackCommandService
{
    private readonly ActionQueue _queue;
    private readonly TurnManager _turns;
    private readonly DuelLogger  _log;
    private readonly ICardIndex  _index;

    public AttackCommandService(ActionQueue queue, TurnManager turns, DuelLogger log, ICardIndex index)
    {
        _queue  = queue  ?? throw new System.ArgumentNullException(nameof(queue));
        _turns  = turns  ?? throw new System.ArgumentNullException(nameof(turns));
        _log    = log    ?? new DuelLogger();
        _index  = index; // optional, fallback to InstanceId
    }

    public bool TryDeclareAttack(Card attacker, Card targetOrNull)
    {
        Debug.Log("TryDeclareAttack");
        if (attacker == null) return false;
        if (_turns.CurrentPhase != RuleSet.Phase.Battle) return false;

        var attackerId = ResolveId(attacker);
        var targetId   = targetOrNull != null ? ResolveId(targetOrNull) : null;

        var action = ActionFactory.DeclareAttack(_turns.CurrentPlayer, _turns, attackerId, targetId);
        if (_queue.Enqueue(action, out var err)) return true;

        _log?.LogText("Attack.Reject", err, source: nameof(AttackCommandService));
        return false;
    }

    private string ResolveId(Card c)
    {
        if (c == null) return "";
        if (_index != null)
        {
            var id = _index.GetId(c);
            if (!string.IsNullOrEmpty(id)) return id;
        }
        return c.InstanceId;
    }
}