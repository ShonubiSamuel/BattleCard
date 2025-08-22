using System;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Foundation;

namespace YGO.Duel.Runtime.Actions
{
    internal static class ActionUtil
    {
        public static Card ResolveCard(ActionContext ctx, string id, BoardManager.Seat seatHint, out string error)
        {
            error = "";
            if (ctx.Board == null) { error = "Board not available"; return null; }
            if (string.IsNullOrEmpty(id)) { error = "Empty card id"; return null; }

            // 1) Prefer the runtime index (runtimeId)
            if (ServiceLocator.TryGet<ICardIndex>(out var index) && index != null)
            {
                var viaIndex = index.Find(id);
                if (viaIndex != null) return viaIndex;
            }

            // 2) Safety: direct instanceId scan (in case something wasn't registered)
            foreach (var c in ctx.Board.AllCards())
                if (string.Equals(c.InstanceId, id, StringComparison.Ordinal))
                    return c;

            // 3) Seat-scoped NAME fallback: current actor's Hand → field → GY/Banished
            if (seatHint == BoardManager.Seat.P1 || seatHint == BoardManager.Seat.P2)
            {
                var z = ctx.Board.Zones[(int)seatHint];

                // Hand list
                var handList = z.Hand.GetType().GetProperty("RawList")?.GetValue(z.Hand) as System.Collections.IEnumerable;
                if (handList != null)
                    foreach (Card c in handList)
                        if (string.Equals(c.Name, id, StringComparison.Ordinal)) return c;

                // Field tops
                foreach (var mz in z.Monsters)     { var t = mz.Top(); if (t != null && t.Name == id) return t; }
                foreach (var st in z.SpellsTraps)  { var t = st.Top(); if (t != null && t.Name == id) return t; }
                if (z.Pendulum != null)
                    foreach (var pz in z.Pendulum) { var t = pz.Top(); if (t != null && t.Name == id) return t; }
                if (z.Field != null) { var t = z.Field.Top(); if (t != null && t.Name == id) return t; }

                // GY / Banished lists
                foreach (var listZone in new YGO.Duel.Zones.IZone[] { z.Graveyard, z.Banished })
                {
                    var list = listZone.GetType().GetProperty("RawList")?.GetValue(listZone) as System.Collections.IEnumerable;
                    if (list == null) continue;
                    foreach (var item in list)
                        if (item is Card c && string.Equals(c.Name, id, StringComparison.Ordinal)) return c;
                }

            }

            // 4) Last-ditch global NAME fallback
            foreach (var c in ctx.Board.AllCards())
                if (string.Equals(c.Name, id, StringComparison.Ordinal)) return c;

            error = $"Card not found: {id}";
            return null;
        }

        // (FirstFreeMonsterZone / FirstFreeSTZone unchanged)
    }
}
