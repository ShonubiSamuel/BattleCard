using UnityEngine;
using YGO.Duel.Cards;
using YGO.Duel.Data;
using YGO.Duel.Board;
using YGO.Duel.UI;

public class CardViewDemoBinder : MonoBehaviour
{
    public CardView cardViewPrefab;      // assign the prefab you made
    public CardDefinition definition;    // assign any CardDefinition asset
    public RectTransform parent;         // any UI container under Canvas

    void Start()
    {
        if (!cardViewPrefab || !definition || !parent) { Debug.LogError("DemoBinder: missing refs"); return; }

        // build a runtime Card and bind
        var runtimeCard = new Card(definition, BoardManager.Seat.P1);
        var view = Instantiate(cardViewPrefab, parent);
        view.Bind(runtimeCard, faceDown: false); // set true to test face-down/back
    }
}