// SummonChoicePopup.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Card = YGO.Duel.Cards.Card;

public sealed class SummonChoicePopup : MonoBehaviour
{
    [Header("Wiring")]
    public CanvasGroup rootGroup;
    public RectTransform panel;

    [Header("Labels")]
    public TMP_Text cardNameLabel;

    [Header("Buttons")]
    public Button normalSummonBtn;
    public Button setMonsterBtn;
    public Button cancelBtn;

    [Header("Why/Tooltip (optional)")]
    public TMP_Text normalWhyLabel;
    public TMP_Text setWhyLabel;

    private IDisposable _lock;
    private Action _onNormal, _onSet;
    private bool _showing;

    private void Awake()
    {
        HideImmediate();
        if (cancelBtn) cancelBtn.onClick.AddListener(Hide);
    }

    private void OnDestroy()
    {
        _lock?.Dispose();
        _lock = null;
    }

    public void Show(
        Card card,
        bool canNormal, string normalWhy,
        bool canSet,    string setWhy,
        Action onNormal,
        Action onSet,
        Vector2? screenPos = null)
    {
        _onNormal = onNormal;
        _onSet    = onSet;

        if (cardNameLabel) cardNameLabel.text = card?.Name ?? "(Card)";

        if (normalSummonBtn)
        {
            normalSummonBtn.gameObject.SetActive(canNormal); // hide if not applicable
            normalSummonBtn.interactable = canNormal;
            normalSummonBtn.onClick.RemoveAllListeners();
            normalSummonBtn.onClick.AddListener(() =>
            {
                _onNormal?.Invoke();
                Hide();
            });
        }

        if (setMonsterBtn)
        {
            setMonsterBtn.gameObject.SetActive(canSet);
            setMonsterBtn.interactable = canSet;
            setMonsterBtn.onClick.RemoveAllListeners();
            setMonsterBtn.onClick.AddListener(() =>
            {
                _onSet?.Invoke();
                Hide();
            });
        }

        if (normalWhyLabel)
        {
            normalWhyLabel.gameObject.SetActive(!canNormal && !string.IsNullOrEmpty(normalWhy));
            normalWhyLabel.text = normalWhy ?? "";
        }

        if (setWhyLabel)
        {
            setWhyLabel.gameObject.SetActive(!canSet && !string.IsNullOrEmpty(setWhy));
            setWhyLabel.text = setWhy ?? "";
        }

        // Lock board input
        _lock?.Dispose();
        _lock = InputLockService.Acquire();

        // Place near click (optional)
        if (screenPos.HasValue && panel)
        {
            Vector2 pos = screenPos.Value;
            panel.position = pos;
        }

        // Show
        _showing = true;
        SetVisible(true);
    }

    public void Hide()
    {
        if (!_showing) return;
        _showing = false;
        SetVisible(false);
        _onNormal = null;
        _onSet = null;

        _lock?.Dispose();
        _lock = null;
    }

    private void HideImmediate()
    {
        _showing = false;
        SetVisible(false);
    }

    private void SetVisible(bool on)
    {
        if (rootGroup)
        {
            rootGroup.alpha = on ? 1f : 0f;
            rootGroup.blocksRaycasts = on;
            rootGroup.interactable = on;
        }
        else
        {
            gameObject.SetActive(on);
        }
    }
}