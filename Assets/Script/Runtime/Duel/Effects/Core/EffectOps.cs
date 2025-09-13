using System.Collections.Generic;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Foundation;

public static class EffectOps
{
    public static void DestroyCards(BoardManager board, DuelLogger log, IEnumerable<Card> cards)
    {
        if (board == null || cards == null) return;
        foreach (var c in cards) TryDestroy(board, log, c);
    }

    public static bool TryDestroy(BoardManager board, DuelLogger log, Card c)
    {
        if (board == null || c == null) return false;

        var ok = board.DestroyCard(c, DestroyReason.Effect, c.Controller);
        if (ok) log?.LogText("Effect.Destroy", $"Destroyed {c.Name}", source: nameof(EffectOps));
        return ok;
    }
}