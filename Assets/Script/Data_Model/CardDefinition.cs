// CardDefinition.cs
// Static (author-time) card data. One asset per card.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization; // <-- add this at the top for FormerlySerializedAs

namespace YGO.Duel.Data
{
    public enum CardKind { Monster, Spell, Trap }

    [Flags]
    public enum MonsterSubtypes
    {
        None = 0,
        Normal = 1 << 0,
        Effect = 1 << 1,
        Fusion = 1 << 2,
        Ritual = 1 << 3,
        Synchro = 1 << 4,
        Xyz = 1 << 5,
        Link = 1 << 6,
        Pendulum = 1 << 7,
        Tuner = 1 << 8,
        Gemini = 1 << 9,
        Spirit = 1 << 10,
        Toon = 1 << 11,
        Union = 1 << 12
    }

    public enum SpellSubtype { Normal, QuickPlay, Continuous, Equip, Field, Ritual }
    public enum TrapSubtype  { Normal, Continuous, Counter }

    public enum LimitStatus { Unlimited, SemiLimited, Limited, Forbidden }

    [CreateAssetMenu(fileName = "CardDefinition", menuName = "YGO/Data/Card Definition", order = 0)]
    public sealed class CardDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique string id across your database (e.g., 'LOB-001', 'BEWD').")]
        public string cardId;

        [SerializeField, HideInInspector] private string definitionGuid;
        
        public string DefinitionId => string.IsNullOrEmpty(definitionGuid)
            ? (definitionGuid = System.Guid.NewGuid().ToString("N"))
            : definitionGuid;
        
        [Tooltip("Konami passcode / numeric id (optional). Use 0 if unknown.")]
        public int passcode;

        [Tooltip("Card name as shown in-game.")]
        public string cardName = "New Card";

        [Header("Classification")]
        public CardKind kind = CardKind.Monster;

        [Tooltip("For monsters: attribute tag (LIGHT, DARK, etc.). For spells/traps leave null.")]
        public AttributeTag attribute;

        [Tooltip("Monster race/type (Dragon, Warrior, ...). For spells/traps leave null.")]
        public TypeTag monsterRace;

        [Tooltip("Archetype tags this card belongs to (e.g., 'Blue-Eyes').")]
        public List<ArchetypeTag> archetypes = new List<ArchetypeTag>();

        [Tooltip("Forbidden/Limited status in the selected format.")]
        public LimitStatus limitStatus = LimitStatus.Unlimited;

        [Header("Monster Stats")]
        [Tooltip("Monster subtypes & mechanics (Fusion, Ritual, Synchro, Xyz, Link, Pendulum, etc.).")]
        public MonsterSubtypes monsterSubtypes = MonsterSubtypes.Normal;

        [Tooltip("Level (1-12). Use 0 for non-level monsters (Xyz/Link).")]
        [Range(0, 12)] public int level = 4;

        [Tooltip("Rank (1-13) for Xyz. Otherwise 0.")]
        [Range(0, 13)] public int rank = 0;

        [Tooltip("Link rating (1-6) for Link monsters. Otherwise 0.")]
        [Range(0, 6)] public int linkRating = 0;

        [Tooltip("Pendulum scale (0-13). Only used if Pendulum subtype is present.")]
        [Range(0, 13)] public int pendulumScale = 0;

        [Tooltip("Base ATK; use -1 for N/A (e.g., some traps/spells, or '?' monsters).")]
        public int baseATK = 0;

        [Tooltip("Base DEF; use -1 for N/A (Link monsters generally have no DEF).")]
        public int baseDEF = 0;

        [Header("Spell/Trap")]
        public SpellSubtype spellSubtype = SpellSubtype.Normal;
        public TrapSubtype  trapSubtype  = TrapSubtype.Normal;

        [Header("Text")]
        [TextArea(4, 12)] public string effectText;
        [TextArea(2, 6)]  public string flavorText;

        [Header("Effect Graph (optional)")]
        [Tooltip("Optional effect graph asset describing targeting/costs/operations.")]
        public EffectGraph effectGraph;
        
        [Tooltip("Optional: override front artwork for the 3D card material (e.g., assigned to _BaseMap).")]
        public Texture2D cardFrontTexture;

        // Existing fields you already had (kept as-is):
        [Tooltip("Monster actor prefab to spawn above the card when face-up in MZ (animated rig/VFX root).")]
        public GameObject monsterPrefab;

        [Tooltip("Local offset for the card mesh on its anchor, if you need to nudge it.")]
        public Vector3 card3DOffset = Vector3.zero;

        [Tooltip("Local offset for the monster actor above the card.")]
        public Vector3 monsterOffset = new Vector3(0f, 0.2f, 0f);
        
        // CardDefinition.cs — add near your other visual fields

        [Header("3D Card (front)")]
        [Tooltip("Optional 3D card prefab. If set, Card3DView will instantiate this for the physical card mesh.")]
        public GameObject card3DPrefab;

        [Tooltip("Optional front art texture for this card. If your mesh shader uses _MainTex, Card3DView can assign it.")]
        public Texture2D frontArtTexture;

        [Tooltip("Local offset for the card mesh (applied to the instantiated card3DPrefab under cardMeshRoot).")]
        public Vector3 cardMeshOffset = Vector3.zero;

        [Tooltip("Local rotation (Euler) for the card mesh.")]
        public Vector3 cardMeshEuler = Vector3.zero;

        [Tooltip("Local scale for the card mesh. Use (1,1,1) for default.")]
        public Vector3 cardMeshScale = Vector3.one;

// (you can keep your VFX/SFX fields; Card3DView will ignore them for now)
        

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Basic hygiene & constraints
            if (string.IsNullOrWhiteSpace(cardName)) cardName = name;

            if (kind == CardKind.Monster)
            {
                // Enforce stat semantics
                if ((monsterSubtypes & MonsterSubtypes.Link) != 0)
                    baseDEF = -1; // Links have no DEF

                if ((monsterSubtypes & MonsterSubtypes.Xyz) != 0)
                {
                    level = 0; // Xyz use Ranks
                    if (rank < 1) rank = 1;
                }
                else if ((monsterSubtypes & MonsterSubtypes.Link) == 0)
                {
                    // Level-based monsters
                    if (level < 1) level = 1;
                    rank = 0;
                }
            }
            else // Spell/Trap
            {
                attribute = null;
                monsterRace = null;
                level = 0; rank = 0; linkRating = 0; pendulumScale = 0;
                // ATK/DEF usually N/A for non-monsters
                if (baseATK != -1) baseATK = -1;
                if (baseDEF != -1) baseDEF = -1;
            }

            // Default ids
            if (string.IsNullOrWhiteSpace(cardId))
                cardId = name.Replace("CardDefinition", "").Trim();

            if (passcode < 0) passcode = 0;
            
            if (string.IsNullOrEmpty(definitionGuid))
            {
                definitionGuid = System.Guid.NewGuid().ToString("N");
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif

        // Convenience checks
        public bool IsMonster => kind == CardKind.Monster;
        public bool IsSpell   => kind == CardKind.Spell;
        public bool IsTrap    => kind == CardKind.Trap;

        public bool IsPendulum => (monsterSubtypes & MonsterSubtypes.Pendulum) != 0;
        public bool IsLink     => (monsterSubtypes & MonsterSubtypes.Link) != 0;
        public bool IsXyz      => (monsterSubtypes & MonsterSubtypes.Xyz) != 0;
        
        public bool HasPerCard3D => card3DPrefab != null;
        public bool HasFrontArt3D => cardFrontTexture != null;
    }
}
