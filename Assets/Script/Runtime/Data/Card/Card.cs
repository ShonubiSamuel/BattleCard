// Card.cs (runtime)
// Canonical runtime card type used everywhere.

using System;
using System.Collections.Generic;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Data;

namespace YGO.Duel.Cards
{
    public enum CardBattlePosition { Attack = 0, Defense = 1 }

    [Serializable]
    public sealed class Card
    {
        // ---- identity ----
        [SerializeField] private string _instanceId;
        public string InstanceId => _instanceId;

        // ---- static data ----
        public CardDefinition Def { get; private set; }

        // ---- ownership & control ----
        public BoardManager.Seat Owner      { get; private set; }
        public BoardManager.Seat Controller { get;  set; }

        // ---- board presence ----
        public BoardManager.CardZone CurrentZone { get; internal set; } = BoardManager.CardZone.Unknown;
        public int ZoneIndex { get; internal set; } = -1;

        // ---- state ----
        public bool IsFaceUp { get;  set; } = true;
        public CardBattlePosition Position { get; private set; } = CardBattlePosition.Attack;

        // Banish-display support
        public bool IsFaceDownBanished { get; internal set; }

        // Convenience (keeps older code happy)
        public string Name  => Def?.cardName ?? "(null)";
        public int    Level => Def != null ? Def.level : 0;
        // Back-compat helpers (many scripts expect these)
        public bool   IsMonsterRuntime => Def != null && Def.IsMonster;
        
        // Card.cs — inside class Card
        public int EnteredFieldTurn { get; internal set; } = 0;  // 0 = never
        public bool WasSetThisTurn { get; internal set; } = false;

        public void MarkEnteredField(int turnNumber, bool wasSet)
        {
            EnteredFieldTurn = turnNumber;
            WasSetThisTurn   = wasSet;
        }


        public bool IsOnField =>
            CurrentZone == BoardManager.CardZone.Monster ||
            CurrentZone == BoardManager.CardZone.SpellTrap ||
            CurrentZone == BoardManager.CardZone.Pendulum ||
            CurrentZone == BoardManager.CardZone.Field;

        public bool IsTributable => Def != null && Def.IsMonster && !Def.IsLink;

        // ---- counters ----
        [SerializeField] private List<CounterEntry> _counters = new();       // persisted
        [NonSerialized]  private Dictionary<CounterTag, int> _counterMap;    // runtime cache

        [Serializable]
        private struct CounterEntry { public CounterTag tag; public int count; }

        // ---- ctor ----
        public Card(CardDefinition def, BoardManager.Seat owner, string instanceId = null)
        {
            Def        = def ? def : throw new ArgumentNullException(nameof(def));
            Owner      = owner;
            Controller = owner;
            _instanceId = !string.IsNullOrEmpty(instanceId) ? instanceId : Guid.NewGuid().ToString("N");
            RebuildCounterMap();
        }

        // ---- counters API ----
        public int GetCounter(CounterTag tag)
        {
            if (!tag) return 0;
            EnsureMap();
            return _counterMap.TryGetValue(tag, out var n) ? n : 0;
        }

        public void AddCounter(CounterTag tag, int count = 1)
        {
            if (!tag || count == 0) return;
            EnsureMap();
            _counterMap.TryGetValue(tag, out var cur);
            var next = Mathf.Max(0, cur + count);
            _counterMap[tag] = next;
            SyncCounterList();
        }

        public void RemoveCounter(CounterTag tag, int count = 1) => AddCounter(tag, -Mathf.Abs(count));

        // ---- state transitions ----
        public void FlipFaceUp(bool faceUp = true) => IsFaceUp = faceUp;

        public void SetPosition(CardBattlePosition pos, bool faceUp = true)
        {
            Position = pos;
            IsFaceUp = faceUp;
        }

        public void SetController(BoardManager.Seat newController) => Controller = newController;

        // ---- stats (modifiers can hook here later) ----
        public int CurrentATK => Def?.baseATK     ?? 0;
        public int CurrentDEF => Def?.baseDEF ?? 0;

        public override string ToString()
            => $"{Def?.cardName ?? "(null)"} [{Def?.kind}] {CurrentZone} {(IsFaceUp ? (Position==CardBattlePosition.Attack? "ATK":"DEF") : "FD")}";

        // ---- internal ----
        private void EnsureMap() { if (_counterMap == null) RebuildCounterMap(); }

        private void RebuildCounterMap()
        {
            _counterMap = new Dictionary<CounterTag, int>(_counters?.Count ?? 0);
            if (_counters != null)
                foreach (var e in _counters) if (e.tag) _counterMap[e.tag] = Mathf.Max(0, e.count);
        }

        private void SyncCounterList()
        {
            _counters.Clear();
            foreach (var kv in _counterMap)
                _counters.Add(new CounterEntry { tag = kv.Key, count = Mathf.Max(0, kv.Value) });
        }
    }
}
