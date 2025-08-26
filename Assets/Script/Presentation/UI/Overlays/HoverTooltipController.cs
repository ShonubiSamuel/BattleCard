// HoverTooltipController.cs
// Delayed, lazy-loaded tooltip that shows quick card info on hover.
// Subscribes to CardView hover events; uses ICardInfoProvider for text/art if available.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using Card = YGO.Duel.Cards.Card;

namespace YGO.Duel.UI
{
    public sealed class HoverTooltipController : MonoBehaviour
    {
        [Header("UI")]
        public RectTransform tooltipRoot;
        public Text titleText;
        public Text typeText;
        public Text bodyText;
        public Image artImage;

        [Header("Behavior")]
        public float showDelay = 0.35f;
        public Vector2 offset = new Vector2(16f, -12f);
        public bool clampToScreen = true;

        private Coroutine _pending;
        private CardView _current;
        private ICardInfoProvider _provider;
        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            ServiceLocator.TryGet(out _provider);
            CardView.OnAnyHoverEnter += HandleEnter;
            CardView.OnAnyHoverExit  += HandleExit;
            Hide();
        }

        private void OnDestroy()
        {
            CardView.OnAnyHoverEnter -= HandleEnter;
            CardView.OnAnyHoverExit  -= HandleExit;
        }

        private void HandleEnter(CardView v)
        {
            _current = v;
            if (_pending != null) StopCoroutine(_pending);
            _pending = StartCoroutine(CoShowDelayed(v));
        }

        private void HandleExit(CardView v)
        {
            if (v == _current)
            {
                if (_pending != null) StopCoroutine(_pending);
                Hide();
                _current = null;
            }
        }

        private IEnumerator CoShowDelayed(CardView v)
        {
            yield return new WaitForSeconds(showDelay);
            if (v == null || v.Card == null) { Hide(); yield break; }
            Populate(v.Card);
            ShowAt(Input.mousePosition);
        }

        private void Populate(Card card)
        {
            var info = _provider != null ? _provider.GetInfo(card) : null;

            if (titleText) titleText.text = info?.DisplayName ?? card.Name;
            if (typeText)  typeText.text  = info?.TypeLine ?? "";
            if (bodyText)  bodyText.text  = info?.EffectText ?? "";
            if (artImage)
            {
                artImage.sprite = info?.Art;
                artImage.enabled = artImage.sprite != null;
            }
        }

        private void ShowAt(Vector2 screenPos)
        {
            if (!tooltipRoot) return;
            tooltipRoot.gameObject.SetActive(true);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform, screenPos + offset,
                _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
                out var local);

            tooltipRoot.anchoredPosition = ClampIfNeeded(local);
        }

        private Vector2 ClampIfNeeded(Vector2 anchored)
        {
            if (!clampToScreen || _canvas == null) return anchored;
            var root = _canvas.transform as RectTransform;
            var size = tooltipRoot.sizeDelta;
            var half = size * 0.5f;
            var min = root.rect.min + half;
            var max = root.rect.max - half;

            anchored.x = Mathf.Clamp(anchored.x, min.x, max.x);
            anchored.y = Mathf.Clamp(anchored.y, min.y, max.y);
            return anchored;
        }

        public void Hide()
        {
            if (tooltipRoot) tooltipRoot.gameObject.SetActive(false);
        }
    }
}
