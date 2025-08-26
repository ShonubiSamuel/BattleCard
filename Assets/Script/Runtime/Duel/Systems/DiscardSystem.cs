// DiscardSystem.cs
// Forced discards and hand size enforcement (end phase).

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Foundation;

namespace YGO.Duel.Systems
{
    public sealed class DiscardSystem
    {
        private readonly BoardManager _board;
        private readonly DuelLogger _logger;
        private readonly DeterministicRng _rng;

        public event Action<BoardManager.Seat, IReadOnlyList<Card>, string> OnDiscarded;

        public DiscardSystem(BoardManager board, DuelLogger logger, DeterministicRng rng = null)
        {
            _board  = board  ?? throw new ArgumentNullException(nameof(board));
            _logger = logger ?? new DuelLogger();
            _rng    = rng;
        }

        /// <summary>Force a player to discard N cards. If chooser is null, discards at random (deterministic if rng provided).</summary>
        public bool ForceDiscard(BoardManager.Seat seat, int count, Func<IReadOnlyList<Card>, IReadOnlyList<Card>> chooser = null, string reason = "Effect")
        {
            if (count <= 0) return true;

            var hand = _board.Zones[(int)seat].Hand;
            if (hand.Count <= 0)
            {
                _logger.LogText("Discard.None", "No cards to discard", data:$"seat=P{(seat==BoardManager.Seat.P1?1:2)}");
                return true;
            }

            var candidates = hand.RawList.ToList();

            // Choose cards
            List<Card> chosen;
            if (chooser != null)
            {
                var pick = chooser(candidates) ?? Array.Empty<Card>();
                chosen = pick.Take(count).ToList();
            }
            else
            {
                var list = candidates.ToList();
                if (_rng != null) _rng.Shuffle(list);
                else
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        int j = UnityEngine.Random.Range(i, list.Count);
                        (list[i], list[j]) = (list[j], list[i]);
                    }
                }
                chosen = list.Take(Mathf.Min(count, list.Count)).ToList();
            }

            if (chosen.Count == 0) return true;

            // Move to GY
            var gy = _board.Zones[(int)seat].Graveyard;
            foreach (var c in chosen)
            {
                if (hand.Remove(c))
                {
                    gy.Add(c);
                    c.CurrentZone = BoardManager.CardZone.Graveyard;
                    c.ZoneIndex   = -1;
                }
            }

            _logger.LogText("Discard.Done", $"Discarded {chosen.Count}", data:$"seat=P{(seat==BoardManager.Seat.P1?1:2)}; reason={reason}", source:nameof(DiscardSystem));
            OnDiscarded?.Invoke(seat, chosen, reason);

            // EventBus: per-card Hand -> GY moves
            if (ServiceLocator.TryGet<EventBus>(out var bus) && bus != null)
            {
                var from = new BoardManager.ZoneId(seat, BoardManager.CardZone.Hand);
                var to   = new BoardManager.ZoneId(seat, BoardManager.CardZone.Graveyard);
                foreach (var c in chosen)
                    bus.RaiseCardMoved(c, new ZoneMove(from, to));
            }

            return true;
        }

        /// <summary>Apply a maximum hand size; discards excess (prefers chooser; otherwise random).</summary>
        public bool EnforceHandSize(BoardManager.Seat seat, int maxHand, Func<IReadOnlyList<Card>, IReadOnlyList<Card>> chooser = null)
        {
            maxHand = Mathf.Max(0, maxHand);
            var hand = _board.Zones[(int)seat].Hand;
            int excess = hand.Count - maxHand;
            if (excess <= 0) return true;
            return ForceDiscard(seat, excess, chooser, reason: "HandLimit");
        }
    }
}
