using System;
using System.Collections.Generic;
using UnityEngine;
using YGO.Duel.Foundation;
using Card = YGO.Duel.Cards.Card;
using Def  = YGO.Duel.Data.CardDefinition;

public sealed class SimpleCardArtProvider : MonoBehaviour, YGO.Duel.UI.ICardArtProvider
{
    [Serializable]
    public struct Entry
    {
        public Def def;           // the ScriptableObject
        public Sprite art;        // full art
        public Sprite frame;      // optional frame sprite (e.g., monster/blue, spell/green)
    }

    [Header("Art Map (Definition -> Sprites)")]
    public List<Entry> entries = new();

    private readonly Dictionary<Def, Entry> _map = new();

    void Awake()
    {
        _map.Clear();
        foreach (var e in entries)
            if (e.def) _map[e.def] = e;

        // Make discoverable for CardView
        ServiceLocator.Register<YGO.Duel.UI.ICardArtProvider>(this, overwrite: true);
    }

    public Sprite GetArt(Card card)
    {
        if (card?.Def && _map.TryGetValue(card.Def, out var e))
            return e.art;
        return null;
    }

    public Sprite GetFrame(Card card)
    {
        if (card?.Def && _map.TryGetValue(card.Def, out var e))
            return e.frame;
        return null; // CardView will hide frameImage if null
    }
}