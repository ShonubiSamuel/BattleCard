using System;
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
using YGO.Duel.UI;
using YGO.Duel.Zones;


[DefaultExecutionOrder(-200)]
public sealed class DuelInstaller : MonoBehaviour
{
    [Header("Core assets")]
    public RuleSet ruleSet;
    public GameConfig gameConfig;

    [Header("Decks (drag CardDefinition assets)")]
    public string player1Name = "Player 1";
    public List<CardDefinition> p1Main = new();
    public List<CardDefinition> p1Extra = new();
    public string player2Name = "Player 2";
    public List<CardDefinition> p2Main = new();
    public List<CardDefinition> p2Extra = new();

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
    private ChainManager _chain;


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

        // Chain/turn/draw systems
        var costs = new CostSystem();
        var conds = new ConditionSystem();
        _turns = new TurnManager(ruleSet, _board, _logger, chainState: null);
        var duelStateProvider = new LocalDuelStateProvider(_turns, _board, ruleSet);
        _chain = new ChainManager(_board, ruleSet, duelStateProvider, _logger, costs, conds);
        _turns = new TurnManager(ruleSet, _board, _logger, chainState: _chain);

        _draws       = new DrawSystem(_board, _logger, ruleSet, _turns, _rng, autoHookTurnStart:false);
        _discards    = new DiscardSystem(_board, _logger, _rng);
        _destruction = new DestructionSystem(_board, _logger, _bus);
        
        // After you construct managers and _queue
        var policyValidator = new ActionPolicyValidator(_board, _turns, ruleSet);
        _queue.SetValidator(policyValidator);

        var p1 = new PlayerActionHandler(_board, _turns, _logger, _queue, BoardManager.Seat.P1);
        var p2 = new PlayerActionHandler(_board, _turns, _logger, _queue, BoardManager.Seat.P2);
        ServiceLocator.Register<IPlayerDirectory>(new SimplePlayerDirectory(p1, p2), overwrite:true);

        
        var attackSvc = new AttackCommandService(_queue, _turns, _logger, ServiceLocator.TryGet<ICardIndex>(out var idx) ? idx : null);
        ServiceLocator.Register<IAttackCommandService>(attackSvc, overwrite:true);


        // --- ServiceLocator wiring (no duplicates) ---
        ServiceLocator.Register(_logger);
        ServiceLocator.Register(_bus);
        ServiceLocator.Register(ruleSet);
        ServiceLocator.Register(gameConfig);
        ServiceLocator.Register(_board);
        ServiceLocator.Register(_turns);
        ServiceLocator.Register(_queue);
        ServiceLocator.Register(_rng);
        ServiceLocator.Register(_pos);
        ServiceLocator.Register(_targeting);
        ServiceLocator.Register(battle, overwrite:true);
        ServiceLocator.Register(_chain);
        ServiceLocator.Register(_draws, overwrite:true);
        ServiceLocator.Register(_discards);
        ServiceLocator.Register(_destruction);
        ServiceLocator.Register<IBattlerResolver>(new DefaultBattlerResolver());
        ServiceLocator.Register<ICardStatProvider>(new SimpleCardStatProvider(), overwrite:true);
        ServiceLocator.Register<IAvatarLocator>(new AvatarLocatorService(), overwrite: true);

        // --- Build & load ---
        _board.BuildEmptyBoard(gameConfig);

        var deckSrc = new InspectorDeckSource(player1Name, p1Main, p1Extra, player2Name, p2Main, p2Extra);
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

        _queue.OnActionDequeued += HandleAction;
    }



    // Very small executor: pull from queue and execute immediately. In a real game you’d
    // drive this from a host loop or a network dispatcher.
    // DuelInstaller.cs — replace Update() with a draining loop
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



    private void HandleAction(GameAction a)
    {
        // Hook for UI or analytics when actions enter the queue
    }

    // --- local provider to satisfy ChainManager timing needs
    private sealed class LocalDuelStateProvider : YGO.Duel.Chain.IDuelStateProvider
    {
        private readonly TurnManager _turns;
        private readonly BoardManager _board;
        private readonly RuleSet _rules;
        public LocalDuelStateProvider(TurnManager t, BoardManager b, RuleSet r) { _turns=t; _board=b; _rules=r; }
        public RuleSet.IRuleDuelState GetDuelState() => _turns.GetDuelStateAdapter();
        public bool IsControllerTurn(BoardManager.Seat seat) => _turns.CurrentPlayer == seat;
    }

    // Deck source that converts CardDefinition → runtime Card at load time
    private sealed class InspectorDeckSource : BoardManager.IDeckSource
    {
        private readonly string n1, n2;
        private readonly List<CardDefinition> m1, e1, m2, e2;

        public InspectorDeckSource(string p1Name, List<CardDefinition> p1Main, List<CardDefinition> p1Extra,
                                   string p2Name, List<CardDefinition> p2Main, List<CardDefinition> p2Extra)
        { n1=p1Name; n2=p2Name; m1=p1Main; e1=p1Extra; m2=p2Main; e2=p2Extra; }

        public string GetPlayerName(BoardManager.Seat seat) => seat==BoardManager.Seat.P1 ? n1 : n2;

        public List<Card> GetMainDeck(BoardManager.Seat seat)
            => Build(seat==BoardManager.Seat.P1 ? m1 : m2, seat);

        public List<Card> GetExtraDeck(BoardManager.Seat seat)
            => Build(seat==BoardManager.Seat.P1 ? e1 : e2, seat);

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
