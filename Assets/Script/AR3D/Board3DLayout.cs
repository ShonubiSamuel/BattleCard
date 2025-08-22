using System;
using System.Linq;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Foundation; // for DuelLogger (optional)

[DefaultExecutionOrder(-120)]
public sealed class Board3DLayout : MonoBehaviour
{
    [Serializable]
    public class SeatParents
    {
        [Header("Parents (assign ONLY the container transforms)")]
        public Transform monsterZonesParent;     // children = slots 0..N
        public Transform spellTrapZonesParent;   // children = slots 0..N
        public Transform pendulumZonesParent;    // optional, children = 0..1
        public Transform fieldZoneParent;        // optional, 1 child (or use the parent itself)
        public Transform graveyardParent;        // optional, 1 child (or use the parent itself)
        public Transform banishedParent;         // optional, 1 child (or use the parent itself)
        public Transform extraDeckParent;        // optional, 1 child (or use the parent itself)

        [Header("Interpretation")]
        [Tooltip("When true, the parent’s immediate children are used as slots, otherwise the parent itself is treated as the slot.")]
        public bool useChildrenForSingleSlotZones = true;

        // Cached slots (populated by RefreshCache)
        [NonSerialized] public Transform[] monsterSlots;
        [NonSerialized] public Transform[] spellTrapSlots;
        [NonSerialized] public Transform[] pendulumSlots;
        [NonSerialized] public Transform   fieldSlot;
        [NonSerialized] public Transform   graveyardSlot;
        [NonSerialized] public Transform   banishedSlot;
        [NonSerialized] public Transform   extraDeckSlot;

        public void RefreshCache(DuelLogger logger = null)
        {
            monsterSlots   = CollectChildren(monsterZonesParent);
            spellTrapSlots = CollectChildren(spellTrapZonesParent);
            pendulumSlots  = CollectChildren(pendulumZonesParent);

            fieldSlot      = ResolveSingleSlot(fieldZoneParent, useChildrenForSingleSlotZones);
            graveyardSlot  = ResolveSingleSlot(graveyardParent, useChildrenForSingleSlotZones);
            banishedSlot   = ResolveSingleSlot(banishedParent, useChildrenForSingleSlotZones);
            extraDeckSlot  = ResolveSingleSlot(extraDeckParent, useChildrenForSingleSlotZones);

#if UNITY_EDITOR
            // Soft diagnostics in editor, not at runtime.
            if (logger != null)
            {
                if (monsterZonesParent && monsterSlots.Length == 0)
                    logger.LogText("3D.Layout.Warn", $"'{monsterZonesParent.name}' has 0 MZ children.", source: nameof(Board3DLayout));
                if (spellTrapZonesParent && spellTrapSlots.Length == 0)
                    logger.LogText("3D.Layout.Warn", $"'{spellTrapZonesParent.name}' has 0 ST children.", source: nameof(Board3DLayout));
                if (pendulumZonesParent && pendulumSlots.Length > 0 && pendulumSlots.Length != 2)
                    logger.LogText("3D.Layout.Info", $"'{pendulumZonesParent.name}' has {pendulumSlots.Length} PZ children (expected 2 if using Pendulum).", source: nameof(Board3DLayout));
            }
#endif
        }

        private static Transform[] CollectChildren(Transform parent)
        {
            if (!parent) return Array.Empty<Transform>();
            // Use the hierarchy order (explicitly controllable in the editor).
            var count = parent.childCount;
            var arr = new Transform[count];
            for (int i = 0; i < count; i++) arr[i] = parent.GetChild(i);
            return arr;
        }

        private static Transform ResolveSingleSlot(Transform parent, bool useChildrenIfPresent)
        {
            if (!parent) return null;
            if (!useChildrenIfPresent) return parent;
            if (parent.childCount > 0) return parent.GetChild(0);
            return parent;
        }
    }

    [Header("Assign only parent containers per seat")]
    public SeatParents p1;
    public SeatParents p2;

    [Header("Validation")]
    public bool validateAgainstBoardLayout = true;
    public bool logValidationWarnings = true;

    private DuelLogger _logger;
    private BoardManager _board;

    private void Start()
    {
        ServiceLocator.TryGet(out _logger);
        ServiceLocator.TryGet(out _board);

        RefreshCache();
        if (validateAgainstBoardLayout) ValidateCounts();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Keep cache fresh when values change in inspector
        if (!Application.isPlaying)
        {
            RefreshCache();
        }
    }
#endif

    public void RefreshCache()
    {
        p1?.RefreshCache(_logger);
        p2?.RefreshCache(_logger);
    }

    public Transform GetSlot(BoardManager.Seat seat, BoardManager.CardZone kind, int index = 0)
    {
        var s = (seat == BoardManager.Seat.P1) ? p1 : p2;
        if (s == null) return null;

        switch (kind)
        {
            case BoardManager.CardZone.Monster:
                return GetIndexed(s.monsterSlots, index, "MZ");
            case BoardManager.CardZone.SpellTrap:
                return GetIndexed(s.spellTrapSlots, index, "ST");
            case BoardManager.CardZone.Pendulum:
                return GetIndexed(s.pendulumSlots, index, "PZ");
            case BoardManager.CardZone.Field:
                return s.fieldSlot;
            case BoardManager.CardZone.Graveyard:
                return s.graveyardSlot;
            case BoardManager.CardZone.Banished:
                return s.banishedSlot;
            case BoardManager.CardZone.ExtraDeck:
                return s.extraDeckSlot;
            default:
                return null;
        }
    }

    private Transform GetIndexed(Transform[] arr, int idx, string labelForLog)
    {
        if (arr == null || arr.Length == 0) return null;
        if (idx < 0 || idx >= arr.Length)
        {
            if (logValidationWarnings)
                _logger?.LogText("3D.Layout.OutOfRange", $"{labelForLog}[{idx}] out of range (size={arr.Length})", source: nameof(Board3DLayout));
            return null;
        }
        return arr[idx];
    }

    private void ValidateCounts()
    {
        if (_board == null) return;

        // Compare against BoardManager layout to help you spot mismatches early.
        var layout = _board.BoardLayout;

        if (p1.monsterSlots != null && p1.monsterSlots.Length > 0 && p1.monsterSlots.Length != layout.MaxMonsterZones && logValidationWarnings)
            _logger?.LogText("3D.Layout.Warn", $"P1 MZ slots={p1.monsterSlots.Length} but rules expect {layout.MaxMonsterZones}.", source: nameof(Board3DLayout));
        if (p2.monsterSlots != null && p2.monsterSlots.Length > 0 && p2.monsterSlots.Length != layout.MaxMonsterZones && logValidationWarnings)
            _logger?.LogText("3D.Layout.Warn", $"P2 MZ slots={p2.monsterSlots.Length} but rules expect {layout.MaxMonsterZones}.", source: nameof(Board3DLayout));

        if (p1.spellTrapSlots != null && p1.spellTrapSlots.Length > 0 && p1.spellTrapSlots.Length != layout.MaxSpellTrapZones && logValidationWarnings)
            _logger?.LogText("3D.Layout.Warn", $"P1 ST slots={p1.spellTrapSlots.Length} but rules expect {layout.MaxSpellTrapZones}.", source: nameof(Board3DLayout));
        if (p2.spellTrapSlots != null && p2.spellTrapSlots.Length > 0 && p2.spellTrapSlots.Length != layout.MaxSpellTrapZones && logValidationWarnings)
            _logger?.LogText("3D.Layout.Warn", $"P2 ST slots={p2.spellTrapSlots.Length} but rules expect {layout.MaxSpellTrapZones}.", source: nameof(Board3DLayout));
    }

    // -------- Gizmos --------
#if UNITY_EDITOR
    [Header("Gizmos")]
    public Color mzGizmo = new Color(0.2f, 0.9f, 0.9f, 0.35f);
    public Color stGizmo = new Color(0.9f, 0.9f, 0.2f, 0.35f);
    public Color fxGizmo = new Color(0.9f, 0.2f, 0.9f, 0.35f);
    public Vector3 slotSize = new Vector3(0.06f, 0.002f, 0.09f);
    public bool drawLabels = true;

    private void OnDrawGizmos()
    {
        DrawSeatGizmos(p1, "P1");
        DrawSeatGizmos(p2, "P2");
    }

    private void DrawSeatGizmos(SeatParents s, string seatName)
    {
        if (s == null) return;

        DrawSlots(s.monsterSlots, mzGizmo, seatName, "MZ");
        DrawSlots(s.spellTrapSlots, stGizmo, seatName, "ST");
        DrawSlots(s.pendulumSlots, fxGizmo, seatName, "PZ");

        DrawOne(s.fieldSlot, fxGizmo, $"{seatName}.FZ");
        DrawOne(s.graveyardSlot, fxGizmo, $"{seatName}.GY");
        DrawOne(s.banishedSlot, fxGizmo, $"{seatName}.BAN");
        DrawOne(s.extraDeckSlot, fxGizmo, $"{seatName}.EX");
    }

    private void DrawSlots(Transform[] arr, Color c, string seat, string tag)
    {
        if (arr == null) return;
        Gizmos.color = c;
        for (int i = 0; i < arr.Length; i++)
        {
            var t = arr[i];
            if (!t) continue;
            Gizmos.matrix = t.localToWorldMatrix;
            Gizmos.DrawCube(Vector3.zero, slotSize);
            if (drawLabels)
                UnityEditor.Handles.Label(t.position + Vector3.up * 0.02f, $"{seat}.{tag}[{i}]");
        }
    }

    private void DrawOne(Transform t, Color c, string label)
    {
        if (!t) return;
        Gizmos.color = c;
        Gizmos.matrix = t.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.zero, slotSize);
        if (drawLabels)
            UnityEditor.Handles.Label(t.position + Vector3.up * 0.02f, label);
    }
#endif
}