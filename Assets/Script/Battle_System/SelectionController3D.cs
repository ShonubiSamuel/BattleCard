using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Runtime;
using Card = YGO.Duel.Cards.Card;
using System;
using YGO.Duel.Battle;
using YGO.Duel.Effects;
using YGO.Duel.Runtime.Actions;

public sealed class SelectionController3D : MonoBehaviour
{
    [Header("Raycast")]
    public Camera rayCamera;
    [Tooltip("Layer(s) for 3D card views")]
    public LayerMask cardLayer = ~0;
    [Tooltip("Layer(s) for player avatars")]
    public LayerMask avatarLayer = ~0;
    public float maxRayDistance = 200f;
    
    public SummonContextPanel summonContextPanel;

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

    // SelectionController3D.cs
// NOTE: ensure you have: using System;  (for Action in ShowSummonContextFor)

    private void HandlePointer(Vector2 screenPos)
    {
        if (InputLockService.IsLocked) return;
        if (_turns == null) return;

        // 1) Card click?
        if (RaycastCard(screenPos, out var view) && view != null && view.BoundCard != null)
        {
            var phase = _turns.CurrentPhase;

            
            if (phase == RuleSet.Phase.Main1 || phase == RuleSet.Phase.Main2)
            {
                // MAIN PHASE → open summon/flip/position context *only for your own on-field monsters*.
                var c = view.BoundCard;
                if (c.Controller == _turns.CurrentPlayer && c.IsOnField && c.IsMonsterRuntime)
                {
                    ShowSummonContextFor(view, screenPos);
                    return;
                }

                // NEW: face-down S/T you control → offer Activate
                if (c.CurrentZone == BoardManager.CardZone.SpellTrap)
                {
                    ShowSTContextFor(view, screenPos);
                    return;
                }
            }

            if (phase == RuleSet.Phase.Battle)
            {
                // BATTLE PHASE → use combat targeting flow.
                HandleCardClick(view);
                return;
            }

            // Other phases: ignore card clicks.
            return;
        }

        // 2) Avatar click (Battle-phase direct attacks)
        if (RaycastAvatar(screenPos, out var avatar) && avatar != null)
        {
            if (_turns.CurrentPhase == RuleSet.Phase.Battle)
            {
                HandleAvatarClick(avatar);
                return;
            }

            // Outside Battle, ignore avatar clicks.
            return;
        }

        // 3) Empty space → clear any combat selection/highlights.
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
        if (IsEnemyMonsterAnyFace(c, me))
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
    
    // SelectionController3D.cs — add method
    // SelectionController3D.cs — replace the body of ShowSummonContextFor(...)
    private void ShowSummonContextFor(Card3DView view, Vector2 clickScreenPos)
    {
        
        if (summonContextPanel == null) return;
        var c = view.BoundCard;

        bool isFaceUp = c.IsFaceUp;
        bool inAttack = c.Position == YGO.Duel.Cards.CardBattlePosition.Attack;

        // Ask PositionManager about legality right now
        ServiceLocator.TryGet(out PositionManager pm);
        ServiceLocator.TryGet(out TurnManager turns);
        ServiceLocator.TryGet(out YGO.Duel.Rules.RuleSet rules);

        bool canFlip = !isFaceUp;
        bool canToAtk = isFaceUp && !inAttack;
        bool canToDef = isFaceUp &&  inAttack;

        string _; // throwaway reason
        if (pm != null)
        {
            if (canFlip  && !pm.CanFlipSummonNow(c, rules, turns, out _))  canFlip  = false;
            if (canToAtk && !pm.CanChangePositionNow(c, rules, turns, out _)) canToAtk = false;
            if (canToDef && !pm.CanChangePositionNow(c, rules, turns, out _)) canToDef = false;
        }

        // If nothing is legal, do not open the panel.
        if (!canFlip && !canToAtk && !canToDef) return;
        
        
        // Resolve id & build enqueue callbacks
        ServiceLocator.TryGet(out ICardIndex index);
        string id = (index != null) ? index.GetId(c) : c.InstanceId;
        ServiceLocator.TryGet(out ActionQueue queue);
        var me = _turns.CurrentPlayer;

        void Enq(GameAction a, string tag)
        {
            if (queue == null) return;
            if (!queue.Enqueue(a, out var err) && _log != null)
                _log.LogText("SummonContext.Fail", $"{tag} rejected: {err}", source: nameof(SelectionController3D));
        }

        Action onFlip  = canFlip  ? () => Enq(ActionFactory.FlipSummon(me, turns, id), "FlipSummon") : null;
        Action onToAtk = canToAtk ? () => Enq(ActionFactory.ChangePosition(me, turns, id, YGO.Duel.Battle.BattlePosition.Attack), "ToATK") : null;
        Action onToDef = canToDef ? () => Enq(ActionFactory.ChangePosition(me, turns, id, YGO.Duel.Battle.BattlePosition.Defense), "ToDEF") : null;

        // Place near the card (world → screen)
        var worldScreen = (rayCamera != null) 
            ? rayCamera.WorldToScreenPoint(view.transform.position) 
            : new Vector3(clickScreenPos.x, clickScreenPos.y, 0f);

        Vector2 screenPos = new Vector2(worldScreen.x, worldScreen.y);
        summonContextPanel.ShowFor(
            card: c,
            screenPos: screenPos,
            showFlip:  canFlip,
            showToAtk: canToAtk,
            showToDef: canToDef,
            onFlip:  onFlip,
            onToAtk: onToAtk,
            onToDef: onToDef,
            onCancel: null
        );
    }

    // SelectionController3D.cs — add this method (parallel to ShowSummonContextFor)
    private void ShowSTContextFor(Card3DView view, Vector2 clickScreenPos)
    {
        if (summonContextPanel == null) return; // you can reuse or have a separate panel for ST
        var c = view.BoundCard;

        // Basic legality: only Activate (no “Set” from field)
        bool canActivate = false;
        string why = "";

        ServiceLocator.TryGet(out RuleSet rules);
        ServiceLocator.TryGet(out TurnManager turns);
        ServiceLocator.TryGet(out BoardManager board);


        RuleSet.SpellSpeed ss = c.Def.GetDeclaredSpeed("");   // primary effect by default
        //var ss = handle != null ? handle.Speed : RuleSet.SpellSpeed.One; // fallback covered in the builder
        
        
        var state = new RuleAdapters.DuelStateAdapter(turns);
        var player = new RuleAdapters.RulePlayerAdapter(c.Controller, turns, board);
        bool isControllerTurn = player.IsTurnPlayer;
        bool wasSetThisTurn = c.WasSetThisTurn;
        bool isTrap = c.Def.IsTrap;

        canActivate = rules.CanActivateSpellTrap(ss, state, RuleSet.Timing.OpenGameState, isControllerTurn, wasSetThisTurn, isTrap);
        if (!canActivate) why = wasSetThisTurn && isTrap ? "Trap was set this turn" : "Not a legal timing";

        // If face-down ST and activation is allowed, open a tiny one-button panel
        if (!canActivate) return;

        // Enqueue Activate on click
        ServiceLocator.TryGet(out ActionQueue queue);
        ServiceLocator.TryGet(out ICardIndex index);
        string id = (index != null) ? index.GetId(c) : c.InstanceId;

        // If your SummonContextPanel only supports monster ops, either:
        //  a) show a minimal confirmation panel, or
        //  b) directly enqueue activation.
        // Here we’ll directly enqueue for brevity.
        var act = ActionFactory.ActivateSpellTrap(turns.CurrentPlayer, turns, id, "", RuleSet.Timing.OpenGameState);
        if (!queue.Enqueue(act, out var err) && _log != null)
            _log.LogText("Select3D.Activate.Fail", err, source: nameof(SelectionController3D));
    }

    private static bool IsFriendlyFaceUpMonster(Card c, BoardManager.Seat me)
        => c != null && c.Controller == me && c.IsOnField && c.IsMonsterRuntime && c.IsFaceUp;

    private static bool IsEnemyMonsterAnyFace(Card c, BoardManager.Seat me)
        => c != null && c.Controller != me && c.IsOnField && c.IsMonsterRuntime; // face-up NOT required
}
