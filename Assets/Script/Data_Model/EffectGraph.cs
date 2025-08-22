// EffectGraph.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using YGO.Duel.Rules;

namespace YGO.Duel.Data
{
    [CreateAssetMenu(fileName = "EffectGraph", menuName = "YGO/Data/Effect Graph", order = 1)]
    public sealed class EffectGraph : ScriptableObject
    {
        [Tooltip("Logical effects this card can activate. Each effect becomes one activation handle.")]
        public List<EffectEntry> effects = new List<EffectEntry>();

        [Serializable]
        public sealed class EffectEntry
        {
            public string effectId = "E0";
            public string displayName = "Effect";

            [Tooltip("Spell speed used by RuleSet timing checks.")]
            public RuleSet.SpellSpeed spellSpeed = RuleSet.SpellSpeed.One;

            [Tooltip("Simple gating (once per turn/duel). Leave Unlimited for default.")]
            public ActivationLimit activationLimit = ActivationLimit.Unlimited;

            [SerializeReference] public List<INode> nodes = new List<INode>(); // ordered steps
        }

        public enum ActivationLimit { Unlimited, OncePerTurn, OncePerDuel }

        // ---------------- Node model (interpreted by your runtime) ----------------

        public interface INode { string Label { get; } }

        [Serializable]
        public sealed class ConditionNode : INode
        {
            public string label = "Condition";
            [Tooltip("Simple key/value predicate, e.g., 'HasFreeMonsterZone=true'. Extend as needed.")]
            public string key;
            public string value;
            public string Label => label;
        }

        [Serializable]
        public sealed class CostNode : INode
        {
            public string label = "Cost";
            [Tooltip("Type of cost: PayLP, Discard, Tribute, Banish")]
            public string costType;
            public int amount; // LP, number of cards, etc.
            public string Label => label;
        }

        [Serializable]
        public sealed class TargetNode : INode
        {
            public string label = "Target";
            [Tooltip("Target query, e.g., 'Monster:Opponent:ATK>=1500:OnField'")]
            public string query;
            public int maxTargets = 1;
            public string Label => label;
        }

        [Serializable]
        public sealed class OperationNode : INode
        {
            public string label = "Operation";
            [Tooltip("Op code, e.g., 'DestroyTargets', 'Draw', 'InflictDamage', 'ChangePosition'")]
            public string op;
            public int intParam;
            public string strParam;
            public string Label => label;
        }
    }
}
