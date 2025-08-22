// MonsterRegistry.cs
// Central map: Card -> MonsterRuntime. Creates/tears down runtimes on summons/moves.
// Register in ServiceLocator from DuelInstaller.

using System.Collections.Generic;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Battle;
using YGO.Duel.Cards;
using YGO.Duel.Foundation;

namespace YGO.Duel.Battle
{
    [DefaultExecutionOrder(-104)]
    public sealed class MonsterRegistry : MonoBehaviour
    {
        private readonly Dictionary<Card, MonsterRuntime> _map = new(256);

        private EventBus _bus;
        private DuelLogger _log;

        private void Awake()
        {
            ServiceLocator.TryGet(out _bus);
            ServiceLocator.TryGet(out _log);
            // make discoverable
            ServiceLocator.Register(this, overwrite: true);
        }

        private void OnEnable()
        {
            if (_bus == null) return;
            _bus.OnSummoned  += HandleSummoned;
            _bus.OnCardMoved += HandleCardMoved;
        }

        private void OnDisable()
        {
            if (_bus == null) return;
            _bus.OnSummoned  -= HandleSummoned;
            _bus.OnCardMoved -= HandleCardMoved;
        }

        // -------- Public API --------

        public bool TryGet(Card c, out MonsterRuntime rt) => _map.TryGetValue(c, out rt);

        public void AttachActor(Card c, Transform actorRoot)
        {
            if (c == null || actorRoot == null) return;
            if (_map.TryGetValue(c, out var rt)) rt.ActorRoot = actorRoot;
        }

        public void DetachActor(Card c, Transform actorRootIfAny = null)
        {
            if (c == null) return;
            if (_map.TryGetValue(c, out var rt) && rt.ActorRoot == actorRootIfAny) rt.ActorRoot = null;
        }

        // -------- Event reactions --------

        private void HandleSummoned(object sender, SummonEvent e)
        {
            // Guarantee a runtime on face-up monster summon
            var card = e.Card;
            if (card == null || !card.Def?.IsMonster == true) return;
            CreateOrRefresh(card, e.Controller, e.ZoneIndex);
        }

        private void HandleCardMoved(object sender, CardMovedEvent e)
        {
            var card = e.Card;
            if (card == null) return;

            var to = e.Move.To;

            // Entering Monster Zone face-up -> ensure runtime
            if (to.Kind == BoardManager.CardZone.Monster)
            {
                if (card.Def?.IsMonster == true && card.IsFaceUp)
                {
                    CreateOrRefresh(card, to.Seat, to.Index);
                }
                else
                {
                    // face-down in MZ or non-monster in MZ => remove runtime if it exists
                    RemoveIfExists(card, reason: "Not face-up monster in MZ");
                }
                return;
            }

            // Leaving the field or going to non-MZ areas => remove runtime
            if (to.Kind == BoardManager.CardZone.Graveyard ||
                to.Kind == BoardManager.CardZone.Banished ||
                to.Kind == BoardManager.CardZone.Hand ||
                to.Kind == BoardManager.CardZone.Deck ||
                to.Kind == BoardManager.CardZone.ExtraDeck ||
                to.Kind == BoardManager.CardZone.SpellTrap ||
                to.Kind == BoardManager.CardZone.Field)
            {
                RemoveIfExists(card, reason: $"Moved to {to.Kind}");
            }
        }

        // -------- Internals --------

        private void CreateOrRefresh(Card c, BoardManager.Seat seat, int mzIndex)
        {
            if (_map.TryGetValue(c, out var rt))
            {
                rt.ZoneIndex = mzIndex;
                return;
            }

            rt = new MonsterRuntime(c, mzIndex);
            _map[c] = rt;
            _log?.LogText("MonsterRegistry.Add", rt.ToString(), source: nameof(MonsterRegistry));
        }

        private void RemoveIfExists(Card c, string reason)
        {
            if (_map.Remove(c, out var rt))
            {
                _log?.LogText("MonsterRegistry.Remove", $"{rt} — {reason}", source: nameof(MonsterRegistry));
            }
        }
    }
}
