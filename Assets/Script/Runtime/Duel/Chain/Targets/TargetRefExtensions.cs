using System;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Chain.YGO.Duel.Chain;
using YGO.Duel.Foundation;

namespace YGO.Duel.Chain
{
    public static class TargetRefExtensions
    {
        /// Best-effort seat (for player targets or card owner/controller).
        public static BoardManager.Seat SeatOrDefault(this ITargetRef tr, BoardManager.Seat fallback = BoardManager.Seat.P1)
        {
            if (tr == null) return fallback;
            if (tr.Raw is Card c) return c.Controller;

            // If the Id encodes "P1"/"P2", parse it (optional convenience)
            if (!string.IsNullOrEmpty(tr.Id))
            {
                if (tr.Id.Equals("P1", StringComparison.OrdinalIgnoreCase)) return BoardManager.Seat.P1;
                if (tr.Id.Equals("P2", StringComparison.OrdinalIgnoreCase)) return BoardManager.Seat.P2;
            }
            return fallback;
        }

        /// True if this ref points to a player (by convention: Id "P1"/"P2" and no Card Raw)
        public static bool IsPlayer(this ITargetRef tr)
            => tr != null && (tr.Raw == null) && (tr.Id == "P1" || tr.Id == "P2");

        /// Try to resolve to a runtime card (compatible with older call-sites)
        public static bool TryResolveCard(this ITargetRef tr, BoardManager board, out Card card)
        {
            card = null;
            if (tr == null || board == null) return false;

            if (tr.Raw is Card rc) { card = rc; return true; }

            if (ServiceLocator.TryGet<ICardIndex>(out var index) && index != null)
            {
                card = index.Find(tr.Id);
                if (card != null) return true;
            }

            foreach (var c in board.AllCards())
            {
                if (c == null) continue;
                if (string.Equals(c.InstanceId, tr.Id, StringComparison.Ordinal))
                { card = c; return true; }
            }
            return false;
        }

        /// Short “Describe()” equivalent for legacy code
        public static string Describe(this ITargetRef tr) => tr?.DebugName ?? "(null)";
    }
}