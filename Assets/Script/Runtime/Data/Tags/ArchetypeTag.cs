// ArchetypeTag.cs
using UnityEngine;

namespace YGO.Duel.Data
{
    [CreateAssetMenu(fileName = "ArchetypeTag", menuName = "YGO/Data/Archetype Tag", order = 10)]
    public sealed class ArchetypeTag : ScriptableObject
    {
        [Tooltip("Unique id/key, e.g., 'BlueEyes', 'ElementalHERO'.")]
        public string archetypeId;

        [Tooltip("Display name shown in UI.")]
        public string displayName;

        [TextArea] public string notes;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name.Replace("ArchetypeTag", "").Trim();
            if (string.IsNullOrWhiteSpace(archetypeId))
                archetypeId = displayName.Replace(" ", "");
        }
#endif
    }
}