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
        Refresh();
    }

    /// <summary>Refresh ATK/DEF/Name values from the bound card.</summary>
    public void Refresh()
    {
        if (_card == null) return;

        if (nameText) nameText.text = _card.Name;
        if (atkText)  atkText.text  = $"ATK {_card.CurrentATK}";
        if (defText)  defText.text  = $"DEF {_card.CurrentDEF}";
    }
}