using System.Collections.Generic;
using Script.Board_zones;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Data;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Runtime;
using YGO.Duel.Runtime.Actions;
using YGO.Duel.Systems;
using YGO.Duel.Targeting;
using YGO.Duel.Battle;
using YGO.Duel.Chain;
using YGO.Duel.Effects;
using YGO.Duel.UI;

[DefaultExecutionOrder(-200)]
public sealed class DuelInstaller : MonoBehaviour
{
    [Header("Core assets")]
    public RuleSet ruleSet;
    public GameConfig gameConfig;

    [Header("Deck Mode")]
    public bool autoBuildDecks = false;    // false = Manual (inspector lists), true = Auto (shared pool)
    [Range(20, 60)] public int autoDeckSize = 40;
    [Range(0f, 1f)] public float autoMonsterRatio = 0.5f;
    [Range(0f, 1f)] public float autoSpellRatio   = 0.3f;
    [Range(0f, 1f)] public float autoTrapRatio    = 0.2f;
    public int autoMaxCopiesPerCard = 3;

    [Header("Manual Deck Enforcement")]
    public bool enforceManualConstraints = true;
    [Range(20, 60)] public int manualDeckSize = 40;       // target size; we’ll warn/adjust if off
    [Range(0f, 1f)] public float manualMonsterRatio = 0.5f;
    [Range(0f, 1f)] public float manualSpellRatio   = 0.3f;
    [Range(0f, 1f)] public float manualTrapRatio    = 0.2f;
    public int manualMaxCopiesPerCard = 3;
    [Tooltip("Allowed ± variance around ratio targets (e.g., 0.1 = ±10%).")]
    [Range(0f, 0.4f)] public float ratioTolerance = 0.1f;

    [Header("Manual Decks (drag CardDefinition assets)")]
    public string player1Name = "Player 1";
    public List<CardDefinition> p1Main = new();
    public List<CardDefinition> p1Extra = new();
    public string player2Name = "Player 2";
    public List<CardDefinition> p2Main = new();
    public List<CardDefinition> p2Extra = new();

    [Header("Auto Deck (shared pool for both players)")]
    public List<CardDefinition> cardPool = new();

    [Header("Startup")]
    public int openingHandSize = 5;
    public bool autoStart = true;

    // Services
    private DuelLogger _logger;
    private EventBus   _bus;
    private BoardManager _board;
    private TurnManager  _turns;
    private DeterministicRng _rng;
    private ActionQueue _queue;
    private PositionManager _pos;
    private TargetingService _targeting;
    private DrawSystem _draws;
    private DiscardSystem _discards;
    private DestructionSystem _destruction;

    private void Awake()
    {
        if (!ruleSet) ruleSet = ScriptableObject.CreateInstance<RuleSet>();
        if (!gameConfig) { Debug.LogError("GameConfig missing"); enabled = false; return; }

        // Core instances
        _logger = new DuelLogger { EchoToUnityConsole = true };
        _bus    = new EventBus(_logger);
        _board  = new BoardManager();
        _rng    = new DeterministicRng(seed:123456);
        _queue  = new ActionQueue(_logger);
        _pos    = new PositionManager(_board, _logger);
        _targeting = new TargetingService(_board, _logger);

        // Battle system
        var calc         = new DamageCalculator();
        var boardQuery   = new BoardQueryAdapter(_board);
        var dirValidator = new DirectAttackValidator(boardQuery);
        var triggers     = new BattleTriggerSystem(_logger, _pos, _bus);
        var battle       = new BattleManager(calc, dirValidator, triggers);

        // Chain / turn systems
        var chainMgr = new ChainManager(ruleSet, _board, _logger, turns: null, rng: _rng, bus: _bus);
        var costs    = new CostSystem();
        var conds    = new ConditionSystem();
        _turns       = new TurnManager(ruleSet, _board, _logger, chainState: chainMgr);

        // Draw/Discard/Destruction
        _draws       = new DrawSystem(_board, _logger, ruleSet, _turns, _rng, autoHookTurnStart:false);
        _discards    = new DiscardSystem(_board, _logger, _rng);
        _destruction = new DestructionSystem(_board, _logger, _bus);

        // Queue policy
        var policyValidator = new ActionPolicyValidator(_board, _turns, ruleSet);
        _queue.SetValidator(policyValidator);

        // Player actions & attack commands
        var p1 = new PlayerActionHandler(_board, _turns, _logger, _queue, BoardManager.Seat.P1);
        var p2 = new PlayerActionHandler(_board, _turns, _logger, _queue, BoardManager.Seat.P2);
        ServiceLocator.Register<IPlayerDirectory>(new SimplePlayerDirectory(p1, p2), overwrite:true);
        var attackSvc = new AttackCommandService(_queue, _turns, _logger, ServiceLocator.TryGet<ICardIndex>(out var idxTmp) ? idxTmp : null);
        ServiceLocator.Register<IAttackCommandService>(attackSvc, overwrite:true);

        // Continuous/equip/field + stat provider
        var baseStats = new SimpleCardStatProvider();
        var contMgr = new ContinuousEffectService(_logger, _bus,baseStats);
        ServiceLocator.Register(contMgr, overwrite:true);
        ServiceLocator.Register<ICardStatProvider>(contMgr, overwrite:true);

        var equipMgr = new EquipManager(_logger, _bus);
        ServiceLocator.Register(equipMgr, overwrite:true);

        var fieldMgr = new FieldZoneManager(_board, _logger, contMgr);
        ServiceLocator.Register(fieldMgr, overwrite:true);


        // --- ServiceLocator wiring (no duplicates) ---
        ServiceLocator.Register(_logger);
        ServiceLocator.Register(_bus);
        ServiceLocator.Register(ruleSet);
        ServiceLocator.Register(gameConfig);
        ServiceLocator.Register(_board);
        ServiceLocator.Register(_turns);
        ServiceLocator.Register<IChainManager>(chainMgr, overwrite:true);
        ServiceLocator.Register(_queue);
        ServiceLocator.Register(_rng);
        ServiceLocator.Register(_pos);
        ServiceLocator.Register(_targeting);
        ServiceLocator.Register(battle, overwrite:true);
        ServiceLocator.Register(_draws, overwrite:true);
        ServiceLocator.Register(_discards);
        ServiceLocator.Register(_destruction);
        ServiceLocator.Register<IBattlerResolver>(new DefaultBattlerResolver());
        ServiceLocator.Register<IAvatarLocator>(new AvatarLocatorService(), overwrite: true);

        // --- Build & load board ---
        _board.BuildEmptyBoard(gameConfig);

        BoardManager.IDeckSource deckSrc;
        if (autoBuildDecks)
        {
            deckSrc = new AutoDeckSource(
                sharedPool: cardPool,
                p1Name: player1Name, p2Name: player2Name,
                deckSize: autoDeckSize,
                mRatio: autoMonsterRatio, sRatio: autoSpellRatio, tRatio: autoTrapRatio,
                maxCopies: autoMaxCopiesPerCard,
                rng: _rng,
                logger: _logger
            );
        }
        else
        {
            deckSrc = new ManualDeckSource(
                player1Name, p1Main, p1Extra,
                player2Name, p2Main, p2Extra,
                enforceManualConstraints,
                manualDeckSize,
                manualMonsterRatio, manualSpellRatio, manualTrapRatio,
                manualMaxCopiesPerCard,
                ratioTolerance,
                _logger
            );
        }

        _board.LoadPlayersAndDecks(gameConfig, deckSrc);
        _board.ShuffleBothDecks(_rng);
        _board.DrawOpeningHands(openingHandSize);

        // Card index AFTER cards exist
        var cardIndex = new RuntimeCardIndex(_board);
        ServiceLocator.Register<ICardIndex>(cardIndex, overwrite:true);

        if (autoStart)
        {
            _turns.BeginFirstTurn(gameConfig);
            _logger.LogText("Bootstrap", "Duel started");
        }
    }

    // Drain queue each frame (same as your original loop)
    private void Update()
    {
        while (_queue.TryDequeue(out var a))
        {
            var ctx = ActionContext.FromServices();
            if (!a.Validate(ctx, out var why))
                _logger.LogText("Action.ValidateFail", $"{a}", data: why);
            else if (!a.Execute(ctx, out why))
                _logger.LogText("Action.ExecuteFail", $"{a}", data: why);
        }
    }

    // -------------------- Deck sources --------------------

    // Manual source with optional enforcement (size/ratios/3-of)
    private sealed class ManualDeckSource : BoardManager.IDeckSource
    {
        private readonly string n1, n2;
        private List<CardDefinition> m1, e1, m2, e2;
        private readonly bool enforce;
        private readonly int deckSize;
        private readonly float rM, rS, rT, tol;
        private readonly int maxCopies;
        private readonly DuelLogger log;

        public ManualDeckSource(
            string p1Name, List<CardDefinition> p1Main, List<CardDefinition> p1Extra,
            string p2Name, List<CardDefinition> p2Main, List<CardDefinition> p2Extra,
            bool enforce, int deckSize,
            float monsterR, float spellR, float trapR, int maxCopies, float tolerance,
            DuelLogger logger)
        {
            n1 = p1Name; n2 = p2Name;
            m1 = p1Main ?? new List<CardDefinition>();
            e1 = p1Extra ?? new List<CardDefinition>();
            m2 = p2Main ?? new List<CardDefinition>();
            e2 = p2Extra ?? new List<CardDefinition>();
            this.enforce = enforce;
            this.deckSize = deckSize;
            rM = monsterR; rS = spellR; rT = trapR; this.maxCopies = Mathf.Max(1, maxCopies);
            tol = Mathf.Clamp01(tolerance);
            log = logger ?? new DuelLogger();
        }

        public string GetPlayerName(BoardManager.Seat seat) => seat==BoardManager.Seat.P1 ? n1 : n2;

        public List<Card> GetMainDeck(BoardManager.Seat seat)
        {
            var src = seat==BoardManager.Seat.P1 ? m1 : m2;
            var fixedList = enforce ? Enforce(src, deckSize, rM, rS, rT, maxCopies, tol) : new List<CardDefinition>(src);
            return Build(fixedList, seat);
        }

        public List<Card> GetExtraDeck(BoardManager.Seat seat)
        {
            var src = seat==BoardManager.Seat.P1 ? e1 : e2;
            // Extra deck left as-is (you can add enforcement if you want)
            return Build(src, seat);
        }

        private List<CardDefinition> Enforce(List<CardDefinition> src, int size, float m, float s, float t, int copies, float tolerance)
        {
            if (src == null) return new List<CardDefinition>();

            // 1) Clamp 3-of
            var perId = new Dictionary<CardDefinition, int>();
            var clamped = new List<CardDefinition>(src.Count);
            foreach (var d in src)
            {
                if (d == null) continue;
                perId.TryGetValue(d, out var have);
                if (have < copies) { clamped.Add(d); perId[d] = have + 1; }
            }

            // 2) Split by kind
            var monsters = new List<CardDefinition>();
            var spells   = new List<CardDefinition>();
            var traps    = new List<CardDefinition>();
            foreach (var d in clamped)
            {
                if (d.IsMonster) monsters.Add(d);
                else if (d.IsSpell) spells.Add(d);
                else if (d.IsTrap) traps.Add(d);
            }

            // 3) Compute targets with tolerance window
            int targetM = Mathf.RoundToInt(size * m);
            int targetS = Mathf.RoundToInt(size * s);
            int targetT = size - targetM - targetS;

            int minM = Mathf.RoundToInt(size * Mathf.Max(0f, m - tolerance));
            int maxM = Mathf.RoundToInt(size * Mathf.Min(1f, m + tolerance));
            int minS = Mathf.RoundToInt(size * Mathf.Max(0f, s - tolerance));
            int maxS = Mathf.RoundToInt(size * Mathf.Min(1f, s + tolerance));
            int minT = Mathf.RoundToInt(size * Mathf.Max(0f, t - tolerance));
            int maxT = Mathf.RoundToInt(size * Mathf.Min(1f, t + tolerance));

            // 4) Build final list trying to hit targets; fallback to whatever exists
            var result = new List<CardDefinition>(size);
            AppendUpTo(result, monsters, targetM);
            AppendUpTo(result, spells,   targetS);
            AppendUpTo(result, traps,    targetT);

            // If we still don’t have enough, fill from whatever remains
            if (result.Count < size)
            {
                var pool = new List<CardDefinition>(clamped);
                while (result.Count < size && pool.Count > 0)
                {
                    result.Add(pool[0]);
                    pool.RemoveAt(0);
                }
            }

            // Trim if oversized
            if (result.Count > size) result.RemoveRange(size, result.Count - size);

            // Log basic summary
            int cm = 0, cs = 0, ct = 0;
            foreach (var d in result) { if (d.IsMonster) cm++; else if (d.IsSpell) cs++; else if (d.IsTrap) ct++; }
            log.LogText("Deck.Manual.Enforce", $"Built {result.Count} (M:{cm} S:{cs} T:{ct})");

            return result;

            static void AppendUpTo(List<CardDefinition> dst, List<CardDefinition> srcList, int count)
            {
                int want = Mathf.Max(0, count);
                for (int i = 0; i < srcList.Count && dst.Count < want + (dst.Count - 0); i++)
                    dst.Add(srcList[i]);
                // If srcList shorter than want, we just add whatever is available.
            }
        }

        private static List<Card> Build(List<CardDefinition> defs, BoardManager.Seat owner)
        {
            var list = new List<Card>(defs?.Count ?? 0);
            if (defs != null)
                foreach (var d in defs)
                    if (d) list.Add(new Card(d, owner));
            return list;
        }
    }

    // Auto source: builds both players’ decks from the same pool with ratios & 3-of
    private sealed class AutoDeckSource : BoardManager.IDeckSource
    {
        private readonly string n1, n2;
        private readonly List<CardDefinition> pool;
        private readonly int size;
        private readonly float mRatio, sRatio, tRatio;
        private readonly int maxCopies;
        private readonly DeterministicRng rng;
        private readonly DuelLogger log;

        public AutoDeckSource(List<CardDefinition> sharedPool, string p1Name, string p2Name,
                              int deckSize, float mRatio, float sRatio, float tRatio,
                              int maxCopies, DeterministicRng rng, DuelLogger logger)
        {
            pool = sharedPool ?? new List<CardDefinition>();
            n1 = p1Name; n2 = p2Name;
            size = Mathf.Clamp(deckSize, 20, 60);
            this.mRatio = mRatio; this.sRatio = sRatio; this.tRatio = tRatio;
            this.maxCopies = Mathf.Max(1, maxCopies);
            this.rng = rng ?? new DeterministicRng(999);
            this.log = logger ?? new DuelLogger();
        }

        public string GetPlayerName(BoardManager.Seat seat) => seat==BoardManager.Seat.P1 ? n1 : n2;

        public List<Card> GetMainDeck(BoardManager.Seat seat)
        {
            var defs = BuildDefs();
            return Build(defs, seat);
        }

        public List<Card> GetExtraDeck(BoardManager.Seat seat)
        {
            return new List<Card>(); // keep simple for now
        }

        private List<CardDefinition> BuildDefs()
        {
            var monsters = new List<CardDefinition>();
            var spells   = new List<CardDefinition>();
            var traps    = new List<CardDefinition>();

            // Clamp 3-of across the source pool (so random picks won’t exceed)
            var perId = new Dictionary<CardDefinition, int>();
            foreach (var d in pool)
            {
                if (d == null) continue;
                perId.TryGetValue(d, out var have);
                if (have < maxCopies)
                {
                    if (d.IsMonster) monsters.Add(d);
                    else if (d.IsSpell) spells.Add(d);
                    else if (d.IsTrap) traps.Add(d);
                    perId[d] = have + 1;
                }
            }

            int mCount = Mathf.RoundToInt(size * mRatio);
            int sCount = Mathf.RoundToInt(size * sRatio);
            int tCount = size - mCount - sCount;

            var outList = new List<CardDefinition>(size);
            outList.AddRange(PickRandom(monsters, mCount));
            outList.AddRange(PickRandom(spells,   sCount));
            outList.AddRange(PickRandom(traps,    tCount));

            // If shortage in any category, fill from any remaining category
            while (outList.Count < size)
            {
                var pick = RandomFromAny(monsters, spells, traps);
                if (pick == null) break;
                outList.Add(pick);
            }

            if (outList.Count > size) outList.RemoveRange(size, outList.Count - size);

            // Log summary
            int cm=0, cs=0, ct=0;
            foreach (var d in outList) { if (d.IsMonster) cm++; else if (d.IsSpell) cs++; else if (d.IsTrap) ct++; }
            log.LogText("Deck.Auto.Build", $"Built {outList.Count} (M:{cm} S:{cs} T:{ct})");

            return outList;
        }

        private CardDefinition RandomFromAny(List<CardDefinition> a, List<CardDefinition> b, List<CardDefinition> c)
        {
            int total = a.Count + b.Count + c.Count;
            if (total == 0) return null;
            int roll = rng.NextInt(0, total);
            if (roll < a.Count) return a[rng.NextInt(0, a.Count)];
            roll -= a.Count;
            if (roll < b.Count) return b[rng.NextInt(0, b.Count)];
            roll -= b.Count;
            return c.Count > 0 ? c[rng.NextInt(0, c.Count)] : null;
        }

        private List<CardDefinition> PickRandom(List<CardDefinition> src, int count)
        {
            var res = new List<CardDefinition>(Mathf.Max(0, count));
            if (src == null || src.Count == 0 || count <= 0) return res;
            for (int i = 0; i < count; i++) res.Add(src[rng.NextInt(0, src.Count)]);
            return res;
        }

        private static List<Card> Build(List<CardDefinition> defs, BoardManager.Seat owner)
        {
            var list = new List<Card>(defs?.Count ?? 0);
            if (defs != null)
                foreach (var d in defs)
                    if (d) list.Add(new Card(d, owner));
            return list;
        }
    }
}