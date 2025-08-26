using UnityEngine;
using UnityEngine.UI;

public sealed class UIInputBlocker : MonoBehaviour
{
    private static UIInputBlocker _instance;

    [Tooltip("Full-screen Image used only to catch raycasts; color alpha can be 0.")]
    public Image raycatcher; // set in Inspector (full-screen)

    private void Awake()
    {
        _instance = this;
        SetBlocked(false);
    }

    public static void SetBlocked(bool on)
    {
        if (_instance == null) return;
        if (_instance.raycatcher != null)
        {
            _instance.raycatcher.raycastTarget = on; // ← actually intercepts all UI clicks beneath
            _instance.raycatcher.gameObject.SetActive(true); // keep active; only raycastTarget toggles
            var cg = _instance.raycatcher.GetComponent<CanvasGroup>();
            if (cg) cg.blocksRaycasts = on; // belt and suspenders
        }
    }
}