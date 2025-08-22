// LogPanel.cs
// Streams DuelLogger entries into a scrollable text area with basic filtering.

using System.Text;
using UnityEngine;
using UnityEngine.UI;
using YGO.Duel.Foundation;
using TMPro;

namespace YGO.Duel.UI
{
    public sealed class LogPanel : MonoBehaviour
    {
        [Header("UI")]
        public TextMeshProUGUI logText;
        public ScrollRect scrollRect;
        public TMP_InputField filterInput;     // optional: filter by substring in Type/Summary
        public Toggle autoScrollToggle;    // if true, stick to bottom on new logs
        public Button clearButton;

        private DuelLogger _logger;
        private StringBuilder _sb = new StringBuilder(8192);
        private string _filter = "";

        private void Awake()
        {
            ServiceLocator.TryGet(out _logger);
            if (_logger != null) _logger.OnLogged += HandleLogged;

            if (filterInput) filterInput.onValueChanged.AddListener(SetFilter);
            if (clearButton) clearButton.onClick.AddListener(Clear);
        }

        private void OnDestroy()
        {
            if (_logger != null) _logger.OnLogged -= HandleLogged;
        }

        private void Start() => RebuildFromHistory();

        public void SetFilter(string f)
        {
            _filter = f ?? "";
            RebuildFromHistory();
        }

        private void RebuildFromHistory()
        {
            if (logText == null) return;
            _sb.Clear();
            if (_logger != null)
            {
                foreach (var e in _logger.Entries)
                    AppendIfPass(e);
            }
            logText.text = _sb.ToString();
            ScrollToBottom();
        }

        private void HandleLogged(DuelLogger.LogEntry e)
        {
            if (logText == null) return;
            AppendIfPass(e, appendToExisting:true);
            if (autoScrollToggle && autoScrollToggle.isOn) ScrollToBottom();
        }

        private void AppendIfPass(DuelLogger.LogEntry e, bool appendToExisting = false)
        {
            if (!string.IsNullOrEmpty(_filter))
            {
                if (!( (e.Type?.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                    || (e.Summary?.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ))
                    return;
            }

            _sb.AppendLine(e.ToString());
            if (appendToExisting) logText.text += e.ToString() + "\n";
        }

        private void ScrollToBottom()
        {
            if (scrollRect == null) return;
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }

        public void Clear()
        {
            _sb.Clear();
            if (logText) logText.text = "";
        }
    }
}
