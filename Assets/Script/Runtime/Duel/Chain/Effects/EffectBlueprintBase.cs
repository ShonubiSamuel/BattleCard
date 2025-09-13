// Effects/EffectBlueprintBase.cs
using YGO.Duel.Rules;
using YGO.Duel.Cards;
using YGO.Duel.Chain;
using UnityEngine;

namespace YGO.Duel.Effects
{
    public abstract class EffectBlueprintBase : ScriptableObject
    {
        [Tooltip("Spell speed used for timing checks before building the handle.")]
        public RuleSet.SpellSpeed declaredSpeed = RuleSet.SpellSpeed.One;

        /// Build the runtime effect handle bound to a specific card instance.
        public abstract IEffectHandle BuildHandle(Card source, string effectId = "");
    }
}