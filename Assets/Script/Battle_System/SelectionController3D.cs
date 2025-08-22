using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Runtime;
using Card = YGO.Duel.Cards.Card;

[DefaultExecutionOrder(-41)]
public sealed class SelectionController3D : MonoBehaviour
{
    [Header("Raycast")]
    public Camera rayCamera;
    public LayerMask cardLayer = ~0;
    public float maxRayDistance = 200f;
    

    // Services
    private TurnManager _turns;
    private DuelLogger  _log;
    private SpawnManager3D _spawner;

    // Selection state
    private Card _selectedAttacker;
    private Card3DView _selectedView;
    
    private IAttackCommandService _attack;
    
   


    private void Start()
    {
        if (!rayCamera) rayCamera = Camera.main;

        // Core services
        ServiceLocator.TryGet(out _turns);
        ServiceLocator.TryGet(out _log);
        ServiceLocator.TryGet(out _spawner);
        
        ServiceLocator.TryGet(out _attack);
        
    }

    private void OnDisable() => ClearSelection();

    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
        if (Input.GetMouseButtonDown(0))
            HandlePointer(Input.mousePosition);
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            HandlePointer(Input.GetTouch(0).position);
#endif
    }

    private void HandlePointer(Vector2 screenPos)
    {
        // Must be in Battle phase to select/attack
        if (_turns == null || _turns.CurrentPhase != RuleSet.Phase.Battle) return;

        // Raycast a 3D card view
        if (!RaycastCard(screenPos, out var view) || view == null || view.BoundCard == null)
        {
            // clicked empty space → clear
            ClearSelection();
            return;
        }
        
        print(view.name);

        var c  = view.BoundCard;
        var me = _turns.CurrentPlayer;

        // STEP 1: choose friendly face-up on-field monster as attacker
        if (_selectedAttacker == null)
        {
            if (!IsFriendlyFaceUpMonster(c, me))
            {
                _log?.LogText("Select3D.Invalid", $"Not a friendly face-up monster: {c?.Name}", source: nameof(SelectionController3D));
                return;
            }

            _selectedAttacker = c;
            _selectedView     = view;
            _selectedView.SetHighlighted(true);
            _log?.LogText("Select3D.Attacker", $"Selected attacker: {c.Name}", source: nameof(SelectionController3D));
            return;
        }

        // STEP 2: attacker selected → tap enemy monster to attack it
        if (IsEnemyFaceUpMonster(c, me))
        {
            if (_attack == null) { print("null battle"); ClearSelection(); return; }

            print("not null input");
            _selectedView?.SetHighlighted(false);
            var attacker = _selectedAttacker;
            ClearSelection();

            // ⤵️ NEW: route through the input controller (no battle logic here)
            _attack.TryDeclareAttack(attacker, c);
            return;
        }

        // Else: tapped irrelevant thing → clear
        ClearSelection();
    }

    private void OnUserPickedAttackerAndTarget(Card attacker, Card targetOrNull)
    {
        _attack?.TryDeclareAttack(attacker, targetOrNull);
    }
    private bool RaycastCard(Vector2 screen, out Card3DView view)
    {
        view = null;
        if (!rayCamera) return false;

        Ray ray = rayCamera.ScreenPointToRay(screen);
        if (!Physics.Raycast(ray, out var hit, maxRayDistance, cardLayer)) return false;

        view = hit.collider.GetComponentInParent<Card3DView>();
        return view != null;
    }

    private void ClearSelection()
    {
        if (_selectedView) _selectedView.SetHighlighted(false);
        _selectedAttacker = null;
        _selectedView     = null;
    }

    private static bool IsFriendlyFaceUpMonster(Card c, BoardManager.Seat me)
        => c != null && c.Controller == me && c.IsOnField && c.IsMonsterRuntime && c.IsFaceUp;

    private static bool IsEnemyFaceUpMonster(Card c, BoardManager.Seat me)
        => c != null && c.Controller != me && c.IsOnField && c.IsMonsterRuntime && c.IsFaceUp;

    // Exposed for a "Direct Attack" UI button
    public void TryDirectAttackFromCurrentSelection()
    {
        if (_turns == null || _turns.CurrentPhase != RuleSet.Phase.Battle) return;
        if (_selectedAttacker == null || _attack == null) return;

        var attacker = _selectedAttacker;
        _selectedView?.SetHighlighted(false);
        ClearSelection();

        // ⤵️ NEW: target=null means "direct" — legality checked by BattleManager
        //_attack.TryDeclareAttack(attacker, target: null);
    }
}
