// ContinuousEffectService.cs
using System;
using System.Collections.Generic;
using YGO.Duel.Foundation;
using YGO.Duel.Cards;
using YGO.Duel.UI;

namespace YGO.Duel.Effects
{
    /// <summary>Manages continuous effects installed by source cards; provides aggregated stat modifiers.</summary>
    public sealed class ContinuousEffectService : ICardStatProvider
    {
        private readonly DuelLogger _log;
        private readonly EventBus _bus;

        // Source card -> list of installed effects
        private readonly Dictionary<Card, List<IContinuousEffect>> _bySource = new();
        private ICardStatProvider _iCardStatProviderImplementation;

        public ContinuousEffectService(DuelLogger logger, EventBus bus)
        {
            _log = logger ?? new DuelLogger();
            _bus = bus  ?? EventBus.Global;
        }

        // ---- Install/remove ----

        public void Install(Card source, IContinuousEffect effect)
        {
            if (source == null || effect == null) return;
            if (!_bySource.TryGetValue(source, out var list)) { list = new List<IContinuousEffect>(); _bySource[source] = list; }
            list.Add(effect);
            effect.OnInstall(_bus);
            _log.LogText("Continuous.Install", $"{source.Name}: {effect.GetType().Name}", source:nameof(ContinuousEffectService));
        }

        public void UninstallAll(Card source)
        {
            if (source == null) return;
            if (_bySource.TryGetValue(source, out var list))
            {
                foreach (var e in list) e.OnUninstall(_bus);
                list.Clear();
                _bySource.Remove(source);
            }
        }

        // Convenience for field effects
        public void InstallFieldLayer(Card fieldSource, IContinuousEffect effect) => Install(fieldSource, effect);

        // ---- ICardStatProvider (wrap base stats + layers) ----

        // You already register SimpleCardStatProvider(). We can replace it with this service
        // or chain them (base + modifiers). For simplicity, we compute from definition here.
        public int GetATK(Card c)
        {
            if (c == null || c.Def == null) return 0;
            int atk = c.Def.baseATK > 0 ? c.Def.baseATK : 0;
            foreach (var e in EnumerateAll()) foreach (var m in e.GetStatModifiers())
                atk += m.DeltaATK(c);
            return atk;
        }

        public int GetDEF(Card c)
        {
            if (c == null || c.Def == null) return 0;
            int def = c.Def.baseDEF > 0 ? c.Def.baseDEF : 0;
            foreach (var e in EnumerateAll()) foreach (var m in e.GetStatModifiers())
                def += m.DeltaDEF(c);
            return def;
        }

        private IEnumerable<IContinuousEffect> EnumerateAll()
        {
            foreach (var kv in _bySource)
                foreach (var e in kv.Value)
                    yield return e;
        }

        
        
        // ContinuousEffectService.cs (replace the backing field + ctor + the two methods)

        private readonly ICardStatProvider _baseProvider;   // <- instead of _iCardStatProviderImplementation

        public ContinuousEffectService(DuelLogger logger, EventBus bus, ICardStatProvider baseProvider = null)
        {
            _log = logger ?? new DuelLogger();
            _bus = bus  ?? EventBus.Global;

            // try DI or fall back to a simple built-in provider
            if (baseProvider != null) _baseProvider = baseProvider;
            else if (ServiceLocator.TryGet(out ICardStatProvider injected) && injected != null)
                _baseProvider = injected;
            else
                _baseProvider = new SimpleCardStatProvider();   // (see snippet below)
        }

// …

        public bool TryGetStats(Card card, out int atk, out int def, out int level, out string typeLine)
        {
            // 1) base provider numbers
            if (!_baseProvider.TryGetStats(card, out atk, out def, out level, out typeLine))
                return false;

            // 2) apply continuous layers
            foreach (var e in EnumerateAll())
            {
                foreach (var m in e.GetStatModifiers())
                {
                    atk += m.DeltaATK(card);
                    def += m.DeltaDEF(card);
                }
            }
            return true;
        }

        public string GetDisplayName(Card card)
        {
            // allow base provider to alias name; else default to card.Name
            var name = _baseProvider?.GetDisplayName(card);
            return string.IsNullOrEmpty(name) ? card?.Name ?? "(Card)" : name;
        }
    }
}