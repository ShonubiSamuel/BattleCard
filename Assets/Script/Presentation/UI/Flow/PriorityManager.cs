// PriorityManager.cs
// Manages response windows ("Open Game State" + specific timings), priority passing, and pass-to-resolve flow.

using System;
using System.Collections.Generic;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;

namespace YGO.Duel.Runtime
{
    /// <summary>
    /// Backend bridge expected by PriorityManager. Implement this on your Chain system (or an adapter).
    /// </summary>
    public interface IPriorityBackend
    {
        bool IsChainEmpty { get; }
        /// <summary>Return true if any card/effect could legally be activated/triggered at this timing.</summary>
        bool CanAnyRespond(RuleSet.Timing timing, BoardManager.Seat whoHasPriority);
        /// <summary>Called when both players pass at this timing. Typical behavior: if chain empty, close; if not, resolve.</summary>
        void OnBothPlayersPass(RuleSet.Timing timing);
        /// <summary>Notify that a new link was added (resets pass flow).</summary>
        void OnLinkAdded();
    }

    /// <summary>
    /// Optional responders (UI/AI/network) can register to tell us whether they have a response available.
    /// This is in addition to backend legality checks.
    /// </summary>
    public interface IResponseProvider
    {
        bool HasResponse(RuleSet.Timing timing, BoardManager.Seat seat);
    }

    public sealed class PriorityManager
    {
        private readonly DuelLogger _logger;
        private readonly RuleSet _rules;
        private readonly BoardManager _board;
        private readonly IPriorityBackend _backend;
        private readonly Func<BoardManager.Seat> _getTurnPlayer;

        private readonly List<IResponseProvider> _providers = new List<IResponseProvider>();

        public bool WindowOpen { get; private set; }
        public RuleSet.Timing CurrentTiming { get; private set; } = RuleSet.Timing.OpenGameState;
        public BoardManager.Seat PrioritySeat { get; private set; }
        public int PassCount { get; private set; } // 0, 1, 2 -> both passed

        // Events
        public event Action<RuleSet.Timing> OnWindowOpened;
        public event Action<RuleSet.Timing> OnWindowClosed;
        public event Action<BoardManager.Seat, BoardManager.Seat> OnPriorityPassed; // from,to

        public PriorityManager(RuleSet rules,
                               BoardManager board,
                               IPriorityBackend backend,
                               DuelLogger logger,
                               Func<BoardManager.Seat> getTurnPlayer)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _logger = logger ?? new DuelLogger();
            _getTurnPlayer = getTurnPlayer ?? (() => BoardManager.Seat.P1);
        }

        public void RegisterProvider(IResponseProvider provider)
        {
            if (provider != null && !_providers.Contains(provider))
                _providers.Add(provider);
        }

        public void UnregisterProvider(IResponseProvider provider)
        {
            if (provider != null) _providers.Remove(provider);
        }

        /// <summary>Open a priority window for the supplied timing. Priority starts with the turn player by default.</summary>
        public void OpenWindow(RuleSet.Timing timing)
        {
            CurrentTiming = timing;
            PrioritySeat = _getTurnPlayer(); // YGO: turn player generally has priority first
            PassCount = 0;
            WindowOpen = true;

            _logger.LogText("Priority.Open", $"Open window: {timing}", data: $"priority={PrioritySeat}", source: nameof(PriorityManager));
            OnWindowOpened?.Invoke(timing);
        }

        /// <summary>Called by your chain system after adding a link.</summary>
        public void NotifyLinkAdded()
        {
            if (!WindowOpen) return;
            PassCount = 0;
            // priority passes to the opponent after an activation
            var from = PrioritySeat;
            PrioritySeat = BoardManager.OpponentOf(PrioritySeat);
            _backend.OnLinkAdded();
            _logger.LogText("Priority.LinkAdded", "Chain link added; priority passes to opponent",
                data: $"from={from}; to={PrioritySeat}; timing={CurrentTiming}", source: nameof(PriorityManager));
        }

        public bool HasResponses()
        {
            if (!WindowOpen) return false;

            // Backend legality first (covers triggers/fast effects)
            if (_backend.CanAnyRespond(CurrentTiming, PrioritySeat))
                return true;

            // UI/AI providers (optional)
            for (int i = 0; i < _providers.Count; i++)
                if (_providers[i].HasResponse(CurrentTiming, PrioritySeat))
                    return true;

            return false;
        }

        /// <summary>
        /// Current priority holder passes. If both players pass in succession:
        ///   - If chain is empty: close the window (and caller advances flow as appropriate)
        ///   - If chain is not empty: backend resolves chain (OnBothPlayersPass)
        /// </summary>
        public void PassPriority()
        {
            if (!WindowOpen) return;

            var from = PrioritySeat;
            var to = BoardManager.OpponentOf(from);

            PassCount++;
            _logger.LogText("Priority.Pass", $"Pass ({PassCount})", data: $"from={from}; to={to}; timing={CurrentTiming}", source: nameof(PriorityManager));
            OnPriorityPassed?.Invoke(from, to);

            if (PassCount >= 2)
            {
                // Both passed
                _logger.LogText("Priority.BothPass", $"Both passed @ {CurrentTiming}", source: nameof(PriorityManager));
                _backend.OnBothPlayersPass(CurrentTiming);

                if (_backend.IsChainEmpty)
                {
                    // Close window; caller decides what to do next (e.g., proceed in Damage Step or phase)
                    WindowOpen = false;
                    OnWindowClosed?.Invoke(CurrentTiming);
                    _logger.LogText("Priority.Close", $"Window closed @ {CurrentTiming}", source: nameof(PriorityManager));
                }
                else
                {
                    // If chain will resolve, priority flow continues after resolution (backend handles reopen/close)
                    PassCount = 0;
                }
            }
            else
            {
                // Single pass: hand priority to opponent
                PrioritySeat = to;
            }
        }
    }
}
