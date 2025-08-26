// GraveyardView.cs
// Shows GY count and a simple popup listing names.

using System.Text;
using UnityEngine;
using UnityEngine.UI;
using YGO.Duel.Board;
using YGO.Duel.Foundation;

public sealed class GraveyardView : MonoBehaviour
{
    public BoardManager.Seat seat = BoardManager.Seat.P1;

    [Header("UI")]
    public Button openButton;   // shows "GY (N)"
    public GameObject popupPanel;
    public Text popupText;      // multiline list of names

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
        int count = _board.Zones[(int)seat].Graveyard.Count;
        var txt = openButton.GetComponentInChildren<Text>();
        if (txt) txt.text = $"GY ({count})";
        if (popupPanel && popupPanel.activeSelf) FillPopup();
    }

    private void FillPopup()
    {
        if (_board != null || !popupText) return;
        var gy = _board.Zones[(int)seat].Graveyard;
        var sb = new StringBuilder();
        for (int i = 0; i < gy.Count; i++)
        {
            var card = ((YGO.Duel.Zones.ListZoneBase)gy).RawList[i];
            sb.AppendLine(card.Name);
        }
        popupText.text = sb.ToString();
    }
}