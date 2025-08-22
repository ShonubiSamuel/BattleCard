// PhaseRibbon.cs
// Clickable phase strip. In Draw phase, clicking "Draw" performs the draw then advances to Standby.
// Otherwise behaves like before (only the "next" phase button is clickable).

using UnityEngine;
using UnityEngine.UI;
using YGO.Duel.Rules;
using YGO.Duel.Foundation;
using YGO.Duel.Runtime;
using YGO.Duel.Runtime.Actions;
using YGO.Duel.Systems;       // <-- for DrawSystem
using YGO.Duel.Board;         // <-- for Seat

[DefaultExecutionOrder(-45)]
public sealed class PhaseRibbon : MonoBehaviour
{
    [Header("Buttons (order: Draw, Standby, Main1, Battle, Main2, End)")]
    public Button drawBtn;
    public Button standbyBtn;
    public Button main1Btn;
    public Button battleBtn;
    public Button main2Btn;
    public Button endBtn;

    private TurnManager _turns;
    private RuleSet     _rules;
    private ActionQueue _queue;
    private DrawSystem  _draws;   // <-- NEW

    private IPlayerDirectory _agents;

    private void Start()
    {
        // Hide the buttons we don't use in this simplified flow
        SetActive(standbyBtn, false);
        SetActive(main1Btn,   false);
        SetActive(main2Btn,   false);
        
        ServiceLocator.TryGet(out _turns);
        ServiceLocator.TryGet(out _rules);
        ServiceLocator.TryGet(out _queue);
        ServiceLocator.TryGet(out _draws);
        ServiceLocator.TryGet(out _agents);

        Wire(drawBtn,    RuleSet.Phase.Draw);
        Wire(standbyBtn, RuleSet.Phase.Standby);
        Wire(main1Btn,   RuleSet.Phase.Main1);
        Wire(battleBtn,  RuleSet.Phase.Battle);
        Wire(main2Btn,   RuleSet.Phase.Main2);
        Wire(endBtn,     RuleSet.Phase.End);

        if (_turns != null)
            _turns.OnPhaseChanged += (_, __) => RefreshInteractable();
        
        
        RefreshInteractable();
    }


    private static void SetActive(Button b, bool on) { if (b) b.gameObject.SetActive(on); }
    
    private void Wire(Button btn, RuleSet.Phase target)
    {
        if (!btn) return;
        btn.onClick.AddListener(() => OnPhaseClicked(target));
    }

    // PhaseRibbon.cs — inside OnPhaseClicked()
    private void OnPhaseClicked(RuleSet.Phase target)
    {
        if (_turns == null) return;

        var cur  = _turns.CurrentPhase;
        var next = _rules != null ? _rules.GetNextPhase(cur) : RuleSet.Phase.Draw;

        // --- Draw phase: click Draw to actually draw, then jump to Main1 ---
        if (cur == RuleSet.Phase.Draw)
        {
            bool canDraw = ShouldAllowDrawThisTurn();
            if (target == RuleSet.Phase.Draw && canDraw)
            {
                var seat = _turns.CurrentPlayer;
                _draws?.Draw(seat, 1, DrawReason.TurnStart, out _);
                // Draw -> Standby -> Main1
                EnqueueEndPhaseNTimes(2);
                return;
            }
            if (!canDraw && target == RuleSet.Phase.Standby)
            {
                // First-turn no-draw → still go to Main1
                EnqueueEndPhaseNTimes(2);
                return;
            }
            return;
        }

        // --- Main1: Battle or End (End should hand turn over to next Draw) ---
        if (cur == RuleSet.Phase.Main1)
        {
            if (target == RuleSet.Phase.Battle)
            {
                AdvancePhase(); // Main1 -> Battle
                return;
            }
            if (target == RuleSet.Phase.End)
            {
                // To End THEN to next Draw:
                // allowMain2: Main1->Battle->Main2->End->Draw (4 steps)
                // no Main2 : Main1->Battle->End->Draw      (3 steps)
                int steps = (_rules != null && _rules.allowMain2) ? 4 : 3;
                EnqueueEndPhaseNTimes(steps);
                return;
            }
            return;
        }

        // --- Battle: End should also reach next Draw ---
        if (cur == RuleSet.Phase.Battle)
        {
            if (target == RuleSet.Phase.End)
            {
                // allowMain2: Battle->Main2->End->Draw (3)
                // no Main2 : Battle->End->Draw        (2)
                int steps = (_rules != null && _rules.allowMain2) ? 3 : 2;
                EnqueueEndPhaseNTimes(steps);
                return;
            }
            return;
        }

        // --- End: allow pressing End again to go to next Draw ---
        if (cur == RuleSet.Phase.End)
        {
            if (target == RuleSet.Phase.End)
            {
                AdvancePhase(); // End -> Draw (TurnManager will rotate player & StartTurn)
            }
            return;
        }

        // Other phases: only natural next
        if (target != next) return;
        AdvancePhase();
    }

    private void EnqueueEndPhaseNTimes(int times)
    {
        if (_queue != null && _agents != null)
        {
            var agent = _agents.Get(_turns.CurrentPlayer); // ✅ correct seat
            for (int i = 0; i < times; i++) agent?.RequestEndPhase();
        }
        else if (_queue != null) // fallback: stamp seat directly
        {
            var s = _turns.CurrentPlayer;
            for (int i = 0; i < times; i++)
            {
                var a = new EndPhaseAction();
                a.FillSnapshot(s, _turns);
                _queue.Enqueue(a, out _);
            }
        }
        else
        {
            for (int i = 0; i < times; i++) _turns.AdvancePhase();
        }
    }

    private void AdvancePhase()
    {
        if (_queue != null && _agents != null)
        {
            _agents.Get(_turns.CurrentPlayer)?.RequestEndPhase();
        }
        else if (_queue != null)
        {
            var a = new EndPhaseAction();
            a.FillSnapshot(_turns.CurrentPlayer, _turns);
            _queue.Enqueue(a, out _);
        }
        else
        {
            _turns.AdvancePhase();
        }
    }
    private void RefreshInteractable()
    {
        if (_turns == null || _rules == null) return;

        var cur  = _turns.CurrentPhase;

        // Show only these three buttons in this simplified flow
        SetActive(drawBtn,   true);
        SetActive(battleBtn, true);
        SetActive(endBtn,    true);

        // Defaults: disabled
        drawBtn.interactable   = false;
        battleBtn.interactable = false;
        endBtn.interactable    = false;

        if (cur == RuleSet.Phase.Draw)
        {
            // Only Draw is clickable (except P1 Turn1 when first-turn draw is off)
            drawBtn.interactable = ShouldAllowDrawThisTurn();
            return;
        }

        if (cur == RuleSet.Phase.Main1)
        {
            // In Main1: Battle allowed only if RuleSet says so; End always allowed.
            var canBattle = _rules.CanEnterBattlePhase(_turns.GetDuelStateAdapter());
            battleBtn.interactable = canBattle;
            endBtn.interactable    = true;
            return;
        }

        if (cur == RuleSet.Phase.Battle)
        {
            // In Battle: only End is relevant (we will jump to End, skipping Main2 if needed)
            endBtn.interactable = true;
            return;
        }

        if (cur == RuleSet.Phase.End)
        {
            // Optional: allow clicking End again to finish quickly (advances to opponent Draw)
            endBtn.interactable = true;
        }
    }



    private bool ShouldAllowDrawThisTurn()
    {
        if (_rules == null || _turns == null) return true;
        // Typical rule: the player who goes first does NOT draw on Turn 1
        return !(_turns.TurnNumber == 1 && !_rules.ShouldFirstTurnDraw());
    }

    private static void SetInteractable(Button b, bool v) { if (b) b.interactable = v; }
}
