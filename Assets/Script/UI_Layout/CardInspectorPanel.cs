// CardInspectorPanel.cs
// Shows a focused card’s name, effect text, rulings, and once-per-turn flags.
// Subscribes to CardView hover events; can also be driven manually via Show(card).

using System.Text;
using UnityEngine;
using UnityEngine.UI;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using Card = YGO.Duel.Cards.Card;

namespace YGO.Duel.UI
{
    public interface ICardInfoProvider
    {
        // Provide all inspector data (return null for anything unknown)
        CardInfo GetInfo(Card card);
    }

    public sealed class CardInfo
    {
        public string DisplayName;
        public string TypeLine;        // e.g., "Dragon/Effect"
        public string EffectText;      // full effect/rules text
        public string[] Rulings;       // bullet rulings
        public bool OncePerTurn;
        public string OnceScope;       // "Hard OPT", "Soft OPT", etc.
        public int ATK = -1, DEF = -1, Level = 0;
        public Sprite Art;
    }

    public sealed class CardInspectorPanel : MonoBehaviour
    {
        [Header("UI")]
        public Image  art;
        public Text   nameText;
        public Text   typeText;
        public Text   statsText;
        public Text   effectText;
        public Text   rulingsText;
        public Text   optText; // once-per-turn marker
        public GameObject panelRoot;

        [Header("Behavior")]
        public bool followHover = true;

        private ICardInfoProvider _provider;
        private DuelLogger _logger;

        private void Awake()
        {
            ServiceLocator.TryGet(out _provider);
            ServiceLocator.TryGet(out _logger);

            if (followHover)
            {
                CardView.OnAnyHoverEnter += HandleHoverEnter;
                CardView.OnAnyHoverExit  += HandleHoverExit;
            }
        }

        private void OnDestroy()
        {
            if (followHover)
            {
                CardView.OnAnyHoverEnter -= HandleHoverEnter;
                CardView.OnAnyHoverExit  -= HandleHoverExit;
            }
        }

        private void HandleHoverEnter(CardView view)
        {
            if (!followHover || view == null) return;
            Show(view.Card);
        }

        private void HandleHoverExit(CardView view)
        {
            // optional: keep last shown; or clear
            // Clear();
        }

        public void Show(Card card)
        {
            if (!panelRoot) return;
            panelRoot.SetActive(true);

            if (card == null) { Clear(); return; }

            CardInfo info = _provider != null ? _provider.GetInfo(card) : null;

            if (art)       art.sprite     = info?.Art;
            if (art)       art.enabled    = art.sprite != null;

            if (nameText)  nameText.text  = info?.DisplayName ?? card.Name;
            if (typeText)  typeText.text  = info?.TypeLine ?? "";
            if (statsText) statsText.text = (info != null && info.ATK >= 0 && info.DEF >= 0)
                                            ? $"Lv{info.Level}  ATK {info.ATK} / DEF {info.DEF}"
                                            : "";

            if (effectText) effectText.text = info?.EffectText ?? "(no effect text)";

            if (rulingsText)
            {
                if (info?.Rulings != null && info.Rulings.Length > 0)
                {
                    var sb = new StringBuilder();
                    foreach (var r in info.Rulings) sb.Append("• ").AppendLine(r);
                    rulingsText.text = sb.ToString();
                }
                else rulingsText.text = "";
            }

            if (optText)
            {
                if (info != null && info.OncePerTurn)
                {
                    optText.gameObject.SetActive(true);
                    optText.text = string.IsNullOrEmpty(info.OnceScope) ? "Once per turn" : info.OnceScope;
                }
                else optText.gameObject.SetActive(false);
            }
        }

        public void Clear()
        {
            if (!panelRoot) return;
            if (art) { art.sprite = null; art.enabled = false; }
            if (nameText) nameText.text = "";
            if (typeText) typeText.text = "";
            if (statsText) statsText.text = "";
            if (effectText) effectText.text = "";
            if (rulingsText) rulingsText.text = "";
            if (optText) optText.gameObject.SetActive(false);
        }
    }
}
