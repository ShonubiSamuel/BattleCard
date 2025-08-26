// GameActionBase.cs
// Polymorphic actions base + envelope/codec + compatibility factories.

using System;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Runtime.Actions;  // for ActionFactory / GameActionCodec (if you use them)
using Card = YGO.Duel.Cards.Card; // alias the canonical runtime card


namespace YGO.Duel.Runtime.Actions
{
    [Serializable]
    public enum ActionType
    {
        ActivateEffect = 1,
        DeclareAttack  = 2,
        ChangePosition = 3,
        NormalSummon   = 4,
        SetCard        = 5,
        EndPhase       = 6,
        Concede        = 7,
        PassPriority   = 8,
        EndTurn        = 9,   // <-- added
        FlipSummon     = 10,
        Custom
    }

    public sealed class ActionContext
    {
        public BoardManager Board;
        public TurnManager  Turns;
        public RuleSet      Rules;
        public DuelLogger   Logger;
        public DeterministicRng Rng;

        public static ActionContext FromServices()
        {
            ServiceLocator.TryGet(out BoardManager board);
            ServiceLocator.TryGet(out TurnManager turns);
            ServiceLocator.TryGet(out RuleSet rules);
            ServiceLocator.TryGet(out DuelLogger logger);
            ServiceLocator.TryGet(out DeterministicRng rng);
            return new ActionContext { Board = board, Turns = turns, Rules = rules, Logger = logger ?? new DuelLogger(), Rng = rng };
        }
    }


    [Serializable]
    public abstract class GameAction
    {
        public long   seq;
        public string sessionId;
        public BoardManager.Seat seat;
        public int    turnNumber;
        public RuleSet.Phase phase;
        public string atUtcIso;

        public abstract ActionType Type { get; }

        public virtual bool Validate(ActionContext ctx, out string reason)
        { reason = ""; return true; }

        public abstract bool Execute(ActionContext ctx, out string error);

        public void FillSnapshot(BoardManager.Seat actor, TurnManager turns)
        {
            seat       = actor;
            turnNumber = turns != null ? turns.TurnNumber   : turnNumber;
            phase      = turns != null ? turns.CurrentPhase : phase;
            atUtcIso   = DateTime.UtcNow.ToString("o");
        }

        public override string ToString()
            => $"#{seq} {Type} P{(seat==BoardManager.Seat.P1?"1":"2")} T{turnNumber}:{phase}";

        // ---------------- Compatibility factories (so old callers still compile) ----------------
        // inside YGO.Duel.Runtime.Actions.GameAction (polymorphic base)
        public static GameAction NormalSummon(BoardManager.Seat seat, int turn, RuleSet.Phase phase, string cardId, int mzIndex)
        {
            var a = new NormalSummonAction { seat = seat, turnNumber = turn, phase = phase, atUtcIso = DateTime.UtcNow.ToString("o") };
            a.handCardId = cardId;
            a.monsterZoneIndex = mzIndex;
            return a;
        }

        public static GameAction EndPhase(BoardManager.Seat seat, int turn, RuleSet.Phase phase)
        {
            var a = new EndPhaseAction { seat = seat, turnNumber = turn, phase = phase, atUtcIso = DateTime.UtcNow.ToString("o") };
            return a;
        }

        public static GameAction PassPriority(BoardManager.Seat seat, int turn, RuleSet.Phase phase)
        {
            var a = new PassPriorityAction { seat = seat, turnNumber = turn, phase = phase, atUtcIso = DateTime.UtcNow.ToString("o") };
            return a;
        }

        public static GameAction EndTurn(BoardManager.Seat seat, int turn, RuleSet.Phase phase)
        {
            var a = new EndTurnAction { seat = seat, turnNumber = turn, phase = phase, atUtcIso = DateTime.UtcNow.ToString("o") };
            return a;
        }
    }

    [Serializable]
    public sealed class ActionEnvelope
    {
        public ActionType type;
        public long seq;
        public string sessionId;
        public string payloadJson;
    }

    public static class GameActionCodec
    {
        public static ActionEnvelope Serialize(GameAction action)
        {
            return new ActionEnvelope
            {
                type        = action.Type,
                seq         = action.seq,
                sessionId   = action.sessionId,
                payloadJson = JsonUtility.ToJson(action)
            };
        }

        public static GameAction Deserialize(ActionEnvelope env)
        {
            if (env == null) return null;

            GameAction a = null;
            switch (env.type)
            {
                case ActionType.ActivateEffect: a = JsonUtility.FromJson<ActivateEffectAction>(env.payloadJson); break;
                case ActionType.DeclareAttack:  a = JsonUtility.FromJson<DeclareAttackAction>(env.payloadJson);  break;
                case ActionType.ChangePosition: a = JsonUtility.FromJson<ChangePositionAction>(env.payloadJson); break;
                case ActionType.NormalSummon:   a = JsonUtility.FromJson<NormalSummonAction>(env.payloadJson);   break;
                case ActionType.SetCard:        a = JsonUtility.FromJson<SetCardAction>(env.payloadJson);        break;
                case ActionType.EndPhase:       a = JsonUtility.FromJson<EndPhaseAction>(env.payloadJson);       break;
                case ActionType.PassPriority:   a = JsonUtility.FromJson<PassPriorityAction>(env.payloadJson);   break; // <-- added
                case ActionType.EndTurn:        a = JsonUtility.FromJson<EndTurnAction>(env.payloadJson);        break; // <-- added
                case ActionType.Concede:        a = JsonUtility.FromJson<ConcedeAction>(env.payloadJson);        break;
                default: return null;
            }

            if (a != null) { a.seq = env.seq; a.sessionId = env.sessionId; }
            return a;
        }
    }
    
}
