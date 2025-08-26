// ChainVfxController.cs
// Visualizes the chain stack as it builds/resolves. Items fly in from sources and pulse on resolve.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YGO.Duel.Foundation;
using YGO.Duel.Chain; // ChainLink
using YGO.Duel.UI;    // CardView
using Card = YGO.Duel.Cards.Card;

namespace YGO.Duel.VFX
{
    /// <summary>
    /// Optional chain events bridge; implement this in your ChainManager and register via ServiceLocator.
    /// </summary>
    public interface IChainEvents
    {
        event System.Action<ChainLink> OnLinkAdded;
        event System.Action<ChainLink> OnLinkResolving;
        event System.Action OnChainCleared;
    }

    public sealed class ChainVfxController : MonoBehaviour
    {
        [Header("UI Layout")]
        public RectTransform stackRoot;          // parent for the chain UI items
        public RectTransform itemTemplate;       // template containing Image + Text
        public float itemSpacing = 42f;
        public Vector2 stackStartOffset = new Vector2(0, 0);

        [Header("Animation")]
        public CardAnimator animator;            // optional, for pulses
        public float flyDuration = 0.22f;
        public float resolvePulseScale = 1.12f;

        private readonly List<RectTransform> _items = new();
        private DuelLogger _logger;

        private void Awake()
        {
            if (itemTemplate) itemTemplate.gameObject.SetActive(false);
            ServiceLocator.TryGet(out _logger);

            if (ServiceLocator.TryGet<IChainEvents>(out var ev) && ev != null)
            {
                ev.OnLinkAdded     += HandleLinkAdded;
                ev.OnLinkResolving += HandleLinkResolving;
                ev.OnChainCleared  += Clear;
            }
        }

        private void OnDestroy()
        {
            if (ServiceLocator.TryGet<IChainEvents>(out var ev) && ev != null)
            {
                ev.OnLinkAdded     -= HandleLinkAdded;
                ev.OnLinkResolving -= HandleLinkResolving;
                ev.OnChainCleared  -= Clear;
            }
        }

        // --------- Public convenience (you can call these directly) ---------

        public void ShowLink(ChainLink link, Transform fromWorldOrUI = null)
        {
            if (!stackRoot || !itemTemplate) return;
            var rt = Instantiate(itemTemplate, stackRoot);
            rt.gameObject.SetActive(true);

            // Set label/icon
            var img = rt.GetComponentInChildren<Image>(includeInactive: true);
            var txt = rt.GetComponentInChildren<Text>(includeInactive: true);
            if (txt) txt.text = link?.Effect?.EffectName ?? $"Link {link?.Index ?? 0}";
            if (img) img.enabled = true; // you can set a generic icon sprite in the template

            // Target anchored position based on stack size
            var target = stackStartOffset + new Vector2(0f, _items.Count * itemSpacing);
            rt.anchoredPosition = target;

            // Start from the source (if provided) and fly to target
            if (fromWorldOrUI != null)
            {
                var canvas = stackRoot.GetComponentInParent<Canvas>();
                Vector2 startLocal;
                if (fromWorldOrUI is RectTransform srcRT)
                {
                    // convert local pos of srcRT to canvas-local
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvas.transform as RectTransform,
                        RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, srcRT.position),
                        canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                        out startLocal);
                }
                else
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvas.transform as RectTransform,
                        RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, fromWorldOrUI.position),
                        canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                        out startLocal);
                }

                rt.anchoredPosition = startLocal;
                var anim = animator ? animator : stackRoot.gameObject.AddComponent<CardAnimator>();
                anim.MoveTo(rt, target, flyDuration);
            }

            _items.Add(rt);
        }

        public void PulseResolveTop()
        {
            if (_items.Count == 0) return;
            var top = _items[_items.Count - 1];
            var anim = animator ? animator : stackRoot.gameObject.AddComponent<CardAnimator>();
            anim.Pulse(top, resolvePulseScale, anim.defaultScale);
        }

        public void PopTop()
        {
            if (_items.Count == 0) return;
            var top = _items[_items.Count - 1];
            _items.RemoveAt(_items.Count - 1);
            Destroy(top.gameObject);
        }

        public void Clear()
        {
            foreach (var it in _items) if (it) Destroy(it.gameObject);
            _items.Clear();
        }

        // --------- Event adapters ---------

        private void HandleLinkAdded(ChainLink link)
        {
            // Try to find a visual origin: the card that created the link
            Transform src = null;
            if (link?.Source is Card card
                && CardViewRegistry.TryGet(card, out var cv) && cv)
            {
                src = cv.transform;
            }
            ShowLink(link, src);
        }

        private void HandleLinkResolving(ChainLink link)
        {
            PulseResolveTop();
            // Optional: delay then pop
            StartCoroutine(CoPopDelayed(0.18f));
        }

        private IEnumerator CoPopDelayed(float d)
        {
            yield return new WaitForSecondsRealtime(d);
            PopTop();
        }
    }
}
