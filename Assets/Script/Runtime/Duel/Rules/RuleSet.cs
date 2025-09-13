// RuleSet.cs
// Rules engine (ScriptableObject) that answers “is X legal?” and defines phase flow & response windows.

using System;
using UnityEngine;

namespace YGO.Duel.Rules
{
    /// <summary>
    /// Encodes core rule checks and phase/timing flow.
    /// Keep this logic data-driven so you can tweak behavior per format.
    /// </summary>
    [CreateAssetMenu(fileName = "RuleSet", menuName = "YGO/Duel/Rule Set", order = 1)]
    public sealed class RuleSet : ScriptableObject
    {
        // ----- Phase order & allowances -----

        public enum Phase { Draw, Standby, Main1, Battle, Main2, End }

        [Tooltip("If false, Main2 is skipped (old-school/simplified formats).")]
        public bool allowMain2 = true;

        [Tooltip("First-turn player can conduct a Battle Phase.")]
        public bool firstTurnCanEnterBattlePhase = false;

        [Tooltip("First-turn player draws during the Draw Phase.")]
        public bool firstTurnPlayerDraws = false;

        // ----- Summon policy -----

        [Tooltip("Players may conduct at most one Normal Summon/Set per turn.")]
        public bool normalSummonOncePerTurn = true;

        [Tooltip("Level 5–6 require 1 tribute; 7+ require 2. Set to false to ignore tribute rules (sandbox).")]
        public bool useStandardTributeRules = true;

        [Tooltip("Treat Tribute Summon level thresholds as inclusive: L5–6 → 1; L7+ → 2.")]
        public int tributeThresholdOne = 5; // Level >= 5 ⇒ at least 1 tribute
        public int tributeThresholdTwo = 7; // Level >= 7 ⇒ at least 2 tributes

        // ----- Response windows / spell speeds -----

        public enum SpellSpeed { One = 1, Two = 2, Three = 3 }

        [Tooltip("Quick effects/traps allowed during Damage Step (still typically restricted).")]
        public bool allowDamageStepResponses = true;

        /// <summary>
        /// Unified timing set. Older names are kept as aliases to richer timings.
        /// </summary>
        public enum Timing
        {
            // Open / general
            OpenGameState            = 0,

            // Activation/chain lifecycle
            OnCardActivated          = 10,   // a card/effect activation is announced/placed on chain
            OnChainLinkResolved      = 11,   // after an individual link resolves

            // Summon resolution
            OnSummonSuccess          = 20,   // Normal/Special/Flip successfully summoned

            // Battle flow
            OnAttackDeclared         = 30,
            OnBattleStepStart        = 31,
            OnDamageStepStart        = 32,
            BeforeDamageCalc         = 33,
            DuringDamageCalc         = 34,
            AfterDamageCalc          = 35,
            EndOfDamageStep          = 36,

            // Lifecycle (card movements)
            OnDestroyed              = 40,
            OnSentToGY               = 41,
            OnBanished               = 42,

            // Phase boundaries
            OnPhaseStart             = 50,
            OnPhaseEnd               = 51,

            // Fallback
            Other                    = 99
        }

        // ----- Interfaces so RuleSet stays decoupled from your concrete classes -----

        public interface IRulePlayer
        {
            /// <summary>True if this player has already used a Normal Summon/Set this turn.</summary>
            bool NormalSummonUsedThisTurn { get; set; }
            /// <summary>Is this the active turn player?</summary>
            bool IsTurnPlayer { get; }
        }

        public interface IRuleBoard
        {
            bool HasFreeMonsterZone(IRulePlayer player);
            int CountTributableMonsters(IRulePlayer player);
        }

        public interface IRuleDuelState
        {
            Phase CurrentPhase { get; }
            int TurnNumber { get; }       // 1-based
            IRulePlayer CurrentPlayer { get; }
            /// <summary>True if the chain is currently empty (open game state).</summary>
            bool IsChainEmpty { get; }
        }

        // ----- Public API -----

        /// <summary>Returns the next phase, considering whether Main2 is enabled.</summary>
        public Phase GetNextPhase(Phase current)
        {
            switch (current)
            {
                case Phase.Draw:    return Phase.Standby;
                case Phase.Standby: return Phase.Main1;
                case Phase.Main1:   return Phase.Battle;
                case Phase.Battle:  return allowMain2 ? Phase.Main2 : Phase.End;
                case Phase.Main2:   return Phase.End;
                case Phase.End:     return Phase.Draw;
                default:            return Phase.Draw;
            }
        }

        /// <summary>
        /// Can the active player enter Battle Phase now?
        /// Considers phase, first turn restriction, and (optionally) your own custom locks.
        /// </summary>
        public bool CanEnterBattlePhase(IRuleDuelState state)
        {
            if (state.CurrentPhase != Phase.Main1) return false;
            if (state.TurnNumber == 1 && !firstTurnCanEnterBattlePhase) return false;
            return true;
        }

        /// <summary>
        /// Compute tribute requirement for a monster level (0 if no tribute needed).
        /// </summary>
        public int GetRequiredTributes(int monsterLevel)
        {
            if (!useStandardTributeRules) return 0;
            if (monsterLevel >= tributeThresholdTwo) return 2;
            if (monsterLevel >= tributeThresholdOne) return 1;
            return 0;
        }

        /// <summary>
        /// Returns true if the player can perform a Normal Summon (or Set) right now.
        /// This does not check card-specific restrictions—only general game rules.
        /// </summary>
        public bool CanNormalSummon(IRulePlayer player, IRuleDuelState state, IRuleBoard board, int monsterLevel)
        {
            // Phase gate
            if (state.CurrentPhase != Phase.Main1 && state.CurrentPhase != Phase.Main2) return false;

            // Open game state (ignition-like action): must be chain empty and it's your turn
            if (!state.IsChainEmpty || !player.IsTurnPlayer) return false;

            // Once per turn
            if (normalSummonOncePerTurn && player.NormalSummonUsedThisTurn) return false;

            // Zone availability
            if (!board.HasFreeMonsterZone(player)) return false;

            // Tribute requirements
            int req = GetRequiredTributes(monsterLevel);
            if (req > 0 && board.CountTributableMonsters(player) < req) return false;

            return true;
        }

        /// <summary>Mark the player's once-per-turn Normal Summon as consumed.</summary>
        public void MarkNormalSummonUsed(IRulePlayer player)
        {
            if (normalSummonOncePerTurn) player.NormalSummonUsedThisTurn = true;
        }

        /// <summary>Reset per-turn flags at the start of a player's turn.</summary>
        public void ResetTurnFlags(IRulePlayer player)
        {
            player.NormalSummonUsedThisTurn = false;
        }

        /// <summary>
        /// Does this timing open a response window (can either player add to the chain)?
        /// </summary>
        public bool OpenResponseWindow(Timing timing)
        {
            switch (timing)
            {
                // Always open windows
                case Timing.OnCardActivated:
                case Timing.OnSummonSuccess:
                case Timing.OnAttackDeclared:
                case Timing.OnBattleStepStart:
                case Timing.OnDamageStepStart:
                case Timing.BeforeDamageCalc:
                case Timing.AfterDamageCalc:
                case Timing.EndOfDamageStep:
                case Timing.OnPhaseStart:
                case Timing.OnPhaseEnd:
                case Timing.OnChainLinkResolved:
                    return true;

                // Damage calculation window: allow if configured (usually restricted)
                case Timing.DuringDamageCalc:
                    return allowDamageStepResponses;
                // OpenGameState/Other do not auto-open a response window.
                default:
                    return false;
            }
        }

        /// <summary>
        /// Check if an effect of a given Spell Speed may be activated at this moment.
        /// (High-level approximation of YGO timing rules.)
        /// </summary>
        /// <param name="speed">Spell Speed of the effect (1/2/3).</param>
        /// <param name="state">Duel state (phase, whose turn, chain).</param>
        /// <param name="timing">Context timing.</param>
        /// <param name="isControllerTurn">Is it the controller's turn?</param>
        public bool CanActivateEffect(SpellSpeed speed, IRuleDuelState state, Timing timing, bool isControllerTurn)
        {
            // Spell Speed 1 (Ignition-like)
            if (speed == SpellSpeed.One)
            {
                if (!isControllerTurn) return false;
                if (state.CurrentPhase != Phase.Main1 && state.CurrentPhase != Phase.Main2) return false;
                if (!state.IsChainEmpty) return false;
                return true;
            }

            // Damage Step has special restrictions in real YGO;
            // here we gate via a simple flag.
            if (timing == Timing.DuringDamageCalc || timing == Timing.OnDamageStepStart || timing == Timing.OnDamageStepStart)
                return allowDamageStepResponses;

            // If chain is empty and it's Main1/Main2, quick effects can be used by turn player.
            if (state.IsChainEmpty && (state.CurrentPhase == Phase.Main1 || state.CurrentPhase == Phase.Main2))
                return true;

            // Otherwise, a response window must be open (due to some event).
            return OpenResponseWindow(timing);
        }

        /// <summary>Whether the first-turn player should draw during the Draw Phase.</summary>
        public bool ShouldFirstTurnDraw() => firstTurnPlayerDraws;
        
        // RuleSet.cs  — add alongside CanNormalSummon(...)
        public bool CanSetMonster(IRulePlayer player, IRuleDuelState state, IRuleBoard board, int monsterLevel)
        {
            // Same timing & once-per-turn budget as a Normal Summon.
            return CanNormalSummon(player, state, board, monsterLevel);
        }
        
        // Simple timing helper used by position/flip actions
        public bool IsMainPhaseOpen(IRuleDuelState state, IRulePlayer player)
        {
            if (state.CurrentPhase != Phase.Main1 && state.CurrentPhase != Phase.Main2) return false;
            if (!state.IsChainEmpty) return false;
            if (!player.IsTurnPlayer) return false;
            return true;
        }
        
        // RuleSet.cs — add this helper near CanActivateEffect(...)
        public bool CanActivateSpellTrap(
            SpellSpeed speed,
            IRuleDuelState state,
            Timing timing,
            bool isControllerTurn,
            bool wasSetThisTurn,
            bool isTrap)
        {
            // Traps can’t be activated the turn they’re Set (basic rule)
            if (isTrap && wasSetThisTurn) return false;

            // Speed 1 (Normal Spell, some Ignition-like effects) → only on your Main Phase, chain empty.
            if (speed == SpellSpeed.One)
                return isControllerTurn
                       && (state.CurrentPhase == Phase.Main1 || state.CurrentPhase == Phase.Main2)
                       && state.IsChainEmpty;

            // Speed 2 (Quick-Play Spells, normal Traps) → need a legal window or open chain window
            if (speed == SpellSpeed.Two)
                return OpenResponseWindow(timing) ||                           // reacting to an event
                       (state.IsChainEmpty &&                                  // or open state on Main
                        (state.CurrentPhase == Phase.Main1 || state.CurrentPhase == Phase.Main2));

            // Speed 3 (Counter Traps) → can respond where a window exists (typically to other effects)
            if (speed == SpellSpeed.Three)
                return OpenResponseWindow(timing);

            return false;
        }
        
      

    }
}
