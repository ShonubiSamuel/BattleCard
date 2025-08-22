// ChainManager.cs
// LIFO stack of ChainLinks. Validates timing/conditions, pays costs, locks targets, and resolves top-to-bottom.

using System;
using System.Collections.Generic;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;

namespace YGO.Duel.Chain
{
    /// <summary>Provides ChainManager the current rules view of the duel and whose turn it is.</summary>
    public interface IDuelStateProvider
    {
        RuleSet.IRuleDuelState GetDuelState();
        bool IsControllerTurn(BoardManager.Seat seat);
    }

    /// <summary>
    /// Activation request passed to AddLink: who, what, timing, chosen targets (from UI).
    /// </summary>
    public sealed class ActivationRequest
    {
        public BoardManager.Seat Activator;
        public object Source;                          // usually the Card runtime
        public IEffectHandle Effect;
        public List<ITargetRef> Targets = new();       // chosen via Targeting UI
        public RuleSet.Timing Timing;                  // context timing at activation

        public override string ToString()
            => $"{Effect?.EffectName ?? "Effect"} by {Activator} at {Timing} with {Targets?.Count ?? 0} targets";
    }

    /// <summary>
    /// Core chain system. Also implements IChainState so your TurnManager can query IsChainEmpty.
    /// </summary>
    public sealed class ChainManager : YGO.Duel.Runtime.IChainState
    {
        private readonly Stack<ChainLink> _stack = new();
        private readonly BoardManager _board;
        private readonly RuleSet _rules;
        private readonly IDuelStateProvider _stateProvider;
        private readonly DuelLogger _logger;
        private readonly CostSystem _costs;
        private readonly ConditionSystem _conditions;
        private readonly EventBus _bus; // optional global event hub

        public ChainManager(BoardManager board,
                            RuleSet rules,
                            IDuelStateProvider stateProvider,
                            DuelLogger logger,
                            CostSystem costSystem,
                            ConditionSystem conditionSystem)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
            _logger = logger ?? new DuelLogger();
            _costs = costSystem ?? throw new ArgumentNullException(nameof(costSystem));
            _conditions = conditionSystem ?? throw new ArgumentNullException(nameof(conditionSystem));

            // Try to capture a global EventBus if one is registered
            ServiceLocator.TryGet(out _bus);
        }

        // IChainState
        public bool IsChainEmpty => _stack.Count == 0;

        /// <summary>Number of links currently on the chain.</summary>
        public int Count => _stack.Count;

        /// <summary>Inspect the top link without removing it; returns null if empty.</summary>
        public ChainLink PeekTop() => _stack.Count > 0 ? _stack.Peek() : null;

        // ---------------- Events ----------------

        /// <summary>Fired when the very first link is added to an empty chain.</summary>
        public event Action OnChainOpened;
        /// <summary>Fired whenever a link is added (top increases).</summary>
        public event Action<ChainLink> OnLinkAdded;
        /// <summary>Fired before resolving a link (top entry).</summary>
        public event Action<ChainLink> OnResolvingLink;
        /// <summary>Fired after a link resolves.</summary>
        public event Action<ChainLink> OnLinkResolved;
        /// <summary>Fired when the chain returns to empty.</summary>
        public event Action OnChainEmptied;

        // ---------------- API ----------------

        /// <summary>
        /// Quick legality check without mutating the chain. Use this to gray/enable Activate buttons.
        /// </summary>
        public bool CanAddLink(ActivationRequest req, out string reason)
        {
            reason = string.Empty;
            if (req == null || req.Effect == null) { reason = "Invalid effect."; return false; }

            var duelState = _stateProvider.GetDuelState();
            bool isControllerTurn = _stateProvider.IsControllerTurn(req.Activator);

            // 1) Timing (Spell Speed + phase / window)
            if (!_rules.CanActivateEffect(req.Effect.Speed, duelState, req.Timing, isControllerTurn))
            {
                reason = "Timing not allowed for this effect.";
                return false;
            }

            // 2) Additional effect-specific conditions
            var condCtx = new ConditionContext(_board, req.Activator, duelState, _rules);
            if (!_conditions.CheckAdditional(req.Effect, condCtx, out reason))
                return false;

            // 3) Once per turn gates
            if (!_conditions.CheckOncePerTurn(req.Effect, out reason))
                return false;

            // 4) Costs preview (optional: ask costs if they *could* be paid)
            var costCtx = new CostContext(_board, req.Activator, req.Source);
            if (!_costs.CanPayAll(req.Effect, costCtx, out reason))
                return false;

            return true;
        }

        /// <summary>
        /// Add a new link to the chain. Pays costs immediately and locks targets.
        /// </summary>
        public bool AddLink(ActivationRequest req, out ChainLink link, out string error)
        {
            link = null; error = string.Empty;

            if (!CanAddLink(req, out error))
                return false;

            var costCtx = new CostContext(_board, req.Activator, req.Source);
            // 5) Pay costs (real mutation)
            if (!_costs.TryPayAll(req.Effect, costCtx, out var receipts, out error))
                return false;

            // 6) Lock targets (snapshot selection)
            var targets = req.Targets ?? new();

            // 7) Create link & push
            link = new ChainLink(index: _stack.Count + 1,
                                 activator: req.Activator,
                                 source: req.Source,
                                 effect: req.Effect,
                                 speed: req.Effect.Speed,
                                 targets: targets,
                                 costs: receipts);

            bool wasEmpty = _stack.Count == 0;
            _stack.Push(link);

            // 8) Mark once-per-turn if needed
            _conditions.MarkOncePerTurn(req.Effect);

            // 9) Events & logs
            if (wasEmpty)
            {
                OnChainOpened?.Invoke();
                _logger.LogText("Chain.Opened", $"Chain opened with {link}", source: nameof(ChainManager));
            }

            OnLinkAdded?.Invoke(link);
            _bus?.RaiseChainLinkAdded(link); // optional global bus
            _logger.LogText("Chain.Add", link.ToString(), source: nameof(ChainManager));

            return true;
        }

        /// <summary>
        /// Resolve chain in LIFO order until empty (or a consumer chooses to stop between links).
        /// </summary>
        public void Resolve()
        {
            if (_stack.Count == 0)
            {
                _logger.LogText("Chain.Resolve", "Resolve called on empty chain", source: nameof(ChainManager));
                return;
            }

            while (_stack.Count > 0)
            {
                var link = _stack.Pop();

                OnResolvingLink?.Invoke(link);
                _logger.LogText("Chain.Resolve.Start", link.ToString(), source: nameof(ChainManager));

                // Provide resolve context (targets may be invalid now—resolvers must handle it).
                var rctx = new ResolveContext(_board, link.Activator, link.Source, link.Targets);
                var action = link.Effect.BuildResolveAction(rctx);
                action?.Resolve(rctx);

                OnLinkResolved?.Invoke(link);
                _bus?.RaiseChainResolved(link); // optional global bus
                _logger.LogText("Chain.Resolve.Done", link.ToString(), source: nameof(ChainManager));
            }

            OnChainEmptied?.Invoke();
            _bus?.RaiseChainCleared(); // optional global bus
            _logger.LogText("Chain.Empty", "Chain is now empty", source: nameof(ChainManager));
        }

        /// <summary>Allows manual pop (e.g., negations that remove the top link without resolving it).</summary>
        public ChainLink PopTop()
        {
            if (_stack.Count == 0) return null;
            var popped = _stack.Pop();

            _logger.LogText("Chain.Pop", popped.ToString(), source: nameof(ChainManager));
            if (_stack.Count == 0)
            {
                OnChainEmptied?.Invoke();
                _bus?.RaiseChainCleared();
                _logger.LogText("Chain.Empty", "Chain is now empty", source: nameof(ChainManager));
            }
            return popped;
        }

        /// <summary>Snapshot array (bottom → top) suitable for UI display.</summary>
        public IReadOnlyList<ChainLink> Snapshot()
        {
            var arr = _stack.ToArray(); // top-first
            Array.Reverse(arr);         // bottom → top for UI
            return Array.AsReadOnly(arr);
        }
    }
}
