// PromptModal.cs
// Simple modal for Yes/No, choose count, or pick options. Buttons are generated dynamically.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YGO.Duel.UI
{
    public sealed class PromptModal : MonoBehaviour
    {
        [Header("UI")]
        public GameObject panelRoot;
        public Text titleText;
        public Text bodyText;
        public Transform buttonsParent;
        public Button buttonPrefab;

        private readonly List<Button> _spawned = new();

        private void OnDisable() => ClearButtons();

        private void ClearButtons()
        {
            foreach (var b in _spawned) if (b) Destroy(b.gameObject);
            _spawned.Clear();
        }

        private Button MakeButton(string label, Action onClick)
        {
            var b = Instantiate(buttonPrefab, buttonsParent);
            var t = b.GetComponentInChildren<Text>();
            if (t) t.text = label;
            b.onClick.AddListener(() => { onClick?.Invoke(); Hide(); });
            _spawned.Add(b);
            return b;
        }

        private void Show(string title, string body)
        {
            if (panelRoot) panelRoot.SetActive(true);
            if (titleText) titleText.text = title ?? "";
            if (bodyText)  bodyText.text  = body ?? "";
        }

        public void Hide()
        {
            ClearButtons();
            if (panelRoot) panelRoot.SetActive(false);
        }

        // --------- Scenarios ---------

        public void AskYesNo(string title, string body, Action<bool> onResult)
        {
            ClearButtons();
            Show(title, body);
            MakeButton("Yes", () => onResult?.Invoke(true));
            MakeButton("No",  () => onResult?.Invoke(false));
        }

        public void ChooseCount(string title, string body, int min, int max, Action<int> onChosen)
        {
            ClearButtons();
            Show(title, body);
            for (int i = min; i <= max; i++)
            {
                int capture = i;
                MakeButton(capture.ToString(), () => onChosen?.Invoke(capture));
            }
            MakeButton("Cancel", () => onChosen?.Invoke(-1));
        }

        public void SelectOptions(string title, string body, IList<string> options, Action<int> onIndexChosen)
        {
            ClearButtons();
            Show(title, body);
            if (options != null)
            {
                for (int i = 0; i < options.Count; i++)
                {
                    int idx = i;
                    MakeButton(options[i], () => onIndexChosen?.Invoke(idx));
                }
            }
            MakeButton("Cancel", () => onIndexChosen?.Invoke(-1));
        }
    }
}
