// CounterTag.cs
using UnityEngine;

namespace YGO.Duel.Data
{
    [CreateAssetMenu(fileName = "CounterTag", menuName = "YGO/Cards/Tags/Counter Type", order = 13)]
    public sealed class CounterTag : ScriptableObject
    {
        [Tooltip("Stable id (e.g., 'SPELL', 'VENOM').")]
        public string id;
        public string displayName;
        public Color  tint = Color.cyan;
    }
}