using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Card = YGO.Duel.Cards.Card;

public sealed class SummonChoicePopup : MonoBehaviour
{
    [Header("Wiring")]
    public Canvas rootCanvas;         // screen-space canvas (optional but recommended)
    public CanvasGroup rootGroup;     // fades/interactable
    public RectTransform panel;       // the window

    [Header("Labels")]
    public TMP_Text cardNameLabel;
    public TMP_Text normalWhyLabel;
    public TMP_Text setWhyLabel;

    [Header("Buttons")]
    public Button normalSummonBtn;
    public Button setMonsterBtn;

    [Header("Backdrop")]
    [Tooltip("Full-screen transparent button under the panel. Clicking it dismisses the popup.")]
    public Button backdropButton;     // <- assign a full-screen Button (with Image alpha 0)

    private IDisposable _lock;        // InputLock token
    private Action _onNormal, _onSet;
    private bool _showing;

    private void Awake()
    {
        HideImmediate();
        if (backdropButton)
        {
            backdropButton.onClick.RemoveAllListeners();
            backdropButton.onClick.AddListener(Hide);
            backdropButton.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        _lock?.Dispose();
        _lock = null;
    }

    /// <summary>
    /// Opens the popup near <paramref name="screenPos"/> (if provided) with the two choices.
    /// </summary>
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

        // Buttons & callbacks
        if (normalSummonBtn)
        {
            normalSummonBtn.gameObject.SetActive(canNormal);
            normalSummonBtn.interactable = canNormal;
            normalSummonBtn.onClick.RemoveAllListeners();
            if (canNormal) normalSummonBtn.onClick.AddListener(() => { _onNormal?.Invoke(); Hide(); });
        }

        if (setMonsterBtn)
        {
            setMonsterBtn.gameObject.SetActive(canSet);
            setMonsterBtn.interactable = canSet;
            setMonsterBtn.onClick.RemoveAllListeners();
            if (canSet) setMonsterBtn.onClick.AddListener(() => { _onSet?.Invoke(); Hide(); });
        }

        // Tooltips/why labels
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

        // If nothing to do, do not open (and don’t lock)
        bool any = (normalSummonBtn && normalSummonBtn.gameObject.activeSelf)
                || (setMonsterBtn  && setMonsterBtn .gameObject.activeSelf);
        if (!any) { HideImmediate(); return; }

        // Position near click
        if (screenPos.HasValue && panel)
        {
            if (rootCanvas && rootCanvas.renderMode != RenderMode.WorldSpace)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)rootCanvas.transform, screenPos.Value, rootCanvas.worldCamera, out var local);
                panel.anchoredPosition = local;
            }
            else
            {
                panel.position = screenPos.Value;
            }
        }

        // Acquire input lock; enable full-screen backdrop so outside clicks dismiss and don’t leak through
        _lock?.Dispose();
        _lock = InputLockService.Acquire();
        if (backdropButton) backdropButton.gameObject.SetActive(true);

        _showing = true;
        SetVisible(true);
    }

    public void Hide()
    {
        if (!_showing) return;
        _showing = false;

        if (backdropButton) backdropButton.gameObject.SetActive(false);
        SetVisible(false);

        _onNormal = null;
        _onSet    = null;

        _lock?.Dispose();
        _lock = null;
    }

    private void HideImmediate()
    {
        _showing = false;
        if (backdropButton) backdropButton.gameObject.SetActive(false);
        SetVisible(false);
        _onNormal = _onSet = null;
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
