// CardDatabase.cs
using System.Collections.Generic;
using UnityEngine;

namespace YGO.Duel.Data
{
    [CreateAssetMenu(fileName = "CardDatabase", menuName = "YGO/Data/Card Database", order = 2)]
    public sealed class CardDatabase : ScriptableObject
    {
        [Tooltip("Drag all CardDefinition assets here, or populate via Addressables at runtime.")]
        public List<CardDefinition> cards = new List<CardDefinition>();

        private Dictionary<string, CardDefinition> _byId;
        private Dictionary<int, CardDefinition> _byPasscode;

        private void OnEnable() { RebuildIndex(); }

        public void RebuildIndex()
        {
            _byId = new Dictionary<string, CardDefinition>();
            _byPasscode = new Dictionary<int, CardDefinition>();

            foreach (var c in cards)
            {
                if (c == null) continue;

                var id = string.IsNullOrWhiteSpace(c.cardId) ? c.name : c.cardId;
                if (!_byId.ContainsKey(id)) _byId.Add(id, c);

                if (c.passcode > 0 && !_byPasscode.ContainsKey(c.passcode))
                    _byPasscode.Add(c.passcode, c);
            }
        }

        public bool TryGetById(string id, out CardDefinition def)
        {
            def = null;
            return !string.IsNullOrEmpty(id) && _byId != null && _byId.TryGetValue(id, out def);
        }

        public bool TryGetByPasscode(int code, out CardDefinition def)
        {
            def = null;
            return code > 0 && _byPasscode != null && _byPasscode.TryGetValue(code, out def);
        }

        public IEnumerable<CardDefinition> All => cards;
    }
}