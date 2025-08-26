using UnityEngine;

using YGO.Duel.Cards;
using TMPro;

[DisallowMultipleComponent]
public sealed class MonsterWorldUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI atkText;
    public TextMeshProUGUI defText;

    private Card _card;

    /// <summary>Bind this UI to a runtime Card.</summary>
    public void Bind(Card card)
    {
        _card = card;
        UpdateStats( card.Name,
            card.CurrentATK,
            card.CurrentDEF,
            card.IsFaceUp);
    }

    /// <summary>Refresh ATK/DEF/Name values from the bound card.</summary>
    public void UpdateStats(string name, int atk, int def, bool isFaceUp)
    {
        if (!isFaceUp)
        {
            nameText.text = "";
            atkText.text  = "";
            defText.text  = "";
        }
        else
        {
            nameText.text = name;
            atkText.text  = $"ATK {atk}";
            defText.text  = $"DEF {def}";
        }
    }
}