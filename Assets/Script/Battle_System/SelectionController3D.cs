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
    [Tooltip("Layer(s) for 3D card views")]
    public LayerMask cardLayer = ~0;
    [Tooltip("Layer(s) for player avatars")]
    public LayerMask avatarLayer = ~0;
    public float maxRayDistance = 200f;

    // Services
    private TurnManager     _turns;
    private DuelLogger      _log;
    private SpawnManager3D  _spawner;
    private IAttackCommandService _attack;

    // Selection state
    private Card        _selectedAttacker;
    private Card3DView  _selectedView;
    private PlayerAvatar3D _hoverAvatar; // optional hover highlight cache

    private void Start()
    {
        if (!rayCamera) rayCamera = Camera.main;

        ServiceLocator.TryGet(out _turns);
        ServiceLocator.TryGet(out _log);
        ServiceLocator.TryGet(out _spawner);
        ServiceLocator.TryGet(out _attack);
    }

    private void OnDisable()
    {
        ClearSelection();
        SetAvatarHover(null);
    }

    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
        if (Input.GetMouseButtonDown(0))
            HandlePointer(Input.mousePosition);
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            HandlePointer(Input.GetTouch(0).position);
#endif
        // (optional) avatar hover preview
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
        UpdateHover(Input.mousePosition);
#endif
    }

    // ------------------- INPUT -------------------

    private void HandlePointer(Vector2 screenPos)
    {
        // SelectionController3D.cs  — inside HandlePointer(Vector2 screenPos)
        if (InputLockService.IsLocked) return;
        // Must be in Battle phase to select/attack
        if (_turns == null || _turns.CurrentPhase != RuleSet.Phase.Battle) return;

        // 1) Try click a CARD first (maintains your existing behavior)
        if (RaycastCard(screenPos, out var view) && view != null && view.BoundCard != null)
        {
            HandleCardClick(view);
            return;
        }

        // 2) If not a card, try click a PLAYER AVATAR
        if (RaycastAvatar(screenPos, out var avatar) && avatar != null)
        {
            HandleAvatarClick(avatar);
            return;
        }

        // 3) Clicked empty space → clear
        ClearSelection();
    }

    private void HandleCardClick(Card3DView view)
    {
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
            if (_attack == null) { ClearSelection(); return; }
            _selectedView?.SetHighlighted(false);

            var attacker = _selectedAttacker;
            ClearSelection();

            _attack.TryDeclareAttack(attacker, c);
            return;
        }

        // Else: tapped irrelevant thing → clear
        ClearSelection();
    }

    private void HandleAvatarClick(PlayerAvatar3D avatar)
    {
        if (_turns == null || _attack == null) return;

        var me = _turns.CurrentPlayer;
        var clickedSeat = avatar.seat;

        // Must click opponent avatar, and must have an attacker selected
        if (_selectedAttacker == null || clickedSeat == me)
        {
            // If you want: selecting your own avatar cancels selection
            ClearSelection();
            return;
        }

        // Direct attack attempt (target=null). Legality (no opponent monsters, effect checks)
        // is handled by BattleManager via DirectAttackValidator.
        _selectedView?.SetHighlighted(false);
        var attacker = _selectedAttacker;
        ClearSelection();

        _attack.TryDeclareAttack(attacker);
    }

    // ------------------- HOVER (optional polish) -------------------

    private void UpdateHover(Vector2 screenPos)
    {
        // Only show hover if an attacker is selected and player points at the opponent avatar
        if (_selectedAttacker == null) { SetAvatarHover(null); return; }

        if (RaycastAvatar(screenPos, out var avatar) && avatar != null)
        {
            // highlight only opponent avatar
            if (avatar.seat != _turns.CurrentPlayer) { SetAvatarHover(avatar); return; }
        }
        SetAvatarHover(null);
    }

    private void SetAvatarHover(PlayerAvatar3D avatar)
    {
        if (_hoverAvatar == avatar) return;
        if (_hoverAvatar != null) _hoverAvatar.SetHighlighted(false);
        _hoverAvatar = avatar;
        if (_hoverAvatar != null) _hoverAvatar.SetHighlighted(true);
    }

    // ------------------- RAYCASTS -------------------

    private bool RaycastCard(Vector2 screen, out Card3DView view)
    {
        view = null;
        if (!rayCamera) return false;

        Ray ray = rayCamera.ScreenPointToRay(screen);
        if (!Physics.Raycast(ray, out var hit, maxRayDistance, cardLayer)) return false;

        view = hit.collider.GetComponentInParent<Card3DView>();
        return view != null;
    }

    private bool RaycastAvatar(Vector2 screen, out PlayerAvatar3D avatar)
    {
        avatar = null;
        if (!rayCamera) return false;

        Ray ray = rayCamera.ScreenPointToRay(screen);
        if (!Physics.Raycast(ray, out var hit, maxRayDistance, avatarLayer)) return false;

        avatar = hit.collider.GetComponentInParent<PlayerAvatar3D>();
        return avatar != null;
    }

    // ------------------- UTILS -------------------

    private void ClearSelection()
    {
        if (_selectedView) _selectedView.SetHighlighted(false);
        _selectedAttacker = null;
        _selectedView     = null;
        SetAvatarHover(null);
    }

    private static bool IsFriendlyFaceUpMonster(Card c, BoardManager.Seat me)
        => c != null && c.Controller == me && c.IsOnField && c.IsMonsterRuntime && c.IsFaceUp;

    private static bool IsEnemyFaceUpMonster(Card c, BoardManager.Seat me)
        => c != null && c.Controller != me && c.IsOnField && c.IsMonsterRuntime && c.IsFaceUp;

    // Optional external entry point if you have a UI button
    public void TryDirectAttackFromCurrentSelection()
    {
        if (_turns == null || _turns.CurrentPhase != RuleSet.Phase.Battle) return;
        if (_selectedAttacker == null || _attack == null) return;

        _selectedView?.SetHighlighted(false);
        var attacker = _selectedAttacker;
        ClearSelection();

        _attack.TryDeclareAttack(attacker);
    }
}