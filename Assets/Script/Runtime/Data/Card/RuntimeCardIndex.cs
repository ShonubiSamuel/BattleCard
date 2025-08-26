using System.Collections.Generic;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Foundation;

public sealed class RuntimeCardIndex : ICardIndex
{
    private readonly BoardManager _board;
    private readonly Dictionary<string, Card> _byId = new(512);

    public RuntimeCardIndex(BoardManager board, bool autoRebuild = true)
    {
        _board = board ?? throw new System.ArgumentNullException(nameof(board));
        if (autoRebuild) TryRebuild();
    }

    private bool SafeToEnumerate()
        => _board != null
           && _board.IsBuilt
           && _board.Zones[(int)BoardManager.Seat.P1] != null
           && _board.Zones[(int)BoardManager.Seat.P2] != null;

    public void TryRebuild()
    {
        if (SafeToEnumerate()) Rebuild();
    }

    public void Rebuild()
    {
        _byId.Clear();
        foreach (var c in _board.AllCards())
            if (c != null && !string.IsNullOrEmpty(c.InstanceId))
                _byId[c.InstanceId] = c;
    }

    public Card Find(string runtimeId)
        => (runtimeId != null && _byId.TryGetValue(runtimeId, out var c)) ? c : null;

    public string GetId(Card card) => card?.InstanceId ?? "";

    public void Register(Card card)
    {
        if (card == null) return;
        var id = card.InstanceId;
        if (!string.IsNullOrEmpty(id)) _byId[id] = card;
    }

    public bool Unregister(Card card)
    {
        if (card == null) return false;
        var id = card.InstanceId;
        if (string.IsNullOrEmpty(id)) return false;
        if (_byId.TryGetValue(id, out var existing) && !ReferenceEquals(existing, card))
            return false;
        return _byId.Remove(id);
    }
}