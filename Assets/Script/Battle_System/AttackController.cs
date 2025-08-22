// AttackController.cs
// Click a friendly face-up monster during Battle Phase to select an attacker.
// Then click an opponent monster to target it, or click the "Direct Attack" button to attack directly.

using UnityEngine;
using UnityEngine.UI;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Runtime;
using YGO.Duel.Runtime.Actions;
using YGO.Duel.UI;
using Card = YGO.Duel.Cards.Card;

[DefaultExecutionOrder(-40)]
public sealed class AttackController : MonoBehaviour
{
    [Header("Optional UI")]
    public Button directAttackButton;   // wire a button if you want an explicit Direct Attack

    private bool _subscribed;

    // Services
    private ActionQueue _queue;
    private TurnManager _turns;
    private DuelLogger  _logger;
    private BoardManager _board;
    private ICardIndex _index;

    // Selection
    private CardView _selectedAttacker;

    private void Start()
    {
        ServiceLocator.TryGet(out _queue);
        ServiceLocator.TryGet(out _turns);
        ServiceLocator.TryGet(out _logger);
        ServiceLocator.TryGet(out _board);
        ServiceLocator.TryGet(out _index);
    }

    private void OnEnable()
    {
        CardView.OnAnyClicked += HandleCardClicked;
        if (directAttackButton) directAttackButton.onClick.AddListener(HandleDirectAttackClicked);
        _subscribed = true;
    }

    private void OnDisable()
    {
        if (!_subscribed) return;
        CardView.OnAnyClicked -= HandleCardClicked;
        if (directAttackButton) directAttackButton.onClick.RemoveListener(HandleDirectAttackClicked);
        _subscribed = false;
        ClearSelection();
    }

    // ----------------- input -----------------

    private void HandleCardClicked(CardView v)
    {
        _logger?.LogText("Attack.HandleCardClicked", 
            $"Clicked card: {(v?.Card != null ? v.Card.Name : "null")}", 
            source: nameof(AttackController));

        if (v == null || v.Card == null)
        {
            _logger?.LogText("Attack.InvalidClick", "CardView or Card was null", source: nameof(AttackController));
            return;
        }

        if (!IsBattlePhase())
        {
            _logger?.LogText("Attack.NotBattlePhase", "Ignored click (not in Battle Phase)", source: nameof(AttackController));
            return;
        }

        var curSeat = _turns.CurrentPlayer;
        _logger?.LogText("Attack.CurrentSeat", $"Current player: {curSeat}", source: nameof(AttackController));

        // 1) no attacker yet → try select a friendly, face-up on-field monster
        if (_selectedAttacker == null)
        {
            if (IsFriendlyFaceUpMonster(v.Card, curSeat))
            {
                _selectedAttacker = v;
                _selectedAttacker.Highlight(true);
                _logger?.LogText("Attack.SelectAttacker", $"Selected attacker: {_selectedAttacker.Card.Name}", source: nameof(AttackController));
            }
            else
            {
                _logger?.LogText("Attack.InvalidAttacker", $"Clicked card is not a valid friendly attacker: {v.Card.Name}", source: nameof(AttackController));
            }
            return;
        }

        // 2) attacker selected → clicking enemy monster declares an attack at that target
        if (IsEnemyMonster(v.Card, curSeat))
        {
            _logger?.LogText("Attack.AttackTarget", $"Attacking {_selectedAttacker.Card.Name} -> {v.Card.Name}", source: nameof(AttackController));
            TryEnqueueDeclareAttack(_selectedAttacker.Card, v.Card);
            ClearSelection();
            return;
        }

        // Clicking somewhere irrelevant cancels selection
        _logger?.LogText("Attack.CancelSelection", $"Cancelled attack selection by clicking {v.Card.Name}", source: nameof(AttackController));
        ClearSelection();
    }


    private void HandleDirectAttackClicked()
    {
        if (!IsBattlePhase()) return;
        if (_selectedAttacker == null) return;

        // Let rules/action validation decide if direct is allowed.
        TryEnqueueDeclareAttack(_selectedAttacker.Card, target: null);
        ClearSelection();
    }

    // ----------------- helpers -----------------

    private bool IsBattlePhase()
        => _turns != null && _turns.CurrentPhase == RuleSet.Phase.Battle;

    private bool IsFriendlyFaceUpMonster(Card c, BoardManager.Seat curSeat)
    {
        if (c == null)
        {
            _logger?.LogText("Check.FriendlyFaceUp", "Card is null", source: nameof(AttackController));
            return false;
        }

        if (c.Controller != curSeat)
        {
            _logger?.LogText("Check.FriendlyFaceUp", 
                $"Card {c.Name} is controlled by {c.Controller}, not current seat {curSeat}", 
                source: nameof(AttackController));
            return false;
        }

        if (!c.IsOnField)
        {
            _logger?.LogText("Check.FriendlyFaceUp", $"Card {c.Name} is not on field", source: nameof(AttackController));
            return false;
        }

        if (!c.IsMonsterRuntime)
        {
            _logger?.LogText("Check.FriendlyFaceUp", $"Card {c.Name} is not a monster (runtime type mismatch)", source: nameof(AttackController));
            return false;
        }

        if (!c.IsFaceUp)
        {
            _logger?.LogText("Check.FriendlyFaceUp", $"Card {c.Name} is face-down", source: nameof(AttackController));
            return false;
        }

        _logger?.LogText("Check.FriendlyFaceUp", $"Card {c.Name} is valid friendly face-up monster", source: nameof(AttackController));
        return true;
    }


    private bool IsEnemyMonster(Card c, BoardManager.Seat curSeat)
    {
        if (c == null) return false;
        if (c.Controller == curSeat) return false;
        if (!c.IsOnField) return false;
        if (!c.IsMonsterRuntime) return false;
        return true;
    }

    private void TryEnqueueDeclareAttack(Card attacker, Card target)
    {
        if (_queue == null || _turns == null) return;

        var attackerId = ResolveId(attacker);
        var targetId   = target != null ? ResolveId(target) : null;

        var a = ActionFactory.DeclareAttack(_turns.CurrentPlayer, _turns, attackerId, targetId);
        if (_queue.Enqueue(a, out var err))
        {
            _logger?.LogText("Attack.Enqueue", 
                $"Declare attack: {attacker?.Name} → {(target!=null ? target.Name : "Direct")}",
                source: nameof(AttackController));
        }
        else
        {
            _logger?.LogText("Attack.Enqueue.Fail", $"Rejected: {err}", source: nameof(AttackController));
        }
    }

    private string ResolveId(Card c)
    {
        if (c == null) return "";
        if (_index != null)
        {
            var id = _index.GetId(c);
            if (!string.IsNullOrEmpty(id)) return id;
        }
        // hard fallback: runtime instance id is safest
        return c.InstanceId;
    }

    private void ClearSelection()
    {
        if (_selectedAttacker) _selectedAttacker.Highlight(false);
        _selectedAttacker = null;
    }
}