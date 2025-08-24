// ZoneSelectionPanel.cs  (stub you can flesh out later)
using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class ZoneSelectionPanel : MonoBehaviour
{
    public CanvasGroup rootGroup;
    public Button[] monsterZoneButtons; // 0..N
    public Button cancelBtn;

    private Action<int> _onChosen;
    private IDisposable _lock;

    private void Awake()
    {
        HideImmediate();
        if (cancelBtn) cancelBtn.onClick.AddListener(Hide);
        for (int i = 0; i < monsterZoneButtons.Length; i++)
        {
            int idx = i;
            monsterZoneButtons[i].onClick.AddListener(() => { _onChosen?.Invoke(idx); Hide(); });
        }
    }

    public void Show(Action<int> onChosen, bool[] interactableMask)
    {
        _onChosen = onChosen;
        _lock?.Dispose();
        _lock = InputLockService.Acquire();

        for (int i = 0; i < monsterZoneButtons.Length; i++)
            monsterZoneButtons[i].interactable = (i < interactableMask.Length) && interactableMask[i];

        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
        _onChosen = null;
        _lock?.Dispose();
        _lock = null;
    }

    private void HideImmediate() => SetVisible(false);

    private void SetVisible(bool on)
    {
        if (rootGroup)
        {
            rootGroup.alpha = on ? 1 : 0;
            rootGroup.blocksRaycasts = on;
            rootGroup.interactable = on;
        }
        else gameObject.SetActive(on);
    }
}