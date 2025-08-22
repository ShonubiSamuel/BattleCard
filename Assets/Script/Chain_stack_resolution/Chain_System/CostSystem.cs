// CostSystem.cs
// Paying costs (LP, discard, release/tribute, banish-as-cost). Built for clarity + easy testing.

using System;
using System.Collections.Generic;
using System.Linq;
using YGO.Duel.Board;
using Card = YGO.Duel.Cards.Card;

namespace YGO.Duel.Chain
{
    // ---------------- Costs model ----------------

    public interface ICost
    {
        /// <summary>Return false if the cost cannot be paid right now (no mutation).</summary>
        bool CanPay(CostContext ctx, out string reason);

        /// <summary>Perform the payment (mutates board/LP). Return a receipt for logs/replay.</summary>
        bool TryPay(CostContext ctx, out CostReceipt receipt, out string error);

        /// <summary>Short human-friendly description (e.g., "Pay 1000 LP").</summary>
        string Describe();
    }

    public sealed class PayLifeCost : ICost
    {
        public readonly int Amount;

        public PayLifeCost(int amount) { Amount = Math.Max(0, amount); }

        public string Describe() => $"Pay {Amount} LP";

        public bool CanPay(CostContext ctx, out string reason)
        {
            var ps = ctx.Board.Players[(int)ctx.Activator];
            if (ps.LifePoints >= Amount) { reason = ""; return true; }
            reason = "Not enough LP."; return false;
        }

        public bool TryPay(CostContext ctx, out CostReceipt receipt, out string error)
        {
            if (!CanPay(ctx, out error)) { receipt = null; return false; }
            var ps = ctx.Board.Players[(int)ctx.Activator];
            ps.LifePoints -= Amount;

            receipt = new CostReceipt { Description = Describe(), Amount = Amount, CardNames = new() };
            return true;
        }
    }

    /// <summary>Discard specific cards from hand as cost.</summary>
    public sealed class DiscardCost : ICost
    {
        public readonly List<Card> Cards;

        public DiscardCost(IEnumerable<Card> cards) => Cards = cards?.ToList() ?? new();

        public string Describe() => $"Discard {Cards.Count} card(s)";

        public bool CanPay(CostContext ctx, out string reason)
        {
            var hand = ctx.Board.Zones[(int)ctx.Activator].Hand;
            if (Cards.Count > 0 && Cards.TrueForAll(hand.Contains))
            {
                reason = ""; return true;
            }
            reason = "Required discard cards not in hand."; return false;
        }

        public bool TryPay(CostContext ctx, out CostReceipt receipt, out string error)
        {
            if (!CanPay(ctx, out error)) { receipt = null; return false; }

            var hand = ctx.Board.Zones[(int)ctx.Activator].Hand;
            var gy   = ctx.Board.Zones[(int)ctx.Activator].Graveyard;

            foreach (var c in Cards)
            {
                if (hand.Remove(c))
                    gy.Add(c);
            }

            receipt = new CostReceipt
            {
                Description = Describe(),
                Amount = Cards.Count,
                CardNames = Cards.Select(c => c.Name).ToList()
            };
            return true;
        }
    }

    /// <summary>Release/tribute monsters you already chose (pass the exact cards).</summary>
    public sealed class ReleaseTributeCost : ICost
    {
        public readonly List<Card> Monsters;

        public ReleaseTributeCost(IEnumerable<Card> monsters) => Monsters = monsters?.ToList() ?? new();

        public string Describe() => $"Tribute {Monsters.Count} monster(s)";

        public bool CanPay(CostContext ctx, out string reason)
        {
            // Validate they sit in own monster zones
            var myMZ = ctx.Board.Zones[(int)ctx.Activator].Monsters;
            foreach (var m in Monsters)
            {
                bool found = false;
                for (int i = 0; i < myMZ.Length; i++)
                    if (myMZ[i].Card == m) { found = true; break; }

                if (!found) { reason = "Chosen tribute not on your field."; return false; }
            }
            reason = ""; return true;
        }

        public bool TryPay(CostContext ctx, out CostReceipt receipt, out string error)
        {
            if (!CanPay(ctx, out error)) { receipt = null; return false; }

            var myMZ = ctx.Board.Zones[(int)ctx.Activator].Monsters;
            var gy   = ctx.Board.Zones[(int)ctx.Activator].Graveyard;

            foreach (var m in Monsters)
            {
                for (int i = 0; i < myMZ.Length; i++)
                {
                    if (myMZ[i].Card == m)
                    {
                        myMZ[i].Card = null;
                        gy.Add(m);
                        break;
                    }
                }
            }

            receipt = new CostReceipt
            {
                Description = Describe(),
                Amount = Monsters.Count,
                CardNames = Monsters.Select(c => c.Name).ToList()
            };
            return true;
        }
    }

    /// <summary>Banish specific cards as a cost (from hand/field—pass exact references).</summary>
    public sealed class BanishAsCost : ICost
    {
        public readonly List<Card> Cards;
        public readonly bool FaceDown;

        public BanishAsCost(IEnumerable<Card> cards, bool faceDown = false)
        {
            Cards = cards?.ToList() ?? new();
            FaceDown = faceDown;
        }

        public string Describe() => $"Banish {Cards.Count} card(s){(FaceDown ? " face-down" : "")}";

        public bool CanPay(CostContext ctx, out string reason)
        {
            // Verify cards exist and are controlled by activator (hand or field).
            if (Cards.Count == 0) { reason = "No cards specified to banish."; return false; }

            foreach (var c in Cards)
            {
                if (!IsOwnedByPlayer(ctx.Board, ctx.Activator, c))
                {
                    reason = "Cannot banish: at least one card is not controlled by you.";
                    return false;
                }
            }
            reason = ""; return true;
        }

        public bool TryPay(CostContext ctx, out CostReceipt receipt, out string error)
        {
            if (!CanPay(ctx, out error)) { receipt = null; return false; }

            var ban = ctx.Board.Zones[(int)ctx.Activator].Banished;

            foreach (var c in Cards)
            {
                // Remove from wherever it is (hand, MZ, STZ)
                RemoveFromAllZones(ctx.Board, ctx.Activator, c);
                ban.Add(c, FaceDown);
            }

            receipt = new CostReceipt
            {
                Description = Describe(),
                Amount = Cards.Count,
                CardNames = Cards.Select(c => c.Name).ToList()
            };
            return true;
        }

        private static bool IsOwnedByPlayer(BoardManager board, BoardManager.Seat seat, Card c)
        {
            var z = board.Zones[(int)seat];

            if (z.Hand.Contains(c)) return true;

            foreach (var mz in z.Monsters) if (mz.Top() == c || mz.Card == c) return true;       // Top() for new API; Card for old
            foreach (var st in z.SpellsTraps) if (st.Top() == c || st.Card == c) return true;

            if (z.Field != null && (z.Field.Top() == c || z.Field.Card == c)) return true;
            if (z.Pendulum != null)
            {
                if (z.Pendulum[0].Top() == c || z.Pendulum[0].Card == c) return true;
                if (z.Pendulum[1].Top() == c || z.Pendulum[1].Card == c) return true;
            }

            return false;
        }


        private static void RemoveFromAllZones(BoardManager board, BoardManager.Seat seat, Card c)
        {
            var z = board.Zones[(int)seat];

            // Hand
            if (z.Hand.Remove(c)) return;

            // Monsters
            for (int i = 0; i < z.Monsters.Length; i++)
                if (z.Monsters[i].Card == c) { z.Monsters[i].Card = null; return; }

            // Spells/Traps
            for (int i = 0; i < z.SpellsTraps.Length; i++)
                if (z.SpellsTraps[i].Card == c) { z.SpellsTraps[i].Card = null; return; }

            // Field
            if (z.Field != null && z.Field.Card == c) { z.Field.Card = null; return; }

            // Pendulum
            if (z.Pendulum != null)
            {
                if (z.Pendulum[0].Card == c) { z.Pendulum[0].Card = null; return; }
                if (z.Pendulum[1].Card == c) { z.Pendulum[1].Card = null; return; }
            }
        }
    }

    // ---------------- System orchestrator ----------------

    public sealed class CostSystem
    {
        /// <summary>Pre-check whether *all* costs of an effect could be paid right now (no mutation).</summary>
        public bool CanPayAll(IEffectHandle effect, CostContext ctx, out string reason)
        {
            reason = "";
            foreach (var cost in effect.GetCosts(ctx))
            {
                if (!cost.CanPay(ctx, out reason))
                    return false;
            }
            return true;
        }

        /// <summary>Pay *all* costs (mutates LP/zones). Returns receipts list for logging/replays.</summary>
        public bool TryPayAll(IEffectHandle effect, CostContext ctx, out List<CostReceipt> receipts, out string error)
        {
            receipts = new List<CostReceipt>();
            error = "";

            foreach (var cost in effect.GetCosts(ctx))
            {
                if (!cost.TryPay(ctx, out var receipt, out error))
                {
                    // Rollback is domain-specific; for now, on failure we stop and report error.
                    receipts.Clear();
                    return false;
                }
                if (receipt != null) receipts.Add(receipt);
            }
            return true;
        }
    }
}
