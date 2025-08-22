// BanishedView.cs
// Shows Banished count and a simple popup list.

using System.Text;
using UnityEngine;
using UnityEngine.UI;
using YGO.Duel.Board;
using YGO.Duel.Foundation;

public sealed class BanishedView : MonoBehaviour
{
    public BoardManager.Seat seat = BoardManager.Seat.P1;

    [Header("UI")]
    public Button openButton;   // shows "Banished (N)"
    public GameObject popupPanel;
    public Text popupText;

    private BoardManager _board;
    private DuelLogger   _logger;

    private void Awake()
    {
        ServiceLocator.TryGet(out _board);
        ServiceLocator.TryGet(out _logger);

        if (openButton) openButton.onClick.AddListener(TogglePopup);

        if (_logger != null)
            _logger.OnLogged += _ => Refresh();
    }

    private void OnDestroy()
    {
        if (_logger != null)
            _logger.OnLogged -= _ => Refresh();
    }

    private void Start() => Refresh();

    private void TogglePopup()
    {
        if (!popupPanel) return;
        popupPanel.SetActive(!popupPanel.activeSelf);
        if (popupPanel.activeSelf) FillPopup();
    }

    public void Refresh()
    {
        if (_board == null || openButton == null) return;
        int count = _board.Zones[(int)seat].Banished.Count;
        var txt = openButton.GetComponentInChildren<Text>();
        if (txt) txt.text = $"Banished ({count})";
        if (popupPanel && popupPanel.activeSelf) FillPopup();
    }

    private void FillPopup()
    {
        if (_board != null || !popupText) return;
        var ban = _board.Zones[(int)seat].Banished;
        var sb = new StringBuilder();
        for (int i = 0; i < ban.Count; i++)
        {
            var card = ((YGO.Duel.Zones.ListZoneBase)ban).RawList[i];
            sb.AppendLine(card.IsFaceDownBanished ? "(Face-down)" : card.Name);
        }
        popupText.text = sb.ToString();
    }
}