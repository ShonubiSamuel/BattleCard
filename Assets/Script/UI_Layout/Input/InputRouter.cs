// InputRouter.cs
// Centralizes click/tap routing: raycast UI → detect CardView or ZoneView → forward to HumanController (if available)
// Falls back to logging if no HumanController is registered.
// Also plays nice with DragDropController (which handles drags); InputRouter focuses on "click" semantics.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using Card = YGO.Duel.Cards.Card;

namespace YGO.Duel.UI
{
    /// <summary>
    /// Optional sink implemented by your HumanController to receive routed inputs.
    /// Register the HumanController in ServiceLocator as IHumanInputSink (recommended).
    /// </summary>
    public interface IHumanInputSink
    {
        void OnCardClicked(Card card);
        void OnZoneClicked(BoardManager.ZoneId zoneId);
        void OnBackgroundClicked();
    }

    /// <summary>
    /// Optional provider implemented by your zone UI to expose a ZoneId.
    /// A simple ZoneView component can implement this interface.
    /// </summary>
    public interface IZoneView
    {
        BoardManager.ZoneId GetZoneId();
    }

    [DefaultExecutionOrder(-50)]
    public sealed class InputRouter : MonoBehaviour
    {
        [Header("References")]
        public EventSystem eventSystem;
        public GraphicRaycaster uiRaycaster; // raycaster on your main UI canvas

        [Header("Behavior")]
        public bool routeClicks = true;

        private DuelLogger _logger;

        private void Awake()
        {
            if (!eventSystem) eventSystem = EventSystem.current;
            if (!uiRaycaster)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas) uiRaycaster = canvas.GetComponent<GraphicRaycaster>();
            }
            ServiceLocator.TryGet(out _logger);
        }

        private void Update()
        {
            if (!routeClicks) return;
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            if (Input.GetMouseButtonDown(0))
                RoutePointer(Input.mousePosition);
#else
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
                RoutePointer(Input.GetTouch(0).position);
#endif
        }

        private void RoutePointer(Vector2 screenPos)
        {
            // UI raycast first (cards/zones live in UI)
            if (TryRaycastUI(screenPos, out var go))
            {
                // Card?
                var cv = go.GetComponentInParent<CardView>();
                if (cv && cv.Card != null)
                {
                    if (ServiceLocator.TryGet<IHumanInputSink>(out var sink) && sink != null)
                        sink.OnCardClicked(cv.Card);
                    else
                        _logger?.LogText("InputRouter", $"Card clicked: {cv.Card.Name}", source: nameof(InputRouter));
                    return;
                }

                // Zone?
                var zv = go.GetComponentInParent<MonoBehaviour>(); // any MB that implements IZoneView
                if (zv is IZoneView zoneview)
                {
                    var zid = zoneview.GetZoneId();
                    if (ServiceLocator.TryGet<IHumanInputSink>(out var sink2) && sink2 != null)
                        sink2.OnZoneClicked(zid);
                    else
                        _logger?.LogText("InputRouter", $"Zone clicked: {zid}", source: nameof(InputRouter));
                    return;
                }
            }

            // Background
            if (ServiceLocator.TryGet<IHumanInputSink>(out var sink3) && sink3 != null)
                sink3.OnBackgroundClicked();
            else
                _logger?.LogText("InputRouter", "Background clicked", source: nameof(InputRouter));
        }

        private bool TryRaycastUI(Vector2 screenPos, out GameObject hitGO)
        {
            hitGO = null;
            if (!uiRaycaster || !eventSystem) return false;

            var results = new List<RaycastResult>(8);
            var data = new PointerEventData(eventSystem) { position = screenPos };
            uiRaycaster.Raycast(data, results);

            if (results.Count > 0)
            {
                hitGO = results[0].gameObject;
                return true;
            }
            return false;
        }
    }
}
