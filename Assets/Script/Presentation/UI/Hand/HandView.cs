// HandView.cs
// Renders a player's hand as buttons (Button + Text). Raises OnCardClicked event.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using Card = YGO.Duel.Cards.Card;

public sealed class HandView : MonoBehaviour
{
    [Header("Config")]
    public BoardManager.Seat seat = BoardManager.Seat.P1;

    [Header("UI")]
    public Transform content;           // parent for card buttons
    public GameObject cardButtonPrefab; // must have a Text child

    public event Action<Card> OnCardClicked;

    private BoardManager _board;
    private DuelLogger   _logger;

    private readonly List<GameObject> _spawned = new List<GameObject>(20);
    private readonly List<Card> _cards = new List<Card>(20);

    private void Awake()
    {
        ServiceLocator.TryGet(out _board);
        ServiceLocator.TryGet(out _logger);

        if (_logger != null)
            _logger.OnLogged += HandleLog;
    }

    private void OnDestroy()
    {
        if (_logger != null)
            _logger.OnLogged -= HandleLog;
    }

    private void Start() => Refresh();

    public void Refresh()
    {
        if (_board == null || content == null || cardButtonPrefab == null) return;

        // Clear
        for (int i = 0; i < _spawned.Count; i++) Destroy(_spawned[i]);
        _spawned.Clear();
        _cards.Clear();

        var hand = _board.Zones[(int)seat].Hand;
        for (int i = 0; i < hand.Count; i++)
        {
            var card = ((YGO.Duel.Zones.ListZoneBase)hand).RawList[i];
            var go = Instantiate(cardButtonPrefab, content);
            var txt = go.GetComponentInChildren<Text>();
            if (txt) txt.text = card.Name;
            var btn = go.GetComponent<Button>();
            if (btn)
            {
                var capture = card;
                btn.onClick.AddListener(() => OnCardClicked?.Invoke(capture));
            }
            _spawned.Add(go);
            _cards.Add(card);
        }
    }

    private void HandleLog(DuelLogger.LogEntry e)
    {
        // naive heuristic: redraw on common hand mutations
        if (e.Type.StartsWith("Draw.") || e.Type.Contains("Set") || e.Type.Contains("Normal Summon") || e.Type.Contains("Discard"))
            Refresh();
    }
}
