using System.Collections;
using System.Linq;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Foundation;
using YGO.Duel.UI;

public sealed class PlayerHandView : MonoBehaviour
{
    [Header("Config")]
    public BoardManager.Seat seat = BoardManager.Seat.P1;
    public Transform content;                 // where card views go; default = this.transform
    public CardView cardViewPrefab;           // drag your CardView prefab here
    public bool clearOnRefresh = true;
    public float cardScale = 1f;

    private BoardManager _board;
    private EventBus _bus;
    private bool _subscribed;

    private void Start()
    {
        if (!content) content = transform;
        ServiceLocator.TryGet(out _board);
        ServiceLocator.TryGet(out _bus);

        Subscribe();
        Refresh();
    }
    

    private void OnDisable()
    {
        Unsubscribe();
    }
    
    private void Subscribe()
    {
        if (_subscribed || _bus == null) return;
        _bus.OnCardsDrawn      += HandleCardsDrawn;
        _bus.OnCardsDiscarded  += HandleCardsDiscarded;
        _bus.OnCardMoved       += HandleCardMoved;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || _bus == null) return;
        _bus.OnCardsDrawn      -= HandleCardsDrawn;
        _bus.OnCardsDiscarded  -= HandleCardsDiscarded;
        _bus.OnCardMoved       -= HandleCardMoved;
        _subscribed = false;
    }

    private void HandleCardsDrawn(object _, CardsDrawnEvent e)       { if (e.Seat == seat) Refresh(); }
    private void HandleCardsDiscarded(object _, CardsDiscardedEvent e){ if (e.Seat == seat) Refresh(); }
    private void HandleCardMoved(object _, CardMovedEvent e)
    {
        // Refresh if the move involves this player's Hand
        if ((e.Move.From.Seat == seat && e.Move.From.Kind == BoardManager.CardZone.Hand) ||
            (e.Move.To.Seat   == seat && e.Move.To.Kind   == BoardManager.CardZone.Hand))
        {
            Refresh();
        }
    }

    [ContextMenu("Refresh")]
    public void Refresh()
    {
        if (_board == null || cardViewPrefab == null || content == null) return;

        if (clearOnRefresh)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);
        }

        var hand = _board.Zones[(int)seat].Hand;
        foreach (var card in hand.RawList.ToList()) // copy for safety
        {
            var view = Instantiate(cardViewPrefab, content);
            view.transform.localScale = Vector3.one * cardScale;
            view.Bind(card, faceDown:false);
        }
    }
}
