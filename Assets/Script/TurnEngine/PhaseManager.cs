// PhaseManager.cs
// Draw → Standby → Main1 → Battle → Main2 → End
// Works standalone or as a helper used by TurnManager.

using System;
using YGO.Duel.Foundation; // DuelLogger
using YGO.Duel.Rules;      // RuleSet
using YGO.Duel.Board;      // BoardManager

namespace YGO.Duel.Runtime
{
    public sealed class PhaseManager
    {
        private readonly RuleSet _rules;
        private readonly DuelLogger _logger;
        private readonly Func<int> _getTurnNumber;                   // supplied by TurnManager
        private readonly Func<BoardManager.Seat> _getCurrentPlayer;  // supplied by TurnManager

        public RuleSet.Phase CurrentPhase { get; private set; } = RuleSet.Phase.Draw;

        // Events (fine-grained + generic)
        public event Action<RuleSet.Phase, RuleSet.Phase> OnPhaseChanged;
        public event Action OnDraw;     public event Action OnStandby;
        public event Action OnMain1;    public event Action OnBattle;
        public event Action OnMain2;    public event Action OnEnd;

        public PhaseManager(RuleSet rules, DuelLogger logger,
                            Func<int> getTurnNumber,
                            Func<BoardManager.Seat> getCurrentPlayer)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _logger = logger ?? new DuelLogger();
            _getTurnNumber = getTurnNumber ?? (() => 0);
            _getCurrentPlayer = getCurrentPlayer ?? (() => BoardManager.Seat.P1);
        }

        public void EnterPhase(RuleSet.Phase phase)
        {
            if (phase == CurrentPhase) return;

            var prev = CurrentPhase;
            CurrentPhase = phase;

            // Keep logger markers tidy for searches/filters
            _logger.MarkTurnPhase(_getTurnNumber(), CurrentPhase);
            _logger.LogText(
                type: "Phase.Enter",
                summary: $"Enter {CurrentPhase}",
                data: $"player=P{(_getCurrentPlayer()==BoardManager.Seat.P1?"1":"2")}; turn={_getTurnNumber()}",
                source: nameof(PhaseManager));

            OnPhaseChanged?.Invoke(prev, CurrentPhase);
            FirePhaseEvent(CurrentPhase);
        }

        public void AdvancePhase()
        {
            var next = _rules.GetNextPhase(CurrentPhase);
            EnterPhase(next);
        }

        private void FirePhaseEvent(RuleSet.Phase phase)
        {
            switch (phase)
            {
                case RuleSet.Phase.Draw:    OnDraw?.Invoke(); break;
                case RuleSet.Phase.Standby: OnStandby?.Invoke(); break;
                case RuleSet.Phase.Main1:   OnMain1?.Invoke(); break;
                case RuleSet.Phase.Battle:  OnBattle?.Invoke(); break;
                case RuleSet.Phase.Main2:   OnMain2?.Invoke(); break;
                case RuleSet.Phase.End:     OnEnd?.Invoke(); break;
            }
        }
    }
}
