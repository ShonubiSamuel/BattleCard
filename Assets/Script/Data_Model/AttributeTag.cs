// AttributeTag.cs
using UnityEngine;

namespace YGO.Duel.Data
{
    [CreateAssetMenu(fileName = "AttributeTag", menuName = "YGO/Data/Attribute Tag", order = 11)]
    public sealed class AttributeTag : ScriptableObject
    {
        [Tooltip("e.g., LIGHT, DARK, FIRE, WATER, WIND, EARTH, DIVINE")]
        public string displayName;

        [Tooltip("Optional color hint for UI.")]
        public Color color = Color.white;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name.Replace("AttributeTag", "").Trim();
        }
#endif
    }
}