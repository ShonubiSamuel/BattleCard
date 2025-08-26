// TypeTag.cs
using UnityEngine;

namespace YGO.Duel.Data
{
    [CreateAssetMenu(fileName = "TypeTag", menuName = "YGO/Data/Type Tag (Monster Race)", order = 12)]
    public sealed class TypeTag : ScriptableObject
    {
        [Tooltip("Monster race/type, e.g., Dragon, Spellcaster, Warrior, Machine, etc.")]
        public string displayName;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name.Replace("TypeTag", "").Trim();
        }
#endif
    }
}