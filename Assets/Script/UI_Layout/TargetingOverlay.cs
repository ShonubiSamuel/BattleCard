// TargetingOverlay.cs
// Highlights legal targets and lets the user confirm/cancel a selection.
// Works via CardViewRegistry to map runtime cards -> CardView instances.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using Card = YGO.Duel.Cards.Card;

namespace YGO.Duel.UI
{
    public sealed class TargetingOverlay : MonoBehaviour
    {
        [Header("UI")]
        public GameObject highlightPrefab; // e.g., a glow ring; parented under CardView
        public Button confirmButton;
        public Button cancelButton;
        public Text   headerText;

        [Header("Selection Rules")]
        public int minTargets = 1;
        public int maxTargets = 1;
        public bool allowRetarget = true;

        // active session state
        private readonly Dictionary<CardView, GameObject> _highlights = new();
        private readonly List<Card> _legal = new();
        private readonly HashSet<CardView> _selected = new();

        public event Action<List<Card>> OnConfirm;
        public event Action OnCancel;

        private void Awake()
        {
            if (confirmButton) confirmButton.onClick.AddListener(Confirm);
            if (cancelButton)  cancelButton.onClick.AddListener(Cancel);
        }

        private void OnDisable() => ClearAll();

        public void Begin(IEnumerable<Card> legalTargets, string prompt = "Select target")
        {
            ClearAll();
            gameObject.SetActive(true);

            if (headerText) headerText.text = prompt ?? "Select target";
            if (legalTargets != null) _legal.AddRange(legalTargets);

            foreach (var card in _legal)
            {
                if (!CardViewRegistry.TryGet(card, out var cv) || cv == null) continue;

                // highlight fx
                if (highlightPrefab != null)
                {
                    var hi = Instantiate(highlightPrefab, cv.transform);
                    _highlights[cv] = hi;
                }
                // tap to select
                CardView.OnAnyClicked += HandleCardClicked;
                cv.Highlight(true);
            }

            RefreshButtons();
        }

        public void End()
        {
            ClearAll();
            gameObject.SetActive(false);
        }

        private void ClearAll()
        {
            CardView.OnAnyClicked -= HandleCardClicked;
            foreach (var kv in _highlights)
                if (kv.Value) Destroy(kv.Value);
            foreach (var cv in _highlights.Keys)
                if (cv) cv.Highlight(false);

            _highlights.Clear();
            _selected.Clear();
            _legal.Clear();
        }

        private void HandleCardClicked(CardView cv)
        {
            if (cv == null || !_highlights.ContainsKey(cv)) return;

            if (_selected.Contains(cv))
            {
                if (allowRetarget) _selected.Remove(cv);
            }
            else
            {
                if (_selected.Count < maxTargets)
                    _selected.Add(cv);
            }
            RefreshButtons();
        }

        private void RefreshButtons()
        {
            if (confirmButton)
                confirmButton.interactable = _selected.Count >= minTargets && _selected.Count <= maxTargets;

            if (headerText)
                headerText.text = $"{_selected.Count}/{maxTargets} selected";
        }

        private void Confirm()
        {
            var outList = new List<Card>(_selected.Count);
            foreach (var cv in _selected)
                if (cv && cv.Card != null) outList.Add(cv.Card);

            OnConfirm?.Invoke(outList);
            End();
        }

        private void Cancel()
        {
            OnCancel?.Invoke();
            End();
        }
    }
}
