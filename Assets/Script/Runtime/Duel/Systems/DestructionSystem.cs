// DestructionSystem.cs
// Handles destruction by battle/effect with indestructible and replacement hooks.

using System;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Foundation;

namespace YGO.Duel.Systems
{

    /// <summary>Hook to prevent destruction ("indestructible" style).</summary>
    public interface IDestructionShield
    {
        /// <summary>Return true if destruction is prevented; put a short reason for logs (optional).</summary>
        bool Prevent(Card card, DestroyReason reason, object source, out string why);
    }

    /// <summary>Hook to replace destruction ("if this would be destroyed, do X instead").</summary>
    public interface IDestructionReplacement
    {
        /// <summary>Return true if you fully handled the destruction (e.g., banish instead, return to hand, etc.).</summary>
        bool TryReplace(Card card, DestroyReason reason, object source, DestructionSystem sys, out string info);
    }

    public sealed class DestructionSystem
    {
        private readonly BoardManager _board;
        private readonly DuelLogger _logger;
        private readonly EventBus _bus;

        // Optional hooks; you can register multiple as your effect layer grows.
        private readonly System.Collections.Generic.List<IDestructionShield> _shields = new(8);
        private readonly System.Collections.Generic.List<IDestructionReplacement> _replacements = new(8);

        public event Action<Card, DestroyReason, string> OnDestroyed;

        public DestructionSystem(BoardManager board, DuelLogger logger, EventBus bus = null)
        {
            _board  = board  ?? throw new ArgumentNullException(nameof(board));
            _logger = logger ?? new DuelLogger();
            _bus    = bus;
        }

        public void RegisterShield(IDestructionShield s)             { if (s != null && !_shields.Contains(s)) _shields.Add(s); }
        public void UnregisterShield(IDestructionShield s)           { if (s != null) _shields.Remove(s); }
        public void RegisterReplacement(IDestructionReplacement r)   { if (r != null && !_replacements.Contains(r)) _replacements.Add(r); }
        public void UnregisterReplacement(IDestructionReplacement r) { if (r != null) _replacements.Remove(r); }

        /// <summary>Destroy a card; returns false if prevented or not found. 'source' is optional effect/battler.</summary>
        public bool TryDestroy(Card card, DestroyReason reason, object source, out string error)
        {
            error = "";
            if (card == null) { error = "Null card"; return false; }

            // 1) Prevent (indestructible)
            foreach (var s in _shields)
            {
                if (s.Prevent(card, reason, source, out var why))
                {
                    _logger.LogText("Destroy.Prevented", $"Destruction prevented",
                        data: $"card={card.Def?.cardName}; reason={reason}; why={why}", source: s.GetType().Name);
                    return false;
                }
            }

            // 2) Replace
            foreach (var r in _replacements)
            {
                if (r.TryReplace(card, reason, source, this, out var info))
                {
                    _logger.LogText("Destroy.Replaced", $"Destruction replaced",
                        data: $"card={card.Def?.cardName}; reason={reason}; {info}", source: r.GetType().Name);
                    return true;
                }
            }

            // 3) Default: send to owner's GY (YGO rule)
            var ownerSeat = card.Owner;
            RemoveFromAllKnownZones(_board, card); // from whichever side currently holds it

            var gy = _board.Zones[(int)ownerSeat].Graveyard;
            gy.Add(card);
            card.CurrentZone = BoardManager.CardZone.Graveyard;
            card.ZoneIndex   = -1;

            _logger.LogText("Destroy.ToGY", $"Destroyed → GY",
                data: $"card={card.Def?.cardName}; owner=P{(ownerSeat==BoardManager.Seat.P1?1:2)}; reason={reason}", source: nameof(DestructionSystem));

            OnDestroyed?.Invoke(card, reason, source?.ToString() ?? "");

            if (ServiceLocator.TryGet<EventBus>(out var bus) && bus != null)
            {
                // 'former controller' is the controller before moving to GY
                var former = card.Controller;
                bus.RaiseCardDestroyed(card, DestroyReason.Effect, former);
                // Optional: also raise a movement event (field -> owner GY)
                var from = new BoardManager.ZoneId(former, BoardManager.CardZone.Field);
                var to   = new BoardManager.ZoneId(card.Owner, BoardManager.CardZone.Graveyard);
                bus.RaiseCardMoved(card, new ZoneMove(from, to));
            }

            return true;
        }

        // ---------------- helpers ----------------

        private static void RemoveFromAllKnownZones(BoardManager board, Card card)
        {
            if (board == null || card == null) return;

            // Try controller first, then owner (in case control had switched)
            TryRemove(board, card.Controller, card);
            if (card.Controller != card.Owner) TryRemove(board, card.Owner, card);
        }

        private static void TryRemove(BoardManager board, BoardManager.Seat seat, Card card)
        {
            var z = board.Zones[(int)seat];

            // Hand (list-backed)
            z.Hand.Remove(card);

            // Single-slot: Monster / S/T / Field / Pendulum
            foreach (var mz in z.Monsters)
            {
                if (ReferenceEquals(mz.Top(), card)) { mz.RemoveTop(); return; }
                var fld = mz.GetType().GetField("Card");
                if (fld != null && ReferenceEquals(fld.GetValue(mz), card)) { fld.SetValue(mz, null); return; }
            }

            foreach (var st in z.SpellsTraps)
            {
                if (ReferenceEquals(st.Top(), card)) { st.RemoveTop(); return; }
                var fld = st.GetType().GetField("Card");
                if (fld != null && ReferenceEquals(fld.GetValue(st), card)) { fld.SetValue(st, null); return; }
            }

            if (z.Field != null)
            {
                if (ReferenceEquals(z.Field.Top(), card)) { z.Field.RemoveTop(); return; }
                var fld = z.Field.GetType().GetField("Card");
                if (fld != null && ReferenceEquals(fld.GetValue(z.Field), card)) { fld.SetValue(z.Field, null); return; }
            }

            if (z.Pendulum != null)
            {
                foreach (var pz in z.Pendulum)
                {
                    if (ReferenceEquals(pz.Top(), card)) { pz.RemoveTop(); return; }
                    var fld = pz.GetType().GetField("Card");
                    if (fld != null && ReferenceEquals(fld.GetValue(pz), card)) { fld.SetValue(pz, null); return; }
                }
            }
        }
    }
}
