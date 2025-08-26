using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Card = YGO.Duel.Cards.Card;

public sealed class SummonContextPanel : MonoBehaviour
{
    [Header("Wiring")]
    public Canvas rootCanvas;
    public RectTransform panel;
    public Button flipSummonBtn;
    public Button toAttackBtn;
    public Button toDefenseBtn;
    [Tooltip("Full-screen transparent button behind the panel to dismiss on any click.")]
    public Button backdropButton;
    public TMP_Text titleLabel;

    // internal
    private IDisposable _lock;
    private Action _onFlip, _onToAtk, _onToDef;

    void Awake()
    {
        HideImmediate();
        if (backdropButton)
        {
            backdropButton.onClick.RemoveAllListeners();
            backdropButton.onClick.AddListener(Close);
        }
    }

    private void OnDestroy()
    {
        _lock?.Dispose();
        _lock = null;
    }

    public void ShowFor(Card card, Vector2 screenPos,
                        bool showFlip,
                        bool showToAtk,
                        bool showToDef,
                        Action onFlip,
                        Action onToAtk,
                        Action onToDef,
                        Action onCancel = null) // kept for signature compat; unused
    {
        _onFlip  = onFlip;
        _onToAtk = onToAtk;
        _onToDef = onToDef;

        if (titleLabel) titleLabel.text = card?.Name ?? "Monster";

        // Toggle buttons + wire
        if (flipSummonBtn)
        {
            flipSummonBtn.gameObject.SetActive(showFlip);
            flipSummonBtn.onClick.RemoveAllListeners();
            if (showFlip) flipSummonBtn.onClick.AddListener(() => { _onFlip?.Invoke(); Close(); });
        }
        if (toAttackBtn)
        {
            toAttackBtn.gameObject.SetActive(showToAtk);
            toAttackBtn.onClick.RemoveAllListeners();
            if (showToAtk) toAttackBtn.onClick.AddListener(() => { _onToAtk?.Invoke(); Close(); });
        }
        if (toDefenseBtn)
        {
            toDefenseBtn.gameObject.SetActive(showToDef);
            toDefenseBtn.onClick.RemoveAllListeners();
            if (showToDef) toDefenseBtn.onClick.AddListener(() => { _onToDef?.Invoke(); Close(); });
        }

        // If no visible option → don't open
        bool any =
            (flipSummonBtn && flipSummonBtn.gameObject.activeSelf) ||
            (toAttackBtn   && toAttackBtn.gameObject.activeSelf)   ||
            (toDefenseBtn  && toDefenseBtn.gameObject.activeSelf);
        if (!any) { HideImmediate(); return; }

        // Position
        var rt = (RectTransform)panel;
        if (rootCanvas && rootCanvas.renderMode != RenderMode.WorldSpace)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)rootCanvas.transform, screenPos, rootCanvas.worldCamera, out var local);
            rt.anchoredPosition = local;
        }
        else { rt.position = screenPos; }

        // Lock inputs + show a global raycatcher
        _lock?.Dispose();
        _lock = InputLockService.Acquire();

        // Show
        if (backdropButton) backdropButton.gameObject.SetActive(true);
        gameObject.SetActive(true);
    }

    public void Close()
    {
        HideImmediate();
        _lock?.Dispose();
        _lock = null;
    }

    private void HideImmediate()
    {
        if (backdropButton) backdropButton.gameObject.SetActive(false);
        gameObject.SetActive(false);
        _onFlip = _onToAtk = _onToDef = null;
    }
}
