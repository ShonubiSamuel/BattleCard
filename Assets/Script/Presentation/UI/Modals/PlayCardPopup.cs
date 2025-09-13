// PlayCardPopup.cs  (new; can replace SummonChoicePopup on the prefab)
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Card = YGO.Duel.Cards.Card;

public sealed class PlayCardPopup : MonoBehaviour
{
    [Header("Root")]
    public CanvasGroup root;
    public Button backdrop;

    [Header("Common")]
    public TMP_Text cardName;

    [Header("Monster Tab")]
    public GameObject monsterPanel;            // group
    public Button normalBtn;
    public TMP_Text normalWhy;
    public Button setBtn;
    public TMP_Text setWhy;

    [Header("Spell/Trap Tab")]
    public GameObject stPanel;                 // group
    public Button activateBtn;
    public TMP_Text activateWhy;
    public Button setStBtn;
    public TMP_Text setStWhy;

    private Action _onNormal, _onSetM, _onActivate, _onSetST;

    void Awake()
    {
        HideImmediate();
        if (backdrop)
        {
            backdrop.onClick.RemoveAllListeners();
            backdrop.onClick.AddListener(Hide);
        }
    }

    public void ShowMonster(Card c,
        bool canNormal, string whyNormal,
        bool canSet,    string whySet,
        Action onNormal, Action onSet,
        Vector2? screen = null)
    {
        SetupCommon(c, screen);
        monsterPanel?.SetActive(true);
        stPanel?.SetActive(false);

        WireButton(normalBtn, canNormal, onNormal);
        WriteWhy(normalWhy, canNormal, whyNormal);

        WireButton(setBtn, canSet, onSet);
        WriteWhy(setWhy, canSet, whySet);

        OpenIfAny();
    }

    public void ShowSpellTrap(Card c,
        bool canActivate, string whyActivate,
        bool canSet,      string whySet,
        Action onActivate, Action onSet,
        Vector2? screen = null)
    {
        SetupCommon(c, screen);
        monsterPanel?.SetActive(false);
        stPanel?.SetActive(true);

        WireButton(activateBtn, canActivate, onActivate);
        WriteWhy(activateWhy, canActivate, whyActivate);

        WireButton(setStBtn, canSet, onSet);
        WriteWhy(setStWhy, canSet, whySet);

        OpenIfAny();
    }

    public void Hide()
    {
        root.alpha = 0; root.blocksRaycasts = false; root.interactable = false;
        if (backdrop) backdrop.gameObject.SetActive(false);
        _onNormal = _onSetM = _onActivate = _onSetST = null;
    }

    private void HideImmediate() => Hide();

    private void SetupCommon(Card c, Vector2? screen)
    {
        if (cardName) cardName.text = c?.Name ?? "(Card)";
        if (backdrop) backdrop.gameObject.SetActive(true);
        if (root) { root.alpha = 1; root.blocksRaycasts = true; root.interactable = true; }

        // (Optional) position with your existing canvas logic if needed.
    }

    private void WireButton(Button b, bool can, Action on)
    {
        if (!b) return;
        b.gameObject.SetActive(can);
        b.interactable = can;
        b.onClick.RemoveAllListeners();
        if (can && on != null) b.onClick.AddListener(() => { on(); Hide(); });
    }

    private void WriteWhy(TMP_Text label, bool can, string why)
    {
        if (!label) return;
        label.gameObject.SetActive(!can && !string.IsNullOrEmpty(why));
        label.text = !can ? why : "";
    }

    private void OpenIfAny()
    {
        // If no visible buttons, close immediately.
        bool any =
            (normalBtn && normalBtn.gameObject.activeSelf) ||
            (setBtn    && setBtn.gameObject.activeSelf)    ||
            (activateBtn && activateBtn.gameObject.activeSelf) ||
            (setStBtn    && setStBtn.gameObject.activeSelf);

        if (!any) Hide();
    }
}