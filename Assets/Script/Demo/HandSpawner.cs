using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.UI;

public class HandSpawner : MonoBehaviour
{
    public CardView cardViewPrefab;
    public RectTransform content; // add Horizontal/Vertical/Grid Layout Group if you like

    void Start()
    {
        var board = YGO.Duel.Foundation.ServiceLocator.Get<BoardManager>();
        var hand = board.Zones[(int)BoardManager.Seat.P1].Hand.RawList;

        foreach (var c in hand)
        {
            var v = Instantiate(cardViewPrefab, content);
            v.Bind(c, faceDown:false);
        }
    }
}