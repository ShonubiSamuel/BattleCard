// EquipManager.cs
using System.Collections.Generic;
using YGO.Duel.Cards;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Systems;

namespace YGO.Duel.Effects
{
    public sealed class EquipManager
    {
        private readonly DuelLogger _log;
        private readonly EventBus _bus;
        private readonly Dictionary<Card, Card> _equipToHost = new(); // equip -> host
        private readonly Dictionary<Card, List<Card>> _hostToEquips = new(); // host -> equips

        public EquipManager(DuelLogger log, EventBus bus)
        {
            _log = log ?? new DuelLogger();
            _bus = bus ?? EventBus.Global;

            // If you want auto cleanup:
            _bus.OnCardMoved += HandleCardMoved;
            _bus.OnDestroyed += HandleDestroyed;
        }

        public bool TryEquip(Card equip, Card host, out string why)
        {
            why = "";
            if (equip == null || host == null) { why = "Null"; return false; }
            // Minimal legality: equip must be Spell with Equip subtype; both face-up and on field, same controller
            if (!(equip.Def?.IsSpell ?? false) || equip.Def.spellSubtype != YGO.Duel.Data.SpellSubtype.Equip)
            { why = "Not an Equip Spell"; return false; }
            if (!equip.IsOnField || !host.IsOnField) { why = "Not on field"; return false; }
            if (equip.Controller != host.Controller) { why = "Different controller"; return false; }

            _equipToHost[equip] = host;
            if (!_hostToEquips.TryGetValue(host, out var list)) { list = new List<Card>(); _hostToEquips[host] = list; }
            if (!list.Contains(equip)) list.Add(equip);

            _log.LogText("Equip.Bind", $"{equip.Name} -> {host.Name}", source:nameof(EquipManager));
            return true;
        }

        public bool IsEquippedTo(Card equip, Card host) => _equipToHost.TryGetValue(equip, out var h) && ReferenceEquals(h, host);

        public Card GetHost(Card equip) => _equipToHost.TryGetValue(equip, out var h) ? h : null;

        public IReadOnlyList<Card> GetEquips(Card host)
        {
            if (_hostToEquips.TryGetValue(host, out var list)) return list;
            return System.Array.Empty<Card>();
        }

        public void Unequip(Card equip)
        {
            if (!_equipToHost.TryGetValue(equip, out var host)) return;
            _equipToHost.Remove(equip);
            if (_hostToEquips.TryGetValue(host, out var list)) list.Remove(equip);
        }

        private void HandleCardMoved(object sender, CardMovedEvent e)
        {
            // If host left field, destroy its equips
            var host = e.Card;
            if (_hostToEquips.TryGetValue(host, out var equips) && equips.Count > 0)
            {
                // Copy because we'll mutate
                var copy = new List<Card>(equips);
                foreach (var eq in copy) DestroyEquip(eq);
            }
            // If an equip itself moved off field, drop mapping
            foreach (var kv in new List<KeyValuePair<Card, Card>>(_equipToHost))
            {
                if (kv.Key == e.Card) Unequip(kv.Key);
            }
        }

        private void HandleDestroyed(object sender, DestroyEvent e)
        {
            // Same idea—cleanup maps
            foreach (var kv in new List<KeyValuePair<Card, Card>>(_equipToHost))
                if (kv.Key == e.Card) Unequip(kv.Key);
            if (_hostToEquips.TryGetValue(e.Card, out var equips))
            {
                var copy = new List<Card>(equips);
                foreach (var eq in copy) DestroyEquip(eq);
            }
        }

        private void DestroyEquip(Card equip)
        {
            Unequip(equip);
            // Send to GY (use your destruction system)
            if (ServiceLocator.TryGet<DestructionSystem>(out var killer) && killer != null)
                killer.TryDestroy(equip, DestroyReason.Rule, equip.Controller, out string reason);
            _log.LogText("Equip.AutoDestroy", equip.Name, source:nameof(EquipManager));
        }
    }
}