using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YGO.Duel.Board;

namespace YGO.Duel.UI
{
    /// <summary>
    /// Simple ZoneView component you can place on your zone UI elements.
    /// Alternatively, implement IZoneView on your own class and remove this.
    /// </summary>
    public class ZoneView : MonoBehaviour, IZoneView
    {
        public BoardManager.Seat seat = BoardManager.Seat.P1;
        public BoardManager.CardZone kind = BoardManager.CardZone.Monster;
        public int index = 0; // used for MZ/ST/Pendulum

        public Image highlight; // optional soft highlight

        public BoardManager.ZoneId GetZoneId() => new BoardManager.ZoneId(seat, kind, index);

        // public void OnPointerEnter(PointerEventData eventData) { if (highlight) highlight.enabled = true; }
        // public void OnPointerExit(PointerEventData eventData)  { if (highlight) highlight.enabled = false; }
    }

}