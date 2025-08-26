using System.Collections.Generic;
using UnityEngine;
using YGO.Duel.Board;

public interface IAvatarLocator
{
    Transform GetAttackOrigin(BoardManager.Seat seat);
}

// optional registry interface (implemented by the service)
public interface IAvatarRegistry
{
    void Register(BoardManager.Seat seat, Transform attackOrigin);
    void Unregister(BoardManager.Seat seat);
}

public sealed class AvatarLocatorService : IAvatarLocator, IAvatarRegistry
{
    private readonly Dictionary<BoardManager.Seat, Transform> _bySeat = new();

    public Transform GetAttackOrigin(BoardManager.Seat seat)
        => _bySeat.TryGetValue(seat, out var t) ? t : null;

    public void Register(BoardManager.Seat seat, Transform attackOrigin)
    {
        if (attackOrigin) _bySeat[seat] = attackOrigin;
    }

    public void Unregister(BoardManager.Seat seat)
    {
        _bySeat.Remove(seat);
    }
}