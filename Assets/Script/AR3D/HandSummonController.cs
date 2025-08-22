using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Runtime;
using YGO.Duel.Runtime.Actions;
using YGO.Duel.UI;
using Card = YGO.Duel.Cards.Card;

[DefaultExecutionOrder(-110)]
public sealed class HandSummonController : MonoBehaviour
{
    [Header("Behavior")]
    [Tooltip("If true, only the current player's hand can be used.")]
    public bool onlyCurrentPlayerHand = true;

    [Tooltip("If true, do quick preflight checks (phase, free slots, basic rule gates) before enqueuing actions.")]
    public bool doPreflightValidation = true;

    [Tooltip("Mobile helper: if true, the next monster click from hand will Set instead of Normal Summon.")]
    public bool setNextMonster = false;

    [Tooltip("Prefer this slot search order for Monsters (0..N).")]
    public bool preferLeftToRight = true;

    // Services
    private ActionQueue _queue;
    private TurnManager _turns;
    private BoardManager _board;
    private DuelLogger _logger;
    private ICardIndex _index;
    private RuleSet _rules;

    private void Start()
    {
        ServiceLocator.TryGet(out _queue);
        ServiceLocator.TryGet(out _turns);
        ServiceLocator.TryGet(out _board);
        ServiceLocator.TryGet(out _logger);
        ServiceLocator.TryGet(out _index);
        ServiceLocator.TryGet(out _rules);
    }

    private void OnEnable()  => CardView.OnAnyClicked += OnCardClicked;
    private void OnDisable() => CardView.OnAnyClicked -= OnCardClicked;

    private void OnCardClicked(CardView v)
    {
        if (v == null || v.Card == null) return;
        if (_queue == null || _turns == null || _board == null) return;

        var c   = v.Card;
        var me  = _turns.CurrentPlayer;

        // Must be my hand (unless you allow peeking/playing from other zones)
        if (onlyCurrentPlayerHand && c.Controller != me) return;
        if (c.CurrentZone != BoardManager.CardZone.Hand) return;

        // Phase gate
        var phase = _turns.CurrentPhase;
        if (phase != RuleSet.Phase.Main1 && phase != RuleSet.Phase.Main2)
        {
            _logger?.LogText("HandSummon.Block", $"Not in Main Phase ({phase})", source: nameof(HandSummonController));
            return;
        }

        // MONSTER → Normal Summon or Set
        if (c.Def?.IsMonster == true)
        {
            HandleMonsterFromHand(c, me);
            return;
        }

        // SPELL/TRAP → Set to S/T
        if (c.Def?.IsSpell == true || c.Def?.IsTrap == true)
        {
            HandleSTFromHand(c, me);
            return;
        }

        _logger?.LogText("HandSummon.Block", $"Unsupported card kind for {c.Name}", source: nameof(HandSummonController));
    }

    // -------------------- Monster flow --------------------

    private void HandleMonsterFromHand(Card c, BoardManager.Seat me)
    {
        var doSet =
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            // Desktop: Shift overrides to Set; otherwise respect mobile toggle.
            (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) || setNextMonster;
#else
            // Mobile: rely on the toggle only.
            setNextMonster;
#endif

        // find zone
        int slot = FindFirstFreeSlot(me, isMonster:true);
        if (slot < 0)
        {
            _logger?.LogText("HandSummon.Block", "No free Monster Zone", source: nameof(HandSummonController));
            return;
        }

        if (doPreflightValidation && !PreflightMonsterPlacement(c, me, slot, doSet, out var why))
        {
            _logger?.LogText("HandSummon.Block", $"Monster preflight failed: {why}", source: nameof(HandSummonController));
            return;
        }

        var id = ResolveId(c);
        GameAction a = doSet
            ? ActionFactory.SetToMonster(me, _turns, id, slot)
            : ActionFactory.NormalSummon(me, _turns, id, slot);

        if (_queue.Enqueue(a, out var err))
        {
            _logger?.LogText(
                doSet ? "HandSummon.Enqueue.SetM" : "HandSummon.Enqueue.NS",
                $"{(doSet ? "Set" : "NS")} {c.Name} → MZ[{slot}] (P{(me==BoardManager.Seat.P1?1:2)})",
                source: nameof(HandSummonController));
        }
        else
        {
            _logger?.LogText("HandSummon.Fail", $"{(doSet ? "Set" : "NS")} rejected: {err}", source: nameof(HandSummonController));
        }

        // consume mobile toggle
        setNextMonster = false;
    }

    private bool PreflightMonsterPlacement(Card c, BoardManager.Seat seat, int mzIndex, bool isSet, out string why)
    {
        why = "";
        if (_turns == null || _board == null) { why = "Turns or Board missing"; return false; }

        // Check zone index is in range & empty
        var mz = _board.Zones[(int)seat].Monsters;
        if (mzIndex < 0 || mzIndex >= mz.Length) { why = "Invalid MZ index"; return false; }
        if (!IsSlotEmpty(mz[mzIndex])) { why = "Target MZ is occupied"; return false; }

        // Simple rule timing check (let action validate deeper)
        if (_rules != null)
        {
            // For Normal Summon, respect OPT rules/timing; for Set we can use same timing gate in this simple model
            var adapters = new ActionPolicyValidator.PlayerRuleAdapters(_board, _turns, seat);
            if (!isSet && !(_rules.CanNormalSummon(adapters.Player, adapters.State, adapters.Board, c.Level)))
            {
                why = "Ruleset rejected Normal Summon at this timing";
                return false;
            }
            // You can add a CanSetMonster(...) in RuleSet later; for now we reuse the same timing window.
        }
        return true;
    }

    // -------------------- Spell/Trap flow --------------------

    private void HandleSTFromHand(Card c, BoardManager.Seat me)
    {
        int slot = FindFirstFreeSlot(me, isMonster:false);
        if (slot < 0)
        {
            _logger?.LogText("HandSummon.Block", "No free S/T Zone", source: nameof(HandSummonController));
            return;
        }

        if (doPreflightValidation && !PreflightSTPlacement(me, slot, out var why))
        {
            _logger?.LogText("HandSummon.Block", $"S/T preflight failed: {why}", source: nameof(HandSummonController));
            return;
        }

        var id = ResolveId(c);
        var a  = ActionFactory.SetToST(me, _turns, id, slot);

        if (_queue.Enqueue(a, out var err))
        {
            _logger?.LogText("HandSummon.Enqueue.SetST",
                $"Set {c.Name} → ST[{slot}] (P{(me==BoardManager.Seat.P1?1:2)})",
                source: nameof(HandSummonController));
        }
        else
        {
            _logger?.LogText("HandSummon.Fail", $"Set ST rejected: {err}", source: nameof(HandSummonController));
        }
    }

    private bool PreflightSTPlacement(BoardManager.Seat seat, int stIndex, out string why)
    {
        why = "";
        var st = _board.Zones[(int)seat].SpellsTraps;
        if (stIndex < 0 || stIndex >= st.Length) { why = "Invalid ST index"; return false; }
        if (!IsSlotEmpty(st[stIndex])) { why = "Target ST is occupied"; return false; }
        // You can add Rules.CanSetST(...) later; timing is already gated by phase above.
        return true;
    }

    // -------------------- Helpers --------------------

    private int FindFirstFreeSlot(BoardManager.Seat seat, bool isMonster)
    {
        if (isMonster)
        {
            var arr = _board.Zones[(int)seat].Monsters;
            if (arr == null || arr.Length == 0) return -1;

            if (preferLeftToRight)
            {
                for (int i = 0; i < arr.Length; i++)
                    if (IsSlotEmpty(arr[i])) return i;
            }
            else
            {
                for (int i = arr.Length - 1; i >= 0; --i)
                    if (IsSlotEmpty(arr[i])) return i;
            }
            return -1;
        }
        else
        {
            var arr = _board.Zones[(int)seat].SpellsTraps;
            if (arr == null || arr.Length == 0) return -1;

            if (preferLeftToRight)
            {
                for (int i = 0; i < arr.Length; i++)
                    if (IsSlotEmpty(arr[i])) return i;
            }
            else
            {
                for (int i = arr.Length - 1; i >= 0; --i)
                    if (IsSlotEmpty(arr[i])) return i;
            }
            return -1;
        }
    }
    private static bool IsSlotEmpty(object zoneSlot)
    {
        if (zoneSlot == null) return false;

        var f   = zoneSlot.GetType().GetField("Card"); // legacy one-card slot
        if (f != null) return f.GetValue(zoneSlot) == null;

        var top = zoneSlot.GetType().GetMethod("Top"); // modern stack API
        if (top != null) return top.Invoke(zoneSlot, null) == null;

        var p = zoneSlot.GetType().GetProperty("IsEmpty");
        if (p != null) return (bool)p.GetValue(zoneSlot);

        return false; // unknown shape → conservative
    }

    private string ResolveId(Card c)
    {
        if (c == null) return "";
        if (_index != null)
        {
            var id = _index.GetId(c);
            if (!string.IsNullOrEmpty(id)) return id;
        }
        return c.InstanceId; // safe fallback
    }

    // -------------------- Future extension points --------------------
    // If you add a special-summon UI (e.g., choose zone, pay costs), you can route here:
    // private bool TrySpecialSummon(Card c) { ... return handled; }
}