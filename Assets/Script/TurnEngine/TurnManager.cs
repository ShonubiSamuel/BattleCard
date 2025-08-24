// TurnManager.cs
// Drives turn order, phase flow, and per-turn resets; bridges RuleSet timing.
// Not a MonoBehaviour: call Tick(deltaTime) from a driver if you use timers.

using System;
using UnityEngine;
using YGO.Duel.Battle;
using YGO.Duel.Board;        // BoardManager, Seat
using YGO.Duel.Foundation;   // GameConfig, ServiceLocator
using YGO.Duel.Rules;        // RuleSet

namespace YGO.Duel.Runtime
{
    public sealed class TurnManager
    {
        private readonly RuleSet _rules;
        private readonly BoardManager _board;
        private readonly DuelLogger _logger;

        // Optional chain state (plug your ChainManager later). If null => treat as empty chain.
        private readonly IChainState _chain;

        // Config snapshot (timers, first-turn rules, etc.)
        private GameConfig.Runtime _cfg;

        // Core state
        public int TurnNumber { get; private set; } = 0;                 // 1-based
        public BoardManager.Seat CurrentPlayer { get; private set; }     // Active seat
        public RuleSet.Phase CurrentPhase { get; private set; }          // Draw/Standby/Main1/Battle/Main2/End

        // Timers
        public bool UseTurnTimer => _cfg.TurnTimerSeconds > 0;
        public float TurnTimerRemaining { get; private set; } = 0f;

        // Derived (for RuleSet.IRuleDuelState)
        public bool IsChainEmpty => _chain?.IsChainEmpty ?? true;

        // Events
        public event Action<BoardManager.Seat, int> OnTurnStarted;
        public event Action<RuleSet.Phase, RuleSet.Phase> OnPhaseChanged;
        public event Action<BoardManager.Seat, int> OnTurnEnded;
        public event Action<float> OnTurnTimerTick; // seconds remaining
        public event Action OnTurnTimerExpired;

        public TurnManager(RuleSet rules, BoardManager board, DuelLogger logger, IChainState chainState = null)
        {
            _rules  = rules  ?? throw new ArgumentNullException(nameof(rules));
            _board  = board  ?? throw new ArgumentNullException(nameof(board));
            _logger = logger ?? new DuelLogger();
            _chain  = chainState; // may be null (treated as always empty)
        }

        /// <summary>
        /// Start the duel's first turn using <paramref name="cfg"/> to pick first player and timers.
        /// </summary>
        public void BeginFirstTurn(GameConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            _cfg = cfg.BuildRuntime();

            // Decide first player
            CurrentPlayer = DecideFirstPlayer(cfg);
            TurnNumber = 1;

            StartTurn();
        }

        /// <summary>Advance to next phase. If leaving End, rotates player and starts new turn.</summary>
        public void AdvancePhase()
        {
            var next = _rules.GetNextPhase(CurrentPhase);

            if (CurrentPhase == RuleSet.Phase.End && next == RuleSet.Phase.Draw)
            {
                // End of turn → switch player, increment turn number, reset flags, start new turn
                EndTurn();
                CurrentPlayer = BoardManager.OpponentOf(CurrentPlayer);
                TurnNumber++;
                StartTurn();
                return;
            }

            SetPhase(next);
        }

        /// <summary>Explicitly enter a specific phase (use carefully).</summary>
        public void SetPhase(RuleSet.Phase phase)
        {
            if (phase == CurrentPhase) return;
            var prev = CurrentPhase;
            CurrentPhase = phase;

            //_logger.LogEvent($"[TURN] Phase changed: {prev} → {CurrentPhase}");
            _logger.MarkTurnPhase(TurnNumber, CurrentPhase);
            _logger.LogText(
                type: "Turn.PhaseChange",
                summary: $"{prev} → {CurrentPhase}",
                data: $"player=P{(CurrentPlayer == BoardManager.Seat.P1 ? "1" : "2")}; turn={TurnNumber}",
                source: nameof(TurnManager));
            OnPhaseChanged?.Invoke(prev, CurrentPhase);
        }

        /// <summary>Per-frame update for timers (call from a driver MonoBehaviour).</summary>
        public void Tick(float deltaTime)
        {
            if (!UseTurnTimer || TurnTimerRemaining <= 0f) return;

            TurnTimerRemaining = Mathf.Max(0f, TurnTimerRemaining - deltaTime);
            OnTurnTimerTick?.Invoke(TurnTimerRemaining);

            if (Mathf.Approximately(TurnTimerRemaining, 0f))
            {
                _logger.LogText(
                    type: "Turn.TimerExpired",
                    summary: "Turn timer expired",
                    data: $"player=P{(CurrentPlayer == BoardManager.Seat.P1 ? "1" : "2")}; turn={TurnNumber}",
                    source: nameof(TurnManager));
                OnTurnTimerExpired?.Invoke();
                // Optionally auto-end phase/turn here.
            }
        }

        /// <summary>Helper: mark that the current player used their Normal Summon this turn.</summary>
        public void MarkNormalSummonUsed()
        {
            var player = new RuleAdapters.RulePlayerAdapter(CurrentPlayer, this, _board);
            _rules.MarkNormalSummonUsed(player);
        }

        /// <summary>Adapter used by RuleSet to read the duel state.</summary>
        public RuleSet.IRuleDuelState GetDuelStateAdapter()
            => new RuleAdapters.DuelStateAdapter(this);

        /// <summary>Adapter used by RuleSet to read counts/slots on the board.</summary>
        public RuleSet.IRuleBoard GetBoardAdapter()
            => new RuleAdapters.BoardAdapter(_board);

        /// <summary>Adapter for the current player from RuleSet's point of view.</summary>
        public RuleSet.IRulePlayer GetCurrentRulePlayer()
            => new RuleAdapters.RulePlayerAdapter(CurrentPlayer, this, _board);

        // -------------------- internals --------------------

        // TurnManager.cs  — replace the entire StartTurn() with this version
        private void StartTurn()
        {
            // Reset per-turn flags via RuleSet
            var p = new RuleAdapters.RulePlayerAdapter(CurrentPlayer, this, _board);
            _rules.ResetTurnFlags(p);
            ResetAttackFlagsFor(CurrentPlayer);

            // Timer
            TurnTimerRemaining = UseTurnTimer ? _cfg.TurnTimerSeconds : 0f;

            // Turn start log & event
            _logger.LogText(
                type: "Turn.Start",
                summary: $"P{(CurrentPlayer == BoardManager.Seat.P1 ? "1" : "2")} Turn {TurnNumber} start",
                source: nameof(TurnManager));
            OnTurnStarted?.Invoke(CurrentPlayer, TurnNumber);

            // Enter Draw via SetPhase so OnPhaseChanged fires and UI updates
            SetPhase(RuleSet.Phase.Draw);

            // If P1 doesn't draw on Turn 1, fast-forward to Main1
            if (TurnNumber == 1 && !_rules.ShouldFirstTurnDraw())
            {
                SetPhase(RuleSet.Phase.Standby);
                SetPhase(RuleSet.Phase.Main1);
            }
        }



        private void EndTurn()
        {
            _logger.LogText(
                type: "Turn.End",
                summary: $"P{(CurrentPlayer == BoardManager.Seat.P1 ? "1" : "2")} Turn {TurnNumber} end",
                source: nameof(TurnManager));
            OnTurnEnded?.Invoke(CurrentPlayer, TurnNumber);
        }

        private BoardManager.Seat DecideFirstPlayer(GameConfig cfg)
        {
            switch (cfg.turnOrder)
            {
                case GameConfig.TurnOrderPolicy.Player1AlwaysGoesFirst:
                    return BoardManager.Seat.P1;

                case GameConfig.TurnOrderPolicy.Player2AlwaysGoesFirst:
                    return BoardManager.Seat.P2;

                case GameConfig.TurnOrderPolicy.AskUI:
                    // TODO: hook a UI selection here; default to P1 for now.
                    _logger.LogText(
                        type: "Turn.FirstPlayer",
                        summary: "AskUI not implemented; defaulting to P1",
                        source: nameof(TurnManager));
                    return BoardManager.Seat.P1;

                case GameConfig.TurnOrderPolicy.FirstPlayerRandomCoinToss:
                default:
                    // Prefer deterministic RNG if available
                    // TurnManager.cs — inside DecideFirstPlayer(), fix the deterministic branch
                    if (ServiceLocator.Contains<DeterministicRng>())
                    {
                        var rng = ServiceLocator.Get<DeterministicRng>();
                        var first = rng.NextInt(0, 2) == 0 ? BoardManager.Seat.P1 : BoardManager.Seat.P2; // <- P2 here
                        _logger.LogText(
                            type: "Turn.CoinToss",
                            summary: $"Coin toss → {(first == BoardManager.Seat.P1 ? "P1" : "P2")} goes first",
                            source: nameof(TurnManager));
                        return first;
                    }
                    else
                    {
                        var first = (UnityEngine.Random.Range(0, 2) == 0) ? BoardManager.Seat.P1 : BoardManager.Seat.P2;
                        _logger.LogText(
                            type: "Turn.CoinToss",
                            summary: $"(Unity RNG) Coin toss → {(first == BoardManager.Seat.P1 ? "P1" : "P2")} goes first",
                            source: nameof(TurnManager));
                        return first;
                    }
            }
        }
        
        private void ResetAttackFlagsFor(BoardManager.Seat seat)
        {
            if (!ServiceLocator.TryGet<IBattlerResolver>(out var resolver) || resolver == null)
            {
                _logger.LogText("Turn.Reset", "No IBattlerResolver; cannot reset attack flags.", source: nameof(TurnManager));
                return;
            }

            var zones = _board.Zones[(int)seat];
            if (zones?.Monsters == null) return;

            int count = 0;
            foreach (var mz in zones.Monsters)
            {
                var c = mz.Top();
                if (c == null) continue;

                var b = resolver.Resolve(c);
                if (b == null) continue;

                b.HasAttackedThisTurn = false;  // ✅ reset per-turn flag
                count++;
            }

            _logger.LogText("Turn.Reset", $"Cleared HasAttacked for {count} monster(s) on P{(seat==BoardManager.Seat.P1?1:2)}.",
                source: nameof(TurnManager));
        }

    }

    /// <summary>
    /// Minimal chain-state interface so TurnManager can answer "IsChainEmpty" for RuleSet timing.
    /// Plug your ChainManager later; for now use NullChainState (always empty).
    /// </summary>
    public interface IChainState { bool IsChainEmpty { get; } }

    /// <summary>Default chain state: always empty (good for initial bring-up).</summary>
    public sealed class NullChainState : IChainState
    {
        public bool IsChainEmpty => true;
    }
}
