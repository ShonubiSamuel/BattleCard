using System.Collections.Generic;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using Card = YGO.Duel.Cards.Card;

[DefaultExecutionOrder(-105)]
public sealed class SpawnManager3D : MonoBehaviour
{
    public Board3DLayout layout;
    public Card3DView card3DPrefab;
    public bool despawnOffField = true;

    private EventBus _bus;
    private BoardManager _board;
    private DuelLogger _logger;

    private readonly Dictionary<Card, Card3DView> _live = new(256);

    private void Start()
    {
        if (!layout) layout = FindObjectOfType<Board3DLayout>();
        ServiceLocator.TryGet(out _bus);
        ServiceLocator.TryGet(out _board);
        ServiceLocator.TryGet(out _logger);

        if (!card3DPrefab)
            Debug.LogWarning("[SpawnManager3D] Global card3DPrefab not set.");

        // ✅ Subscribe here, after _bus is fetched.
        if (_bus != null)
        {
            _bus.OnSummoned  += HandleSummoned;
            _bus.OnCardMoved += HandleCardMoved;
            _bus.OnDestroyed += HandleDestroyed;
        }
        else
        {
            Debug.LogWarning("[SpawnManager3D] EventBus not found in Start().");
        }
    }

    private void OnDisable()
    {
        if (_bus != null)
        {
            _bus.OnSummoned  -= HandleSummoned;
            _bus.OnCardMoved -= HandleCardMoved;
            _bus.OnDestroyed -= HandleDestroyed;
        }
    }

    // --------------- Event handlers ----------------

    private void HandleSummoned(object sender, SummonEvent e)
    {
        var card = e.Card;
        if (card == null || layout == null) return;

        var anchor = layout.GetSlot(e.Controller, BoardManager.CardZone.Monster, e.ZoneIndex);
        if (!anchor)
        {
            _logger?.LogText("3D.Spawn.Warn",
                $"No Monster anchor for {SafeName(card)} @ P{SeatN(e.Controller)}[{e.ZoneIndex}]",
                source: nameof(SpawnManager3D));
            return;
        }

        var view = GetOrCreateView(card, anchor);
        SafeBindFaceAndOffsets(card, view);
        // SpawnManager3D.cs — in HandleSummoned(...) after EnsureMonsterSpawned()
        if (card.Def?.IsMonster == true)
        {
            view.EnsureMonsterSpawned();
            view.EnsureWorldUIAttached();      // ✅ NEW
        }
    }

    private void HandleCardMoved(object sender, CardMovedEvent e)
    {
        var card = e.Card;
        if (card == null || layout == null) return;

        var to = e.Move.To;

        // Off-field destinations
        if (to.Kind == BoardManager.CardZone.Hand ||
            to.Kind == BoardManager.CardZone.Deck ||
            to.Kind == BoardManager.CardZone.ExtraDeck)
        {
            if (despawnOffField) Despawn(card);
            return;
        }

        // GY / Banished (off-field “piles”)
        if (to.Kind == BoardManager.CardZone.Graveyard ||
            to.Kind == BoardManager.CardZone.Banished)
        {
            var anchor = layout.GetSlot(to.Seat, to.Kind, to.Index);
            if (_live.TryGetValue(card, out var view) && view)
            {
                if (anchor)
                {
                    Reparent(view, anchor);
                    view.ApplyFace(false); // generally face-down in GY piles visuals, tweak to taste
                }
                else
                {
                    Despawn(card);
                }
            }
            return;
        }

        // Field zones (MZ / ST / Field)
        var targetAnchor = layout.GetSlot(to.Seat, to.Kind, to.Index);
        if (!targetAnchor)
        {
            _logger?.LogText("3D.Spawn.Warn",
                $"No anchor for {SafeName(card)} @ {to}",
                source: nameof(SpawnManager3D));
            return;
        }

        var v = GetOrCreateView(card, targetAnchor);
        SafeBindFaceAndOffsets(card, v);

        // Spawn monster actor if entering Monster Zone
        if (to.Kind == BoardManager.CardZone.Monster && card.Def?.IsMonster == true)
            v.EnsureMonsterSpawned();
        else
            v.DespawnMonsterIfAny(); // keep card-only for ST/Field zones
    }

    private void HandleDestroyed(object sender, DestroyEvent e)
    {
        // You can add a small destruction FX here, then despawn/move to GY.
        if (e?.Card == null) return;
        // Optional: keep visuals until the subsequent CardMoved to GY arrives.
        // Here we do nothing; CardMoved handler will handle the actual move.
        
        // If your GY move isn’t raised by the adapter yet, this is a safe fallback.
        //Despawn(e.Card);
    
    }

    // --------------- Helpers ----------------

    private Card3DView GetOrCreateView(Card c, Transform parentAnchor)
    {
        // Reuse if alive
        if (_live.TryGetValue(c, out var existing) && existing)
        {
            Reparent(existing, parentAnchor);
            return existing;
        }

        // Choose prefab: per-card override > global fallback
        GameObject prefab = c?.Def?.card3DPrefab
            ? c.Def.card3DPrefab
            : card3DPrefab ? card3DPrefab.gameObject : null;

        if (!prefab)
        {
            _logger?.LogText("3D.Spawn.Err",
                $"No prefab for {SafeName(c)} and no global fallback. Skipping spawn.",
                source: nameof(SpawnManager3D));
            return null;
        }

        var go = Instantiate(prefab, parentAnchor);
        var view = go.GetComponent<Card3DView>();
        if (!view)
        {
            _logger?.LogText("3D.Spawn.Err",
                $"Prefab {prefab.name} missing Card3DView component.",
                source: nameof(SpawnManager3D));
            Destroy(go);
            return null;
        }

        ResetLocalTRS(view.transform);
        _live[c] = view;
        return view;
    }

    private void Reparent(Card3DView view, Transform parent)
    {
        if (!view || !parent) return;
        view.transform.SetParent(parent, worldPositionStays: false);
        ResetLocalTRS(view.transform);
    }

    private void ResetLocalTRS(Transform t)
    {
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale    = Vector3.one;
    }

    private void Despawn(Card c)
    {
        if (_live.TryGetValue(c, out var view))
        {
            if (view) Destroy(view.gameObject);
            _live.Remove(c);
        }
    }

    private void SafeBindFaceAndOffsets(Card card, Card3DView view)
    {
        if (!view) return;
        view.Bind(card);
        // Apply the per-card offset if present
        var offset = card?.Def ? card.Def.card3DOffset : Vector3.zero;
        view.transform.localPosition = offset;
        // Face state derived from runtime card
        view.ApplyFace(card?.IsFaceUp == true);
    }
    
    // Keep track of spawned views (you already have _live< Card, Card3DView >)
    public bool TryGetView(Card card, out Card3DView view)
    {
        if (card != null && _live.TryGetValue(card, out var v) && v)
        {
            view = v;
            return true;
        }
        view = null;
        return false;
    }

    private static int SeatN(BoardManager.Seat s) => s == BoardManager.Seat.P1 ? 1 : 2;

    private static string SafeName(Card c) => c?.Name ?? "(null)";
}