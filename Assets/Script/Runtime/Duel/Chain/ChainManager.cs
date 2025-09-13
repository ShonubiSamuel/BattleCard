// Assets/Script/Runtime/Duel/Chain/Core/ChainManager.cs
//
// Central “Stack/Chain” controller.
// - Adds links after validating timing, speed, and effect-specific conditions.
// - Pays costs up-front and stores CostReceipts in the ChainLink.
// - Locks targets by reference (ITargetRef).
// - Resolves LIFO and raises EventBus notifications.
// - Implements IChainState so TurnManager / RuleSet can query IsChainEmpty.
//
// Dependencies used via constructor or ServiceLocator:
// - RuleSet (timing, spell-speed checks)
// - BoardManager (target lookup, moves)
// - DuelLogger (logs)
// - TurnManager (for IRuleDuelState)
// - EventBus (notifications)
// - DeterministicRng (optional, for effect RNGs)

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Chain.YGO.Duel.Chain;
using YGO.Duel.Data;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Runtime;

namespace YGO.Duel.Chain
{
    /// <summary>
    /// Chain manager API for the rest of the game.
    /// </summary>
    public interface IChainManager
    {
        /// <summary>Try to add (activate) a new chain link.</summary>
        bool TryAddLink(AddLinkArgs args, out ChainLink link, out string why);

        /// <summary>Resolve the topmost link (LIFO). Returns false if chain was empty.</summary>
        bool ResolveTop(out ChainLink resolvedLink);

        /// <summary>Resolve everything currently on the chain (top-down).</summary>
        void ResolveAll();

        /// <summary>Clear the chain without resolving (emergency/cancel paths).</summary>
        void Clear();

        /// <summary>True if no links are currently on the chain.</summary>
        bool IsEmpty { get; }

        /// <summary>Number of links on the chain.</summary>
        int Count { get; }

        /// <summary>Read-only snapshot of links (bottom to top).</summary>
        IReadOnlyList<ChainLink> Snapshot();
    }

    /// <summary>
    /// Constructor-style arguments for adding a link.
    /// </summary>
    public readonly struct AddLinkArgs
    {
        public readonly BoardManager.Seat Activator;
        public readonly object Source;               // card or system
        public readonly string SourceId;             // stable id (e.g., card.InstanceId); may be null/empty
        public readonly bool IsCardSource;           // convenience flag
        public readonly IEffectHandle Effect;        // effect blueprint
        public readonly List<ITargetRef> Targets;    // already selected/locked
        public readonly RuleSet.Timing Timing;       // activation timing (e.g., OpenGameState)
        public readonly string SummaryOverride;      // optional short summary for logs

        public AddLinkArgs(
            BoardManager.Seat activator,
            object source,
            string sourceId,
            bool isCardSource,
            IEffectHandle effect,
            List<ITargetRef> targets,
            RuleSet.Timing timing,
            string summaryOverride = null)
        {
            Activator = activator;
            Source = source;
            SourceId = sourceId ?? string.Empty;
            IsCardSource = isCardSource;
            Effect = effect ?? throw new ArgumentNullException(nameof(effect));
            Targets = targets ?? new List<ITargetRef>(0);
            Timing = timing;
            SummaryOverride = summaryOverride;
        }
    }

    /// <summary>
    /// Concrete ChainManager. Register in ServiceLocator for global access:
    /// ServiceLocator.Register<IChainManager>(new ChainManager(...));
    /// Also pass this instance to TurnManager (as IChainState) if you want TurnManager.IsChainEmpty to reflect the chain.
    /// </summary>
    public sealed class ChainManager : IChainManager, IChainState
    {
        private readonly List<ChainLink> _stack = new(8); // bottom..top
        private readonly RuleSet _rules;
        private readonly BoardManager _board;
        private readonly DuelLogger _log;
        private readonly TurnManager _turns; // optional (used to build adapters)
        private readonly DeterministicRng _rng; // optional

        private readonly EventBus _bus;

        public bool IsEmpty => _stack.Count == 0;
        public int Count => _stack.Count;

        public ChainManager(RuleSet rules, BoardManager board, DuelLogger logger, TurnManager turns = null, DeterministicRng rng = null, EventBus bus = null)
        {
            _rules = rules ?? ScriptableObject.CreateInstance<RuleSet>();
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _log   = logger ?? new DuelLogger();
            _turns = turns; // can be null in early boot
            _rng   = rng;

            // Prefer injected bus; else try ServiceLocator; else make a private one
            _bus = bus ?? (ServiceLocator.TryGet<EventBus>(out var b) ? b : new EventBus(_log));
        }

        /// <summary>
        /// Try to add a new chain link. Performs:
        /// 1) RuleSet timing & spell-speed check
        /// 2) Effect-specific condition check
        /// 3) Costs are paid and receipts stored
        /// 4) Link is pushed & EventBus notified
        /// </summary>
        public bool TryAddLink(AddLinkArgs args, out ChainLink link, out string why)
        {
            link = null;
            why = "";

            if (args.Effect == null) { why = "No effect"; return false; }

            // Resolve spell speed & timing gates
            var speed = args.Effect.Speed;

            var state = GetStateAdapter();

// Correct: is the ACTIVATOR the turn player?
            bool isControllerTurn = _turns != null
                ? (_turns.CurrentPlayer == args.Activator)
                : true; // if TurnManager not wired yet, trust the earlier Validate()

            if (!_rules.CanActivateEffect(speed, state, args.Timing, isControllerTurn))
            {
                why = $"Effect cannot be activated at this timing (speed={speed}, timing={args.Timing})";
                return false;
            }

            // Effect-specific conditions
            var condCtx = new ConditionContext(_board, args.Activator, state, _rules);
            if (!args.Effect.CheckAdditionalConditions(condCtx, out why))
            {
                if (string.IsNullOrEmpty(why)) why = "Effect-specific condition failed";
                return false;
            }

            // Costs
            var costCtx = new CostContext(_board, args.Activator, args.Source);
            var receipts = new List<CostReceipt>(4);
            foreach (var cost in args.Effect.GetCosts(costCtx))
            {
                if (cost == null) continue;
                if (!cost.TryPay(costCtx, out var r, out var cwhy))
                {
                    why = $"Failed to pay cost: {cwhy}";
                    return false;
                }
                if (r != null) receipts.Add(r);
            }

            // Targets are locked by reference (caller pre-selects them)
            var lockedTargets = new List<ITargetRef>(args.Targets ?? new List<ITargetRef>(0));

            // Calculate index (1-based top = newest)
            var index = _stack.Count + 1;

            // Build summary text
            var summary = BuildActivationSummary(args, speed);

            link = new ChainLink(
                index: index,
                activator: args.Activator,
                source: args.Source,
                sourceId: args.SourceId,
                isCardSource: args.IsCardSource,
                effect: args.Effect,
                speed: speed,
                timing: args.Timing,
                activationSummary: args.SummaryOverride ?? summary,
                targets: lockedTargets,
                costs: receipts
            );

            _stack.Add(link);
            _log.LogText("Chain.Add", link.ToString(), source: nameof(ChainManager));
            _bus?.RaiseChainLinkAdded(link);

            // Mark OPT (once-per-turn) if required at activation
            if (args.Effect is IOncePerTurn opt) opt.ConsumedThisTurn = true;

            return true;
        }

        /// <summary>
        /// Pop and resolve the topmost link. Returns false if chain is empty.
        /// </summary>
        public bool ResolveTop(out ChainLink resolvedLink)
        {
            resolvedLink = null;
            if (IsEmpty) return false;

            var top = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);

            // Build resolution context (you can choose to filter invalid targets here; 
            // many YGO effects fizzle if targets are illegal at resolution).
            var finalTargets = FilterTargetsForResolution(top.Targets);

            var resCtx = new ResolveContext(_board, top.Activator, top.Source, finalTargets);

            try
            {
                var action = top.Effect.BuildResolveAction(resCtx);
                if (action != null)
                {
                    action.Resolve(resCtx);
                }
                else
                {
                    _log.LogText("Chain.Resolve.Warn", $"No resolver for {top}", source: nameof(ChainManager));
                }
            }
            catch (Exception ex)
            {
                _log.LogText("Chain.Resolve.Error", ex.Message, data: ex.StackTrace, source: nameof(ChainManager));
            }
            
            // --- Post-resolution cleanup for S/T that shouldn't remain on the field ---
            if (top.IsCardSource && top.Source is Card src && src.Def != null)
            {
                var def = src.Def;

                // Stays only if it's Continuous/Equip/Field Spell, or Continuous Trap
                bool shouldRemain =
                    (def.IsSpell && (def.spellSubtype == SpellSubtype.Continuous
                                     || def.spellSubtype == SpellSubtype.Equip
                                     || def.spellSubtype == SpellSubtype.Field))
                    || (def.IsTrap  &&  def.trapSubtype  == TrapSubtype.Continuous);

                if (!shouldRemain)
                {
                    // not "destroyed"—just sent to GY by game rule on resolution
                    _board.SendToGraveyard(src, "Effect resolved");
                }
            }

            _log.LogText("Chain.Resolve", top.ToString(), source: nameof(ChainManager));
            _bus?.RaiseChainResolved(top);

            // If fully cleared, raise cleared event
            if (IsEmpty) _bus?.RaiseChainCleared();

            resolvedLink = top;
            return true;
        }

        /// <summary>
        /// Resolve every link currently on the chain (top → bottom).
        /// </summary>
        public void ResolveAll()
        {
            while (ResolveTop(out _)) { /* loop */ }
        }

        /// <summary>
        /// Force-clear the chain without resolving (e.g. scoop, reset, dev tool).
        /// </summary>
        public void Clear()
        {
            if (IsEmpty) return;
            _stack.Clear();
            _log.LogText("Chain.Cleared", "All links discarded", source: nameof(ChainManager));
            _bus?.RaiseChainCleared();
        }

        public IReadOnlyList<ChainLink> Snapshot() => _stack.AsReadOnly();

        // -------- Internals --------

        private RuleSet.IRuleDuelState GetStateAdapter()
        {
            if (_turns == null) return new RuleAdapters.DuelStateAdapter(null); // adapter handles null
            return new RuleAdapters.DuelStateAdapter(_turns);
        }
        private static List<ITargetRef> FilterTargetsForResolution(IReadOnlyList<ITargetRef> locked)
        {
            // Basic policy: keep only targets that are still valid.
            // Some effects in YGO partially resolve; callers/effects can still inspect the list.
            var list = new List<ITargetRef>(locked?.Count ?? 0);
            if (locked == null) return list;
            for (int i = 0; i < locked.Count; i++)
            {
                var t = locked[i];
                if (t != null && t.IsStillValid()) list.Add(t);
            }
            return list;
        }

        private string BuildActivationSummary(AddLinkArgs args, RuleSet.SpellSpeed speed)
        {
            // Friendly one-liner for logs/UI
            var sb = new StringBuilder(64);
            if (args.IsCardSource)
            {
                // Try to derive a nice name if possible
                string name = TryGetCardName(args.Source) ?? "Card";
                sb.Append(name);
            }
            else
            {
                sb.Append(args.Source?.GetType().Name ?? "Source");
            }

            sb.Append(": ");
            sb.Append(args.Effect?.EffectName ?? "Effect");
            sb.Append(" [SS");
            sb.Append(((int)speed).ToString());
            sb.Append("]");

            if (args.Targets != null && args.Targets.Count > 0)
            {
                sb.Append(" → ");
                // Show up to 2 targets succinctly
                int shown = 0;
                foreach (var t in args.Targets)
                {
                    if (t == null) continue;
                    if (shown++ > 0) sb.Append(", ");
                    sb.Append(t.DebugName);
                    if (shown >= 2 && args.Targets.Count > 2) { sb.Append(" (+)"); break; }
                }
            }
            return sb.ToString();
        }

        private string TryGetCardName(object src)
        {
            if (src is Card c) return c.Name;
            return null;
        }
        
     

        // IChainState implementation (so TurnManager can report IsChainEmpty correctly)
        bool IChainState.IsChainEmpty => IsEmpty;
    }
}