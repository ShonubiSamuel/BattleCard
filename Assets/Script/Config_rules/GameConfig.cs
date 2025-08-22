// GameConfig.cs
// Match-level configuration preset (ScriptableObject) + immutable runtime snapshot.

using System;
using UnityEngine;

namespace YGO.Duel.Foundation
{
    /// <summary>
    /// Scriptable preset for duel settings (LP, hand size, timers, zones).
    /// Create multiple assets for different modes (casual, ranked, custom).
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "YGO/Duel/Game Config", order = 0)]
    public sealed class GameConfig : ScriptableObject
    {
        public enum TurnOrderPolicy
        {
            FirstPlayerRandomCoinToss = 0,
            Player1AlwaysGoesFirst   = 1,
            Player2AlwaysGoesFirst   = 2,
            AskUI                    = 3, // Let a UI choose
        }

        public enum MasterRuleVersion
        {
            MR3 = 3, MR4 = 4, MR5 = 5
        }

        // ===== Serialized fields (Unity inspector) =====
        [Header("Core")]
        [Min(1)] public int startingLifePoints = 8000;

        [Range(1, 10)] public int startingHandSize = 5;

        [Tooltip("In modern formats the first-turn player does NOT draw on turn 1.")]
        public bool firstTurnPlayerDraws = false;

        [Tooltip("Conducting the Battle Phase on the very first turn.")]
        public bool firstTurnCanEnterBattlePhase = false;

        [Tooltip("Allow 'First Turn Kill' scenarios (rarely true in standard formats).")]
        public bool allowFTK = false;

        [Header("Turn Order")]
        public TurnOrderPolicy turnOrder = TurnOrderPolicy.FirstPlayerRandomCoinToss;

        [Header("Timer (0 = disabled)")]
        [Tooltip("Per-turn timer in seconds; 0 disables the turn timer.")]
        [Range(0, 1800)] public int turnTimerSeconds = 0;

        [Header("Board Layout")]
        [Range(1, 7)] public int maxMonsterZones = 5;
        [Range(1, 7)] public int maxSpellTrapZones = 5;
        public bool enablePendulumZones = true;
        public bool useFieldZone = true;

        [Header("Ruleset Versioning")]
        public MasterRuleVersion masterRule = MasterRuleVersion.MR5;

        [Header("Misc")]
        [Tooltip("Optional id/name for a Forbidden/Limited list to validate decks against.")]
        public string limitListId = "Default";

        [Tooltip("Print detailed boot logs to the console.")]
        public bool verboseLogging = true;

        // ===== PascalCase read-only properties for external scripts =====
        public int StartingLifePoints => startingLifePoints;
        public int StartingHandSize => startingHandSize;
        public bool FirstTurnPlayerDraws => firstTurnPlayerDraws;
        public bool FirstTurnCanEnterBattlePhase => firstTurnCanEnterBattlePhase;
        public bool AllowFTK => allowFTK;
        public TurnOrderPolicy TurnOrder => turnOrder;
        public int TurnTimerSeconds => turnTimerSeconds;
        public int MaxMonsterZones => maxMonsterZones;
        public int MaxSpellTrapZones => maxSpellTrapZones;
        public bool EnablePendulumZones => enablePendulumZones;
        public bool UseFieldZone => useFieldZone;
        public MasterRuleVersion MasterRuleVer => masterRule;
        public string LimitListId => limitListId;
        public bool VerboseLogging => verboseLogging;

        /// <summary>
        /// Build an immutable snapshot for runtime (safe to cache/share).
        /// </summary>
        public Runtime BuildRuntime()
        {
            return new Runtime(
                startingLifePoints,
                startingHandSize,
                firstTurnPlayerDraws,
                firstTurnCanEnterBattlePhase,
                allowFTK,
                turnOrder,
                turnTimerSeconds,
                maxMonsterZones,
                maxSpellTrapZones,
                enablePendulumZones,
                useFieldZone,
                masterRule,
                limitListId ?? string.Empty,
                verboseLogging
            );
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            startingLifePoints = Mathf.Clamp(startingLifePoints, 1, 999_999);
            startingHandSize   = Mathf.Clamp(startingHandSize, 1, 10);
            turnTimerSeconds   = Mathf.Clamp(turnTimerSeconds, 0, 1800);
            maxMonsterZones    = Mathf.Clamp(maxMonsterZones, 1, 7);
            maxSpellTrapZones  = Mathf.Clamp(maxSpellTrapZones, 1, 7);

            if (masterRule == MasterRuleVersion.MR3)
                enablePendulumZones = false; // historically not present
        }
#endif

        // ===== Immutable runtime snapshot =====
        [Serializable]
        public readonly struct Runtime
        {
            public readonly int StartingLifePoints;
            public readonly int StartingHandSize;
            public readonly bool FirstTurnPlayerDraws;
            public readonly bool FirstTurnCanEnterBattlePhase;
            public readonly bool AllowFTK;
            public readonly TurnOrderPolicy TurnOrder;
            public readonly int TurnTimerSeconds;
            public readonly int MaxMonsterZones;
            public readonly int MaxSpellTrapZones;
            public readonly bool EnablePendulumZones;
            public readonly bool UseFieldZone;
            public readonly MasterRuleVersion MasterRule;
            public readonly string LimitListId;
            public readonly bool VerboseLogging;

            public Runtime(
                int startingLifePoints,
                int startingHandSize,
                bool firstTurnPlayerDraws,
                bool firstTurnCanEnterBattlePhase,
                bool allowFTK,
                TurnOrderPolicy turnOrder,
                int turnTimerSeconds,
                int maxMonsterZones,
                int maxSpellTrapZones,
                bool enablePendulumZones,
                bool useFieldZone,
                MasterRuleVersion masterRule,
                string limitListId,
                bool verboseLogging)
            {
                StartingLifePoints = startingLifePoints;
                StartingHandSize = startingHandSize;
                FirstTurnPlayerDraws = firstTurnPlayerDraws;
                // ✅ Fixed: correct assignment (no named-argument weirdness)
                FirstTurnCanEnterBattlePhase = firstTurnCanEnterBattlePhase;
                AllowFTK = allowFTK;
                TurnOrder = turnOrder;
                TurnTimerSeconds = turnTimerSeconds;
                MaxMonsterZones = maxMonsterZones;
                MaxSpellTrapZones = maxSpellTrapZones;
                EnablePendulumZones = enablePendulumZones;
                UseFieldZone = useFieldZone;
                MasterRule = masterRule;
                LimitListId = limitListId;
                VerboseLogging = verboseLogging;
            }
        }
    }
}
