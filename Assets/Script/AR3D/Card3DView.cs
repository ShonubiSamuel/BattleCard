using UnityEngine;
using UnityEngine.Serialization;
using YGO.Duel.Cards;
using YGO.Duel.Data;

public sealed class Card3DView : MonoBehaviour
{
    [FormerlySerializedAs("cardMeshRoot")]
    [Header("Anchors")]
    [Tooltip("Parent for the 3D card mesh prefab (from CardDefinition.card3DPrefab).")]
    public Transform CardRoot;

    [FormerlySerializedAs("monsterRoot")] [Tooltip("Parent for the monster actor prefab (from CardDefinition.monsterPrefab).")]
    public Transform MonsterRoot;
    
// Card3DView.cs — add fields at top with other anchors
    [Header("World UI")]
    [Tooltip("Where to attach the world-space UI (ATK/DEF, pips). Defaults to MonsterRoot; will auto-create.")]
    public Transform WorldUIAnchor;
    
    [Header("Optional Art Hook")]
    [Tooltip("If your card mesh has a front Renderer, assign it to allow art texture swapping.")]
    public Renderer cardFrontRenderer;

    [Tooltip("Material slot index for the front art texture (_MainTex).")]
    public int frontMainTexSlot = 0;
    
    public GameObject spawnedCardMesh;   // instance of card3DPrefab
    public GameObject spawnedMonster;    // instance of monsterPrefab

    private CardDefinition _def;
    
    public Card BoundCard { get; private set; }

// Optional: if present we move/aim to this point when attacking.
    public Transform AttackOrigin;

// e.g., outline or emissive toggle reference (optional)
    [SerializeField] private Renderer highlightRenderer;
    private MaterialPropertyBlock _pb;
    
    

    // Card3DView.cs — in Reset(), ensure anchors are not null
    private void Reset()
    {
        if (!CardRoot)    CardRoot    = transform;
        if (!MonsterRoot) MonsterRoot = transform;
        if (!WorldUIAnchor)
        {
            var go = new GameObject("WorldUIAnchor");
            var t  = go.transform;
            t.SetParent(MonsterRoot ? MonsterRoot : transform, false);
            t.localPosition = Vector3.up * 0.1f; // small default offset above the monster
            WorldUIAnchor = t;
        }
    }

    private void OnDestroy()
    {
        DespawnMonsterIfAny();
        DespawnCardMeshIfAny();
    }

    // --------------------------------------------------------------------
    // Public API
    // --------------------------------------------------------------------

    /// <summary>Bind this view to a runtime Card. Idempotent.</summary>
    // Card3DView.cs  — inside Bind(Card card)
    public void Bind(Card card)
    {
        BoundCard = card;       // <-- THIS WAS MISSING
        _def      = card?.Def;

        EnsureCardMeshSpawned();
        EnsureMonsterConsistency();
        RefreshAll();
    }

    /// <summary>Flip visible face. Hides monster when face-down.</summary>
    public void ApplyFace(bool faceUp)
    {
        if (CardRoot)
        {
            var e = CardRoot.localEulerAngles;
            e.x = faceUp ? 0f : 180f;
            CardRoot.localEulerAngles = e;
        }

        if (spawnedMonster) spawnedMonster.SetActive(faceUp);
    }

    /// <summary>Spawn the monster actor if this is a Monster card and not already spawned.</summary>
    public void EnsureMonsterSpawned()
    {
        if (spawnedMonster || _def == null || !_def.IsMonster || !_def.monsterPrefab) return;
        if (!MonsterRoot) MonsterRoot = transform;

        if (MonsterRoot) MonsterRoot.gameObject.SetActive(true);
        if (CardRoot)    CardRoot.gameObject.SetActive(true); // keep or disable depending on your style
        
        spawnedMonster = Instantiate(_def.monsterPrefab, MonsterRoot);
        spawnedMonster.transform.localPosition = _def.monsterOffset;
        spawnedMonster.transform.localRotation = Quaternion.identity;
        spawnedMonster.transform.localScale    = Vector3.one;

        TryTrigger(spawnedMonster, "Idle");
    }
    

    /// <summary>Destroy the monster actor if present.</summary>
    public void DespawnMonsterIfAny()
    {
        if (MonsterRoot) MonsterRoot.gameObject.SetActive(false);
        if (!spawnedMonster) return;
        Destroy(spawnedMonster);
        spawnedMonster = null;
    }

    /// <summary>Refresh visuals based on bound card/definition.</summary>
    public void RefreshAll()
    {
        // Face
        ApplyFace(BoundCard?.IsFaceUp == true);

        // Art (optional)
        SetCardArtTexture(_def?.frontArtTexture);
    }
    
    // --------------------------------------------------------------------
    // Card mesh handling
    // --------------------------------------------------------------------

    /// <summary>Ensure the physical 3D card is visible. If the view prefab already has a child under cardMeshRoot, it will adopt it.</summary>
    public void EnsureCardMeshSpawned()
    {
        // Already spawned and correctly parented
        if (spawnedCardMesh && spawnedCardMesh.transform.parent == CardRoot)
            return;

        // Author placed a mesh under the root? Adopt first child.
        if (!spawnedCardMesh && CardRoot && CardRoot.childCount > 0)
        {
            spawnedCardMesh = CardRoot.GetChild(0).gameObject;
            // Try to find a front renderer automatically if none provided
            if (!cardFrontRenderer)
                cardFrontRenderer = spawnedCardMesh.GetComponentInChildren<Renderer>();
            return;
        }

        // Instantiate from definition if provided
        if (_def != null && _def.card3DPrefab != null && CardRoot != null)
        {
            spawnedCardMesh = Instantiate(_def.card3DPrefab, CardRoot);
            spawnedCardMesh.transform.localPosition = _def.cardMeshOffset;
            spawnedCardMesh.transform.localRotation = Quaternion.Euler(_def.cardMeshEuler);
            spawnedCardMesh.transform.localScale    = _def.cardMeshScale;

            if (!cardFrontRenderer)
                cardFrontRenderer = spawnedCardMesh.GetComponentInChildren<Renderer>();
        }
        // Else: nothing to do; you can still have a static mesh authored on this prefab.
    }

    /// <summary>Destroy the spawned card mesh (only if it’s the instance we created/adopted).</summary>
    public void DespawnCardMeshIfAny()
    {
        if (!spawnedCardMesh) return;

        // Only destroy if we own it (parented to our anchor)
        if (spawnedCardMesh.transform.parent == CardRoot)
            Destroy(spawnedCardMesh);

        spawnedCardMesh = null;
    }

    /// <summary>Optional: swap the card art texture on the front material.</summary>
    public void SetCardArtTexture(Texture tex)
    {
        if (!cardFrontRenderer || tex == null) return;

        // Use instantiated materials (avoid editing shared material)
        var mats = cardFrontRenderer.materials;
        if (frontMainTexSlot < 0 || frontMainTexSlot >= mats.Length) return;

        mats[frontMainTexSlot].SetTexture("_MainTex", tex);
        cardFrontRenderer.materials = mats;
    }

    // --------------------------------------------------------------------
    // Internals
    // --------------------------------------------------------------------

    private void EnsureMonsterConsistency()
    {
        // If not a monster, make sure no monster actor remains spawned
        if (_def != null && !_def.IsMonster)
        {
            DespawnMonsterIfAny();
            return;
        }

        // If it is a monster, spawn only when needed
        if (_def != null && _def.IsMonster)
        {
            EnsureMonsterSpawned();
        }
    }
    
    /// <summary>Transform to move when attacking.</summary>
    public Transform GetAttackTransform()
    {
        if (MonsterRoot != null) return MonsterRoot;
        return CardRoot != null ? CardRoot : this.transform; // fallback
    }

    private static void TryTrigger(GameObject go, string trigger)
    {
        if (!go || string.IsNullOrEmpty(trigger)) return;
        var anim = go.GetComponentInChildren<Animator>();
        if (anim) anim.SetTrigger(trigger);
    }
    
    // Card3DView.cs — ensure the UI exists and bound to this view
    public void EnsureWorldUIAttached()
    {
        if (!WorldUIAnchor)
        {
            var go = new GameObject("WorldUIAnchor");
            var t  = go.transform;
            t.SetParent(MonsterRoot ? MonsterRoot : transform, false);
            t.localPosition = Vector3.up * 0.1f;
            WorldUIAnchor = t;
        }

        var ui = WorldUIAnchor.GetComponent<MonsterWorldUI>();
        if (!ui)Debug.LogError("No UI attached to MonsterWorldUI");
        ui.Bind(BoundCard); // ✅ let UI know which card/view it mirrors
    }
    
    // Card3DView.cs — optional, if you want highlight on the monster
    public void SetHighlighted(bool on)
    {
        if (spawnedMonster)
        {
            var r = spawnedMonster.GetComponentInChildren<Renderer>();
            if (r)
            {
                var pb = _pb ??= new MaterialPropertyBlock();
                r.GetPropertyBlock(pb);
                pb.SetFloat("_Highlighted", on ? 1f : 0f);
                r.SetPropertyBlock(pb);
            }
            return;
        }

        // fallback: old card-plane highlight
        if (!highlightRenderer) return;
        var pb2 = _pb ??= new MaterialPropertyBlock();
        highlightRenderer.GetPropertyBlock(pb2);
        pb2.SetFloat("_Highlighted", on ? 1f : 0f);
        highlightRenderer.SetPropertyBlock(pb2);
    }
    
}