// SummonCommandService.cs
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Runtime;
using YGO.Duel.Runtime.Actions;
using Card = YGO.Duel.Cards.Card;

public interface ISummonCommandService
{
    bool TryNormalSummon(Card c, BoardManager.Seat seat, int mzIndex, out string error);
    bool TrySetMonster(Card c, BoardManager.Seat seat, int mzIndex, out string error);
    bool TrySetSpellTrap(Card c, BoardManager.Seat seat, int stIndex, out string error);
}

public sealed class SummonCommandService : ISummonCommandService
{
    private readonly ActionQueue _queue;
    private readonly TurnManager _turns;
    private readonly DuelLogger  _log;
    private readonly ICardIndex  _index;

    public SummonCommandService()
    {
        ServiceLocator.TryGet(out _queue);
        ServiceLocator.TryGet(out _turns);
        ServiceLocator.TryGet(out _log);
        ServiceLocator.TryGet(out _index);
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

    public bool TryNormalSummon(Card c, BoardManager.Seat seat, int mzIndex, out string error)
    {
        error = "";
        if (c == null || _queue == null || _turns == null) { error = "Services missing"; return false; }

        var id = ResolveId(c);
        var a  = ActionFactory.NormalSummon(seat, _turns, id, mzIndex);
        if (_queue.Enqueue(a, out var err)) return true;

        error = err;
        _log?.LogText("SummonCmd.NS.Fail", err, source: nameof(SummonCommandService));
        return false;
    }

    public bool TrySetMonster(Card c, BoardManager.Seat seat, int mzIndex, out string error)
    {
        error = "";
        if (c == null || _queue == null || _turns == null) { error = "Services missing"; return false; }

        var id = ResolveId(c);
        var a  = ActionFactory.SetToMonster(seat, _turns, id, mzIndex);
        if (_queue.Enqueue(a, out var err)) return true;

        error = err;
        _log?.LogText("SummonCmd.SetM.Fail", err, source: nameof(SummonCommandService));
        return false;
    }

    public bool TrySetSpellTrap(Card c, BoardManager.Seat seat, int stIndex, out string error)
    {
        error = "";
        if (c == null || _queue == null || _turns == null) { error = "Services missing"; return false; }

        var id = ResolveId(c);
        var a  = ActionFactory.SetToST(seat, _turns, id, stIndex);
        if (_queue.Enqueue(a, out var err)) return true;

        error = err;
        _log?.LogText("SummonCmd.SetST.Fail", err, source: nameof(SummonCommandService));
        return false;
    }
}