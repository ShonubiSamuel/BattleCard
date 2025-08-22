// CardView.cs
// Renders a single card: art, frame, name, stats. Supports flip/rotate and hover/click events.
// Decoupled via provider interfaces; falls back gracefully if data is missing.

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using Card = YGO.Duel.Cards.Card;
using TMPro;

namespace YGO.Duel.UI
{
    public interface ICardArtProvider
    {
        Sprite GetArt(Card card);
        Sprite GetFrame(Card card); // optional; return null to skip
    }

    public interface ICardStatProvider
    {
        bool TryGetStats(Card card, out int atk, out int def, out int level, out string typeLine);
        string GetDisplayName(Card card); // allows aliasing definition name vs instance
    }

    /// <summary>Static registry so other UI (targeting overlay, inspector) can find a CardView for a card.</summary>
    public static class CardViewRegistry
    {
        private static readonly System.Collections.Generic.Dictionary<Card, CardView> _map
            = new System.Collections.Generic.Dictionary<Card, CardView>();

        public static void Register(Card c, CardView v)
        {
            if (c != null) _map[c] = v;
        }

        public static void Unregister(Card c, CardView v)
        {
            if (c != null && _map.TryGetValue(c, out var cv) && cv == v) _map.Remove(c);
        }

        public static bool TryGet(Card c, out CardView v) => _map.TryGetValue(c, out v);
    }

    public sealed class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("References")] public Image artImage; // Large art
        public Image frameImage; // Optional frame overlay
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI statsText; // "ATK/DEF" or type line if non-monster
        public GameObject faceDownBack; // toggle this when face-down (e.g., generic card back)
        public CanvasGroup highlightFx; // optional glow/intensity

        [Header("State")] public bool isFaceDown = false;
        public bool interactable = true;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Card == null) return;
            OnAnyHoverEnter?.Invoke(this);
            // Optional: visual feedback
            Highlight(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Card == null) return;
            OnAnyHoverExit?.Invoke(this);
            // Optional: remove visual feedback
            Highlight(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!interactable || Card == null) return;
            OnAnyClicked?.Invoke(this);
            // If you want to react differently to right/left clicks:
            // if (eventData.button == PointerEventData.InputButton.Right) { /* context menu, etc. */ }
        }

        public Card Card { get; private set; }

        public static event Action<CardView> OnAnyHoverEnter;
        public static event Action<CardView> OnAnyHoverExit;
        public static event Action<CardView> OnAnyClicked;

        private ICardArtProvider _art;
        private ICardStatProvider _stats;
        private DuelLogger _logger;
        private IPointerClickHandler _pointerClickHandlerImplementation;

        private void Awake()
        {
            ServiceLocator.TryGet(out _art);
            ServiceLocator.TryGet(out _stats);
            ServiceLocator.TryGet(out _logger);
        }

        private void OnDestroy()
        {
            if (Card != null) CardViewRegistry.Unregister(Card, this);
        }

        // --------- API ---------

        public void Bind(Card card, bool faceDown = false)
        {
            if (Card != null) CardViewRegistry.Unregister(Card, this);
            Card = card;
            isFaceDown = faceDown;
            CardViewRegistry.Register(Card, this);
            Redraw();
        }

        public void SetFaceDown(bool v)
        {
            isFaceDown = v;
            Redraw();
        }

        public void Flip()
        {
            isFaceDown = !isFaceDown;
            Redraw();
        }

        public void Rotate90(bool clockwise)
        {
            var rt = transform as RectTransform;
            if (!rt) return;
            var z = rt.localEulerAngles.z + (clockwise ? -90f : 90f);
            rt.localEulerAngles = new Vector3(rt.localEulerAngles.x, rt.localEulerAngles.y, z);
        }

        public void Highlight(bool on)
        {
            if (!highlightFx) return;
            highlightFx.alpha = on ? 1f : 0f;
        }

        public void Redraw()
        {
            // Face-down handling
            if (faceDownBack) faceDownBack.SetActive(isFaceDown);

            if (Card == null)
            {
                if (nameText) nameText.text = "(no card)";
                if (statsText) statsText.text = "";
                if (artImage) artImage.sprite = null;
                if (frameImage) frameImage.enabled = false;
                return;
            }

            // Name
            var displayName = (_stats != null) ? _stats.GetDisplayName(Card) : Card.Name;
            if (nameText) nameText.text = string.IsNullOrEmpty(displayName) ? Card.Name : displayName;

            // Stats/type line
            if (_stats != null &&
                _stats.TryGetStats(Card, out int atk, out int def, out int level, out string typeLine))
            {
                if (statsText)
                {
                    if (atk >= 0 && def >= 0)
                        statsText.text = $"Lv{level}  ATK {atk} / DEF {def}";
                    else
                        statsText.text = typeLine ?? "";
                }
            }
            else if (statsText) statsText.text = "";

            // Art & frame (hidden if face-down)
            if (artImage)
            {
                artImage.enabled = !isFaceDown;
                artImage.sprite = !isFaceDown && _art != null ? _art.GetArt(Card) : null;
            }

            if (frameImage)
            {
                var frame = _art != null ? _art.GetFrame(Card) : null;
                frameImage.enabled = !isFaceDown && frame != null;
                frameImage.sprite = frame;

            }
        }
    }
}