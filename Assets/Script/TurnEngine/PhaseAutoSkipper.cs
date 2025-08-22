using UnityEngine;
using YGO.Duel.Rules;
using YGO.Duel.Foundation;
using YGO.Duel.Runtime;
using YGO.Duel.Runtime.Actions;

public sealed class PhaseAutoSkipper : MonoBehaviour
{
    private TurnManager _turns;
    private ActionQueue _queue;

    void Awake()
    {
        ServiceLocator.TryGet(out _turns);
        ServiceLocator.TryGet(out _queue);
        if (_turns != null) _turns.OnPhaseChanged += OnPhaseChanged;
    }

    void OnDestroy()
    {
        if (_turns != null) _turns.OnPhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(RuleSet.Phase prev, RuleSet.Phase cur)
    {
        if (cur == RuleSet.Phase.Standby || cur == RuleSet.Phase.Main2)
            AdvanceOne();
    }

    private void AdvanceOne()
    {
        if (_queue != null)
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
}