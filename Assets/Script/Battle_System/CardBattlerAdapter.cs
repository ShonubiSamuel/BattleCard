// CardBattlerAdapter.cs
// Adapts a runtime Card to the IBattler interface required by the battle system.

using System;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Foundation;
using YGO.Duel.Battle;    // IBattler, BattlePosition
using YGO.Duel.Zones;     // if you need zone helpers

namespace YGO.Duel.Battle
{
    /// <summary>
    /// Lightweight adapter so a Card can participate in battles without forcing Card to implement IBattler.
    /// Keeps responsibilities decoupled and lets BattleManager work with any IBattler.
    /// </summary>
    public sealed class CardBattlerAdapter : IBattler
    {
        private readonly Card _card;

        // Local fallbacks if you don't have a PositionManager or other systems available.
        private bool _canAttackThisTurn = true;
        private bool _hasAttackedThisTurn = false;

        public CardBattlerAdapter(Card card)
        {
            _card = card ?? throw new ArgumentNullException(nameof(card));
        }

        // ---------- Identity / controller ----------

        public string Name => _card.Def?.cardName ?? "(Card)";
        public BoardManager.Seat Controller => _card.Controller;

        // ---------- Status flags ----------

        public bool IsOnField => _card.IsOnField;
        public bool IsFaceUp  => _card.IsFaceUp;

        // If you have a system tracking target-prevention effects, query it here.
        public bool IsAttackTargetable => true;

        // Ex: effects like “can attack directly”; fold your effect layer here (default false).
        public bool IsDirectAttackAllowed => false;

        // Ex: “piercing” battle damage (ATK vs DEF deals LP); hook your effect layer (default false).
        public bool HasPiercing => false;

        // ---------- Stats ----------

        public int ATK => _card.CurrentATK;
        public int DEF => _card.CurrentDEF;

        // ---------- Position ----------

        public BattlePosition Position
        {
            get => _card.Position == CardBattlePosition.Attack ? BattlePosition.Attack : BattlePosition.Defense;
            set
            {
                // Prefer going through a PositionManager so “once per turn” and flip rules are enforced.
                if (ServiceLocator.TryGet<PositionManager>(out var pos) && pos != null)
                {
                    // Make face-up when the battle system sets a position mid-combat.
                    // If your game needs face-down DEF to persist here, pass 'false' instead.
                    pos.RequestPositionChange(_card, value, faceUp: true, out _);
                }
                else
                {
                    // Fallback: set directly on the Card
                    _card.SetPosition(value == BattlePosition.Attack ? CardBattlePosition.Attack : CardBattlePosition.Defense, faceUp: true);
                }
            }
        }

        // ---------- Turn-scoped attack flags ----------

        public bool CanAttackThisTurn
        {
            get
            {
                if (ServiceLocator.TryGet<PositionManager>(out var pos) && pos != null)
                    return pos.CanAttackThisTurn(_card);
                return _canAttackThisTurn;
            }
            set
            {
                if (ServiceLocator.TryGet<PositionManager>(out var pos) && pos != null)
                {
                    pos.SetCanAttackThisTurn(_card, value); // add this helper in PositionManager if you haven’t
                }
                else
                {
                    _canAttackThisTurn = value;
                }
            }
        }

        public bool HasAttackedThisTurn
        {
            get
            {
                if (ServiceLocator.TryGet<PositionManager>(out var pos) && pos != null)
                    return pos.HasAttackedThisTurn(_card);
                return _hasAttackedThisTurn;
            }
            set
            {
                if (ServiceLocator.TryGet<PositionManager>(out var pos) && pos != null)
                {
                    if (value) pos.MarkAttackUsed(_card);
                    else       pos.ClearAttackUsed(_card); // add this small helper if missing
                }
                else
                {
                    _hasAttackedThisTurn = value;
                }
            }
        }

        // ---------- Core actions ----------

        public void DestroyByBattle()
        {
            // Typical YGO rule: destroyed card is sent to its OWNER’s GY.
            SendToGraveyard(sendToOwnerGY: true, reason: "DestroyByBattle");
        }

        public void SendToGraveyard()
        {
            SendToGraveyard(sendToOwnerGY: true, reason: "SendToGY");
        }

        public void InflictBattleDamage(int amount, BoardManager.Seat playerDamaged)
        {
            if (amount <= 0) return;

            if (!ServiceLocator.TryGet<BoardManager>(out var board) || board == null) return;

            var ps = board.Players[(int)playerDamaged];
            var prev = ps.LifePoints;
            ps.LifePoints = Math.Max(0, ps.LifePoints - amount);

            if (ServiceLocator.TryGet<DuelLogger>(out var logger) && logger != null)
            {
                logger.LogText("Battle.Damage", $"P{(playerDamaged==BoardManager.Seat.P1?1:2)} takes {amount}",
                    data: $"from={Name}; lp:{prev}->{ps.LifePoints}", source: nameof(CardBattlerAdapter));
            }

            // // Optional: raise an event on your EventBus if you implemented one
            // if (ServiceLocator.TryGet<YGO.Duel.State.EventBus>(out var bus) && bus != null)
            // {
            //     bus.RaiseLPChanged(playerDamaged, prev, ps.LifePoints, source: Name);
            // }
        }

        public void AfterDamageStep()
        {
            // Hook for card-specific post-damage logic. If you have an effect system,
            // you can dispatch here. Default is no-op.
        }

        // ---------- helpers ----------

        private void SendToGraveyard(bool sendToOwnerGY, string reason)
        {
            if (!ServiceLocator.TryGet<BoardManager>(out var board) || board == null) return;

            var ownerSeat = _card.Owner;
            var controllerSeat = _card.Controller;

            // Build a 'from' ZoneId (best-effort)
            var from = new BoardManager.ZoneId(controllerSeat, BoardManager.CardZone.Monster, _card.ZoneIndex);

            RemoveFromAllKnownZones(board, controllerSeat, _card);
            if (controllerSeat != ownerSeat)
                RemoveFromAllKnownZones(board, ownerSeat, _card);

            // Place into OWNER's GY
            var to   = new BoardManager.ZoneId(ownerSeat, BoardManager.CardZone.Graveyard, -1);
            var gy   = board.Zones[(int)ownerSeat].Graveyard;
            gy.Add(_card);
            _card.CurrentZone = BoardManager.CardZone.Graveyard;
            _card.ZoneIndex   = -1;

            if (ServiceLocator.TryGet<EventBus>(out var bus) && bus != null)
            {
                // Optional: also raise a Destroy event (reason = Battle)
                bus.RaiseCardDestroyed(_card, DestroyReason.Battle, controllerSeat);
                // ✅ authoritative movement for visuals
                bus.RaiseCardMoved(_card, new ZoneMove(from, to));
            }

            if (ServiceLocator.TryGet<DuelLogger>(out var logger) && logger != null)
            {
                logger.LogText("Battle.MoveToGY", $"{Name} → Owner GY",
                    data: $"owner=P{(ownerSeat==BoardManager.Seat.P1?1:2)}; reason={reason}",
                    source: nameof(CardBattlerAdapter));
            }
        }


        private static void RemoveFromAllKnownZones(BoardManager board, BoardManager.Seat seat, Card card)
        {
            var z = board.Zones[(int)seat];

            // Hand
            TryInvokeRemove(z.Hand, card);

            // Monsters
            foreach (var mz in z.Monsters)
            {
                if (IsSameCard(mz.Top(), card)) { mz.RemoveTop(); break; }
                // legacy shape support (if a field exists): 
                var fld = mz.GetType().GetField("Card");
                if (fld != null && IsSameCard(fld.GetValue(mz) as Card, card)) { fld.SetValue(mz, null); break; }
            }

            // Spells/Traps
            foreach (var st in z.SpellsTraps)
            {
                if (IsSameCard(st.Top(), card)) { st.RemoveTop(); break; }
                var fld = st.GetType().GetField("Card");
                if (fld != null && IsSameCard(fld.GetValue(st) as Card, card)) { fld.SetValue(st, null); break; }
            }

            // Field
            if (z.Field != null)
            {
                if (IsSameCard(z.Field.Top(), card)) z.Field.RemoveTop();
                var fld = z.Field.GetType().GetField("Card");
                if (fld != null && IsSameCard(fld.GetValue(z.Field) as Card, card)) fld.SetValue(z.Field, null);
            }

            // Pendulum
            if (z.Pendulum != null)
            {
                for (int i = 0; i < z.Pendulum.Length; i++)
                {
                    var pz = z.Pendulum[i];
                    if (IsSameCard(pz.Top(), card)) { pz.RemoveTop(); break; }
                    var fld = pz.GetType().GetField("Card");
                    if (fld != null && IsSameCard(fld.GetValue(pz) as Card, card)) { fld.SetValue(pz, null); break; }
                }
            }
        }

        private static void TryInvokeRemove(object zone, Card card)
        {
            if (zone == null || card == null) return;

            var m = zone.GetType().GetMethod("Remove", new[] { typeof(Card) });
            if (m != null) { m.Invoke(zone, new object[] { card }); return; }

            var rawProp = zone.GetType().GetProperty("RawList");
            if (rawProp != null)
            {
                if (rawProp.GetValue(zone) is System.Collections.IList list && list.Contains(card))
                    list.Remove(card);
            }
        }

        private static bool IsSameCard(Card a, Card b)
            => !(a is null) && !(b is null) && ReferenceEquals(a, b) || (a?.InstanceId == b?.InstanceId);
        
        // CardBattlerAdapter.cs  — add inside the class
        public YGO.Duel.Cards.Card RuntimeCard => _card;
    }
}
