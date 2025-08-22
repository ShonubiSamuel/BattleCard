// DuelHud.cs
// LP bars, turn/phase indicator, turn timer, simple prompts.

using System;
using UnityEngine;
using UnityEngine.UI;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Runtime;

public sealed class DuelHud : MonoBehaviour
{
    [Header("LP")]
    public Text p1LPText;
    public Text p2LPText;
    public Slider p1LPSlider;
    public Slider p2LPSlider;

    [Header("Turn / Phase / Timer")]
    public Text turnText;     // "Turn 3 — P1"
    public Text phaseText;    // "Main Phase 1"
    public Text timerText;    // "00:37"

    [Header("Prompts")]
    public Text promptText;   // "Your move", "Waiting for opponent", etc.

    private BoardManager _board;
    private TurnManager  _turns;
    private DuelLogger   _logger;

    private int _lpMaxP1 = 8000;
    private int _lpMaxP2 = 8000;

    private void Awake()
    {
        ServiceLocator.TryGet(out _board);
        ServiceLocator.TryGet(out _turns);
        ServiceLocator.TryGet(out _logger);

        if (_turns != null)
        {
            _turns.OnTurnStarted  += HandleTurnStarted;
            _turns.OnPhaseChanged += HandlePhaseChanged;
            _turns.OnTurnEnded    += HandleTurnEnded;
            _turns.OnTurnTimerTick+= HandleTimerTick;
        }

        if (_logger != null)
            _logger.OnLogged += HandleLog;
    }

    private void OnDestroy()
    {
        if (_turns != null)
        {
            _turns.OnTurnStarted  -= HandleTurnStarted;
            _turns.OnPhaseChanged -= HandlePhaseChanged;
            _turns.OnTurnEnded    -= HandleTurnEnded;
            _turns.OnTurnTimerTick-= HandleTimerTick;
        }
        if (_logger != null)
            _logger.OnLogged -= HandleLog;
    }

    private void Start()
    {
        RefreshAll();
    }

    public void RefreshAll()
    {
        RefreshLP();
        RefreshTurnPhase();
        RefreshTimer(_turns != null ? _turns.TurnTimerRemaining : 0f);
    }

    private void RefreshLP()
    {
        if (_board == null) return;
        var p1 = _board.Players[(int)BoardManager.Seat.P1];
        var p2 = _board.Players[(int)BoardManager.Seat.P2];
        if (p1 == null || p2 == null) return;

        // Capture max on first draw
        _lpMaxP1 = Mathf.Max(_lpMaxP1, p1.LifePoints);
        _lpMaxP2 = Mathf.Max(_lpMaxP2, p2.LifePoints);

        if (p1LPText)   p1LPText.text   = p1.LifePoints.ToString();
        if (p2LPText)   p2LPText.text   = p2.LifePoints.ToString();

        if (p1LPSlider)
        {
            p1LPSlider.maxValue = _lpMaxP1;
            p1LPSlider.value = Mathf.Clamp(p1.LifePoints, 0, _lpMaxP1);
        }
        if (p2LPSlider)
        {
            p2LPSlider.maxValue = _lpMaxP2;
            p2LPSlider.value = Mathf.Clamp(p2.LifePoints, 0, _lpMaxP2);
        }
    }

    private void RefreshTurnPhase()
    {
        if (_turns == null || _board == null) return;

        var seat = _turns.CurrentPlayer;
        var tn   = _turns.TurnNumber;
        var ph   = _turns.CurrentPhase;

        if (turnText)  turnText.text  = $"Turn {tn} — {(seat==BoardManager.Seat.P1 ? "P1" : "P2")}";
        if (phaseText) phaseText.text = PhaseToLabel(ph);

        if (promptText)
        {
            promptText.text = $"Awaiting {(seat==BoardManager.Seat.P1 ? _board.Players[0].DisplayName : _board.Players[1].DisplayName)}";
        }
    }

    private static string PhaseToLabel(RuleSet.Phase p)
    {
        switch (p)
        {
            case RuleSet.Phase.Draw:    return "Draw Phase";
            case RuleSet.Phase.Standby: return "Standby Phase";
            case RuleSet.Phase.Main1:   return "Main Phase 1";
            case RuleSet.Phase.Battle:  return "Battle Phase";
            case RuleSet.Phase.Main2:   return "Main Phase 2";
            case RuleSet.Phase.End:     return "End Phase";
            default: return p.ToString();
        }
    }

    private void RefreshTimer(float seconds)
    {
        if (!timerText) return;
        int s = Mathf.CeilToInt(seconds);
        int m = s / 60; s %= 60;
        timerText.text = $"{m:00}:{s:00}";
    }

    // ---- Event handlers ----

    private void HandleTurnStarted(BoardManager.Seat seat, int turn)
    {
        RefreshTurnPhase();
        RefreshTimer(_turns.TurnTimerRemaining);
        if (promptText) promptText.text = $"{(_board.Players[(int)seat].DisplayName)}'s turn";
    }

    private void HandlePhaseChanged(RuleSet.Phase prev, RuleSet.Phase next)
    {
        RefreshTurnPhase();
    }

    private void HandleTurnEnded(BoardManager.Seat seat, int turn)
    {
        RefreshTurnPhase();
    }

    private void HandleTimerTick(float seconds)
    {
        RefreshTimer(seconds);
    }

    private void HandleLog(DuelLogger.LogEntry e)
    {
        // If LP changed or someone drew, refresh LP/hand counts, etc.
        if (e.Type.StartsWith("LP.") || e.Type.StartsWith("Draw"))
            RefreshLP();
    }
}
