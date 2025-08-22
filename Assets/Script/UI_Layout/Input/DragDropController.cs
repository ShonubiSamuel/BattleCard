// DragDropController.cs
// Drag cards from HAND → drop onto Monster/SpellTrap zones to Summon/Set.
// Uses ActionFactory + ActionQueue. Highlights legal drop targets via ZoneView.
// Tip: Hold SHIFT while dropping on MZ to "Set" the monster instead of "Normal Summon".

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Runtime;
using YGO.Duel.Runtime.Actions;
using Card = YGO.Duel.Cards.Card;

namespace YGO.Duel.UI
{

    [DefaultExecutionOrder(-45)]
    public sealed class DragDropController : MonoBehaviour
    {
        [Header("References")]
        public EventSystem eventSystem;
        public GraphicRaycaster uiRaycaster;
        public Canvas uiCanvas;               // for positioning the ghost
        public Image dragGhostPrefab;         // simple Image used as drag visual (optional)

        [Header("Behavior")]
        public float dragThreshold = 6f;      // pixels before a press becomes a drag
        public bool  onlyCurrentPlayerHand = true;
        
 
        private bool _dragging;
        private Vector2 _pressPos;
        private CardView _dragCardView;
        private Image _ghost;
        private ZoneView _hoverZone;

        // Services
        private ActionQueue _queue;
        private TurnManager _turns;
        private DuelLogger  _logger;
        private BoardManager _board; // NEW
        // private RuleSet _rules;   // (optional if you want deeper preflight)

        private void Awake()
        {
            if (!eventSystem) eventSystem = EventSystem.current;
            if (!uiRaycaster)
            {
                var c = GetComponentInParent<Canvas>();
                if (c) uiRaycaster = c.GetComponent<GraphicRaycaster>();
            }
            if (!uiCanvas) uiCanvas = GetComponentInParent<Canvas>();

            ServiceLocator.TryGet(out _queue);
            ServiceLocator.TryGet(out _turns);
            ServiceLocator.TryGet(out _logger);
            ServiceLocator.TryGet(out _board);   // NEW
        }

        private void Update()
        {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            var isDown  = Input.GetMouseButtonDown(0);
            var isHeld  = Input.GetMouseButton(0);
            var isUp    = Input.GetMouseButtonUp(0);
            var pos     = (Vector2)Input.mousePosition;
#else
            bool isDown=false, isHeld=false, isUp=false; Vector2 pos=Vector2.zero;
            if (Input.touchCount > 0) { var t=Input.GetTouch(0); pos=t.position; isDown=t.phase==TouchPhase.Began; isHeld=t.phase==TouchPhase.Moved || t.phase==TouchPhase.Stationary; isUp=t.phase==TouchPhase.Ended || t.phase==TouchPhase.Canceled; }
#endif
            if (isDown) BeginPress(pos);
            if (!_dragging && isHeld) MaybeBeginDrag(pos);
            if (_dragging) OnDrag(pos);
            if (_dragging && isUp) EndDrag(pos);
        }

        private void BeginPress(Vector2 screenPos)
        {
            _pressPos = screenPos;
            _dragCardView = RaycastForCard(screenPos);
        }

        private void MaybeBeginDrag(Vector2 screenPos)
        {
            if (_dragCardView == null) return;
            if ((screenPos - _pressPos).sqrMagnitude < dragThreshold * dragThreshold) return;

            // Only allow dragging from the active player's HAND (optional)
            if (onlyCurrentPlayerHand && !IsInCurrentPlayerHand(_dragCardView.Card))
            {
                _dragCardView = null;
                return;
            }

            _dragging = true;
            CreateGhost(_dragCardView);
            _logger?.LogText("DragDrop", $"Begin drag: {_dragCardView.Card?.Name}", source: nameof(DragDropController));
        }

        private void OnDrag(Vector2 screenPos)
        {
            MoveGhost(screenPos);

            var zv = RaycastForZone(screenPos);
            if (_hoverZone != zv)
            {
                SetZoneHighlight(_hoverZone, false);
                _hoverZone = null;

                // Only highlight if the hovered zone is a legal target for this card
                if (zv != null && _dragCardView != null && _dragCardView.Card != null)
                {
                    var isShift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    if (IsLegalDropTarget(_dragCardView.Card, zv, isShift, out _))
                    {
                        _hoverZone = zv;
                        SetZoneHighlight(_hoverZone, true);
                    }
                }
            }
        }

        private void EndDrag(Vector2 screenPos)
        {
            SetZoneHighlight(_hoverZone, false);

            var target = _hoverZone;
            var card   = _dragCardView?.Card;

            DestroyGhost();
            _dragging = false;
            _dragCardView = null;

            if (card == null || target == null) return;

            // Determine intent based on destination zone + modifiers
            // SHIFT on MZ => Set monster; otherwise Normal Summon
            // Check legality before enqueuing anything
            var isShift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!IsLegalDropTarget(card, target, isShift, out var why))
            {
                _logger?.LogText("DragDrop.Illegal", $"Drop rejected: {why}", source: nameof(DragDropController));
                return;
            }

            if (target.kind == BoardManager.CardZone.Monster)
            {
                if (isShift)
                {
                    // Set monster
                    EnqueueSetToMonster(card, target);
                }
                else
                {
                    // Normal Summon
                    EnqueueNormalSummon(card, target);
                }
            }
            else if (target.kind == BoardManager.CardZone.SpellTrap)
            {
                // Set S/T
                EnqueueSetST(card, target);
            }
            // else: ignore (no action)
        }

        // -------------- Actions --------------

        private bool IsMainPhase()
        {
            if (_turns == null) return false;
            var p = _turns.CurrentPhase;
            return p == RuleSet.Phase.Main1 || p == RuleSet.Phase.Main2;
        }
        
        private void EnqueueNormalSummon(Card card, ZoneView target)
        {
            if (_queue == null || _turns == null) return;

            if (!card?.Def?.IsMonster ?? true)
            {
                _logger?.LogText("DragDrop.Blocked", "Only monsters can be Normal Summoned");
                return;
            }
            if (!IsMainPhase())
            {
                _logger?.LogText("DragDrop.Blocked", $"Cannot Normal Summon during {_turns.CurrentPhase}");
                return;
            }

            var id = ResolveCardId(card);
            var a  = ActionFactory.NormalSummon(target.seat, _turns, id, target.index);
            if (_queue.Enqueue(a, out var err))
                _logger?.LogText("DragDrop.Summon", $"NS {card.Name} → MZ[{target.index}] (P{(target.seat==BoardManager.Seat.P1?1:2)})", source: nameof(DragDropController));
            else
                _logger?.LogText("DragDrop.Summon.Fail", $"NS rejected: {err}", source: nameof(DragDropController));
        }

        private void EnqueueSetToMonster(Card card, ZoneView target)
        {
            if (_queue == null || _turns == null) return;

            if (!card?.Def?.IsMonster ?? true)
            {
                _logger?.LogText("DragDrop.Blocked", "Only monsters can be Set to the Monster Zone");
                return;
            }
            if (!IsMainPhase())
            {
                _logger?.LogText("DragDrop.Blocked", $"Cannot Set a monster during {_turns.CurrentPhase}");
                return;
            }

            var id = ResolveCardId(card);
            var a  = ActionFactory.SetToMonster(target.seat, _turns, id, target.index);
            if (_queue.Enqueue(a, out var err))
                _logger?.LogText("DragDrop.SetMonster", $"Set {card.Name} → MZ[{target.index}]", source: nameof(DragDropController));
            else
                _logger?.LogText("DragDrop.SetMonster.Fail", $"Set rejected: {err}", source: nameof(DragDropController));
        }

        private void EnqueueSetST(Card card, ZoneView target)
        {
            if (_queue == null || _turns == null) return;

            if (!(card?.Def?.IsSpell == true || card?.Def?.IsTrap == true))
            {
                _logger?.LogText("DragDrop.Blocked", "Only Spells/Traps can be Set to the S/T Zone");
                return;
            }
            if (!IsMainPhase())
            {
                _logger?.LogText("DragDrop.Blocked", $"Cannot Set a card during {_turns.CurrentPhase}");
                return;
            }

            var id = ResolveCardId(card);
            var a  = ActionFactory.SetToST(target.seat, _turns, id, target.index);
            if (_queue.Enqueue(a, out var err))
                _logger?.LogText("DragDrop.SetST", $"Set {card.Name} → ST[{target.index}]", source: nameof(DragDropController));
            else
                _logger?.LogText("DragDrop.SetST.Fail", $"Set rejected: {err}", source: nameof(DragDropController));
        }

        
        private string ResolveCardId(Card card)
        {
            if (ServiceLocator.TryGet<ICardIndex>(out var idx) && idx != null)
            {
                var id = idx.GetId(card);
                if (!string.IsNullOrEmpty(id)) return id;
            }
            return card?.InstanceId ?? ""; // fallback must be runtimeId, not name
        }

        // -------------- Helpers --------------

        private CardView RaycastForCard(Vector2 screenPos)
        {
            if (!uiRaycaster || !eventSystem) return null;
            var results = new List<RaycastResult>(8);
            uiRaycaster.Raycast(new PointerEventData(eventSystem) { position = screenPos }, results);
            foreach (var r in results)
            {
                var cv = r.gameObject.GetComponentInParent<CardView>();
                if (cv != null) return cv;
            }
            return null;
        }

        private ZoneView RaycastForZone(Vector2 screenPos)
        {
            if (!uiRaycaster || !eventSystem) return null;
            var results = new List<RaycastResult>(8);
            uiRaycaster.Raycast(new PointerEventData(eventSystem) { position = screenPos }, results);
            foreach (var r in results)
            {
                var zv = r.gameObject.GetComponentInParent<ZoneView>();
                if (zv != null) return zv;
                // If you use your own class implementing IZoneView:
                var any = r.gameObject.GetComponentInParent<MonoBehaviour>();
                if (any is IZoneView iv)
                {
                    // Wrap into a lightweight proxy ZoneView so the rest of the code can use seat/kind/index.
                    var zid = iv.GetZoneId();
                    var proxy = any.GetComponent<ZoneView>() ?? any.gameObject.AddComponent<ZoneView>();
                    proxy.seat  = zid.Seat;
                    proxy.kind  = zid.Kind;
                    proxy.index = zid.Index;
                    return proxy;
                }
            }
            return null;
        }

        private bool IsInCurrentPlayerHand(Card card)
        {
            if (card == null || _turns == null) return false;
            var seat = _turns.CurrentPlayer;
            var hand = ServiceLocator.Get<BoardManager>().Zones[(int)seat].Hand;

            // Support both `.Cards` list or other shapes
            var fld = hand.GetType().GetField("Cards");
            if (fld != null)
            {
                var list = fld.GetValue(hand) as System.Collections.IList;
                return list != null && list.Contains(card);
            }
            var contains = hand.GetType().GetMethod("Contains", new[] { typeof(Card) });
            if (contains != null) return (bool)contains.Invoke(hand, new object[] { card });
            return false;
        }

        private void CreateGhost(CardView source)
        {
            if (!dragGhostPrefab || !uiCanvas) return;

            _ghost = Instantiate(dragGhostPrefab, uiCanvas.transform);
            _ghost.raycastTarget = false;
            // pick sprite: face-down back or art
            if (source.isFaceDown && source.faceDownBack)
                _ghost.sprite = source.faceDownBack.GetComponent<Image>()?.sprite;
            else
                _ghost.sprite = source.artImage ? source.artImage.sprite : null;

            _ghost.color = new Color(1f, 1f, 1f, 0.85f);
            _ghost.rectTransform.sizeDelta = (source.artImage ? source.artImage.rectTransform.sizeDelta : new Vector2(200, 280));
            _ghost.gameObject.SetActive(true);
        }

        private void MoveGhost(Vector2 screenPos)
        {
            if (!_ghost) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                uiCanvas.transform as RectTransform, screenPos, uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : uiCanvas.worldCamera,
                out var localPos);
            _ghost.rectTransform.anchoredPosition = localPos;
        }
        
        

        private void DestroyGhost()
        {
            if (_ghost) Destroy(_ghost.gameObject);
            _ghost = null;
        }

        private void SetZoneHighlight(ZoneView zv, bool on)
        {
            if (!zv) return;
            if (zv.highlight) zv.highlight.enabled = on;
        }
        
        private bool IsLegalDropTarget(Card card, ZoneView target, bool isShift, out string reason)
        {
            reason = "";
            if (card == null || target == null) { reason = "No card/target"; return false; }
            if (_turns == null)                 { reason = "Turns unavailable"; return false; }

            // You can only drop to your own zones (active player)
            if (target.seat != _turns.CurrentPlayer)
            {
                reason = "You can only place cards on your own field";
                return false;
            }

            // From hand only (sanity, UI already enforces this at drag start)
            if (!IsInCurrentPlayerHand(card))
            {
                reason = "Card is not in your hand";
                return false;
            }

            // Zone kind rules
            switch (target.kind)
            {
                case BoardManager.CardZone.Monster:
                {
                    if (!(card.Def?.IsMonster ?? false))
                    {
                        reason = "Only monsters can be placed in the Monster Zone";
                        return false;
                    }

                    // If we can see the board, ensure target slot is empty
                    if (_board != null)
                    {
                        var mz = _board.Zones[(int)target.seat].Monsters;
                        if (target.index < 0 || target.index >= mz.Length)
                        {
                            reason = "Invalid monster zone index";
                            return false;
                        }
                        if (!IsZoneSlotEmpty(mz[target.index]))
                        {
                            reason = "That monster zone is occupied";
                            return false;
                        }
                    }
                    return true;
                }

                case BoardManager.CardZone.SpellTrap:
                {
                    if (!((card.Def?.IsSpell ?? false) || (card.Def?.IsTrap ?? false)))
                    {
                        reason = "Only Spells/Traps can be placed in the S/T Zone";
                        return false;
                    }

                    if (_board != null)
                    {
                        var st = _board.Zones[(int)target.seat].SpellsTraps;
                        if (target.index < 0 || target.index >= st.Length)
                        {
                            reason = "Invalid S/T zone index";
                            return false;
                        }
                        if (!IsZoneSlotEmpty(st[target.index]))
                        {
                            reason = "That S/T zone is occupied";
                            return false;
                        }
                    }
                    return true;
                }

                // (Optional) handle Pendulum/Field later if you add UI affordances for those
                default:
                    reason = $"Dropping to {target.kind} is not supported from hand";
                    return false;
            }
        }

        // Minimal reflection helper to test if a board slot is empty
        private static bool IsZoneSlotEmpty(object zoneSlot)
        {
            if (zoneSlot == null) return false;

            var f = zoneSlot.GetType().GetField("Card");            // legacy single-slot
            if (f != null) return f.GetValue(zoneSlot) == null;

            var top = zoneSlot.GetType().GetMethod("Top");          // modern stack API
            if (top != null) return top.Invoke(zoneSlot, null) == null;

            var prop = zoneSlot.GetType().GetProperty("IsEmpty");   // explicit flag
            if (prop != null) return (bool)prop.GetValue(zoneSlot);

            return false; // unknown shape => be conservative
        }
    }
}
