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
    
    [Header("UI")]
    public SummonChoicePopup summonPopup;  // assign in inspector

    private ISummonCommandService _summon; // resolved in Start()
    
    private bool _summonOrSetPending;
    

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
        // Resolve or create the summon command service
        if (!ServiceLocator.TryGet(out _summon) || _summon == null)
        {
            _summon = new SummonCommandService();
            ServiceLocator.Register<ISummonCommandService>(_summon, overwrite: true);
        }
    }

// Optional public entry point if your hand exists as 3D cards in world space.
// You can call this from SelectionController3D when it raycasts a hand card.
    public void RequestSummonChoiceForHandCard(Card card)
    {
        if (card == null || _turns == null) return;
        if (InputLockService.IsLocked) return; // UI modal open

        var me = _turns.CurrentPlayer;

        if (onlyCurrentPlayerHand && card.Controller != me) return;
        if (card.CurrentZone != BoardManager.CardZone.Hand) return;

        var phase = _turns.CurrentPhase;
        if (phase != RuleSet.Phase.Main1 && phase != RuleSet.Phase.Main2) return;

        if (card.Def?.IsMonster == true) { HandleMonsterFromHand(card, me); return; }
        if (card.Def?.IsSpell == true || card.Def?.IsTrap == true) { HandleSTFromHand(card, me); return; }
    }

    private void OnEnable()
    {
        CardView.OnAnyClicked += OnCardClicked;
        if (ServiceLocator.TryGet(out EventBus bus) && bus != null)
        {
            bus.OnSummoned += HandleSummoned;
            bus.OnPhaseChanged += HandlePhaseChanged;
            bus.OnTurnStarted += HandleTurnStarted;
        }
    }
    private void OnDisable()
    {
        CardView.OnAnyClicked -= OnCardClicked;
        if (ServiceLocator.TryGet(out EventBus bus) && bus != null)
        {
            bus.OnSummoned -= HandleSummoned;
            bus.OnPhaseChanged -= HandlePhaseChanged;
            bus.OnTurnStarted -= HandleTurnStarted;
        }
    }


    private void OnCardClicked(CardView v)
    {
        // At the very top of OnCardClicked / RequestSummonChoiceForHandCard:
        if (_summonOrSetPending || InputLockService.IsLocked) return;


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
        // 1) Find target slot now (we’ll pass it to the service)
        int slot = FindFirstFreeSlot(me, isMonster: true);
        if (slot < 0)
        {
            _logger?.LogText("HandSummon.Block", "No free Monster Zone", source: nameof(HandSummonController));
            return;
        }

        // 2) Preflight both options to inform the popup (and gray out if blocked)
        bool canNS   = true; string whyNS = "";
        bool canSet  = true; string whySet = "";

        if (doPreflightValidation)
        {
            canNS  = PreflightMonsterPlacement(c, me, slot, isSet:false, out whyNS);
            canSet = PreflightMonsterPlacement(c, me, slot, isSet:true,  out whySet);
        }

        // 3) Show popup; on choice, call service
        if (!summonPopup)
        {
            _logger?.LogText("HandSummon.Warn", "SummonChoicePopup missing; defaulting to Normal Summon.", source:nameof(HandSummonController));
            if (_summon != null && canNS)
            {
                if (!_summon.TryNormalSummon(c, me, slot, out var err) && !string.IsNullOrEmpty(err))
                    _logger?.LogText("HandSummon.Fail", $"NS rejected: {err}", source: nameof(HandSummonController));
            }
            return;
        }

        // Optional: position near the clicked hand card if your CardView reports screen pos.
        Vector2? at = null;
        // if you have a CardView ref v: at = v.LastClickScreenPosition;

        summonPopup.Show(
            card: c,
            canNormal: canNS, normalWhy: whyNS,
            canSet: canSet,   setWhy:   whySet,
            onNormal: () =>
            {
                if (_summon == null) return;
                if (!_summon.TryNormalSummon(c, me, slot, out var err) && !string.IsNullOrEmpty(err))
                    _logger?.LogText("HandSummon.Fail", $"NS rejected: {err}", source: nameof(HandSummonController));
            },
            onSet: () =>
            {
                if (_summon == null) return;
                if (!_summon.TrySetMonster(c, me, slot, out var err) && !string.IsNullOrEmpty(err))
                    _logger?.LogText("HandSummon.Fail", $"Set rejected: {err}", source: nameof(HandSummonController));
            },
            screenPos: at
        );
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
        // HandSummonController.cs — inside PreflightMonsterPlacement(...)
        if (_rules != null)
        {
            var adapters = new ActionPolicyValidator.PlayerRuleAdapters(_board, _turns, seat);

            if (!isSet)
            {
                if (!_rules.CanNormalSummon(adapters.Player, adapters.State, adapters.Board, c.Level))
                { why = "Ruleset: Normal Summon not allowed now"; return false; }
            }
            else
            {
                if (!_rules.CanSetMonster(adapters.Player, adapters.State, adapters.Board, c.Level))
                { why = "Ruleset: Set not allowed now"; return false; }
            }
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
    
    private void HandleSummoned(object _, SummonEvent e)
    {
        // Only clear for my own seat; prevents the other player from clearing my guard.
        if (_turns != null && e.Controller == _turns.CurrentPlayer)
            _summonOrSetPending = false;
    }
    private void HandlePhaseChanged(object _, PhaseChangedEvent e)
    {
        if (_turns != null && e.TurnPlayer == _turns.CurrentPlayer)
            _summonOrSetPending = false;
    }
    private void HandleTurnStarted(object _, TurnEvent e)
    {
        // New turn → certainly clear
        _summonOrSetPending = false;
    }

    // -------------------- Future extension points --------------------
    // If you add a special-summon UI (e.g., choose zone, pay costs), you can route here:
    // private bool TrySpecialSummon(Card c) { ... return handled; }
}