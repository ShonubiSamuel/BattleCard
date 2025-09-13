// Assets/Script/Runtime/Duel/UI/TargetPickerOverlay.cs
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YGO.Duel.Cards;
using YGO.Duel.Foundation;

public sealed class TargetPickerOverlay : MonoBehaviour
{
    [Header("Wiring")]
    public CanvasGroup root;
    public TMP_Text headerLabel;      // e.g., "Select 1 opponent monster (0/1)"
    public Button confirmBtn;         // enabled when min met
    public Button cancelBtn;          // cancels selection
    public Camera rayCamera;
    public LayerMask cardLayer = ~0;

    public struct Request
    {
        public int Min, Max;
        public Func<Card, bool> Filter;
        public string Label;

        public Request(int min, int max, Func<Card,bool> filter, string label)
        { Min=min; Max=max; Filter=filter; Label=label; }
    }

    private Request _req;
    private readonly List<Card> _picked = new();
    private readonly HashSet<Card3DView> _highlight = new();

    private Action<List<Card>> _onDone;
    private Action _onCancel;
    private IDisposable _lock;

    private DuelLogger _log;

    void Awake()
    {
        ServiceLocator.TryGet(out _log);
        if (cancelBtn) cancelBtn.onClick.AddListener(Cancel);
        if (confirmBtn) confirmBtn.onClick.AddListener(Confirm);
        HideImmediate();
    }

    public void Show(Request request, Action<List<Card>> onDone, Action onCancel)
    {
        _req = request;
        _picked.Clear();
        ClearHighlights();

        _onDone = onDone;
        _onCancel = onCancel;

        UpdateHeader();

        _lock?.Dispose();
        _lock = InputLockService.Acquire();

        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
        _onDone = null; _onCancel = null;
        _picked.Clear();
        ClearHighlights();

        _lock?.Dispose(); _lock = null;
    }

    void Update()
    {
        if (!root || root.alpha <= 0.01f) return;

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
        if (Input.GetMouseButtonDown(0)) TryClick(Input.mousePosition);
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            TryClick(Input.GetTouch(0).position);
#endif
    }

    private void TryClick(Vector2 screen)
    {
        if (!rayCamera) rayCamera = Camera.main;
        if (!rayCamera) return;

        Ray r = rayCamera.ScreenPointToRay(screen);
        if (!Physics.Raycast(r, out var hit, 200f, cardLayer)) return;

        var view = hit.collider.GetComponentInParent<Card3DView>();
        var card = view != null ? view.BoundCard : null;
        if (card == null) return;

        // Filter
        if (_req.Filter != null && !_req.Filter(card)) return;

        // Toggle selection
        if (_picked.Contains(card))
        {
            _picked.Remove(card);
            MarkHighlight(view, false);
        }
        else
        {
            if (_picked.Count >= _req.Max) return;
            _picked.Add(card);
            MarkHighlight(view, true);
        }

        UpdateHeader();
    }

    private void UpdateHeader()
    {
        if (headerLabel)
        {
            var baseText = string.IsNullOrEmpty(_req.Label) ? "Select targets" : _req.Label;
            headerLabel.text = $"{baseText} ({_picked.Count}/{_req.Max})";
        }
        if (confirmBtn) confirmBtn.interactable = _picked.Count >= _req.Min;
    }

    private void Confirm()
    {
        if (_picked.Count < _req.Min) return;
        _onDone?.Invoke(new List<Card>(_picked));
        Hide();
    }

    private void Cancel()
    {
        _onCancel?.Invoke();
        Hide();
    }

    private void MarkHighlight(Card3DView v, bool on)
    {
        if (!v) return;
        if (on)
        {
            if (_highlight.Add(v)) v.SetHighlighted(true);
        }
        else
        {
            if (_highlight.Remove(v)) v.SetHighlighted(false);
        }
    }

    private void ClearHighlights()
    {
        foreach (var v in _highlight) if (v) v.SetHighlighted(false);
        _highlight.Clear();
    }

    private void HideImmediate()
    {
        if (root) { root.alpha = 0; root.blocksRaycasts = false; root.interactable = false; }
        else gameObject.SetActive(false);
    }
    private void SetVisible(bool v)
    {
        if (root)
        {
            root.alpha = v ? 1 : 0;
            root.blocksRaycasts = v;
            root.interactable = v;
        }
        else gameObject.SetActive(v);
    }
}