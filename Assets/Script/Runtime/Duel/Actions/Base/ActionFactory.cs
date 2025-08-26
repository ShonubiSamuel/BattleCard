// ActionFactory.cs

using YGO.Duel.Battle;
using YGO.Duel.Board;
using YGO.Duel.Rules;

namespace YGO.Duel.Runtime.Actions
{
    public static class ActionFactory
    {
        public static EndPhaseAction EndPhase(BoardManager.Seat seat, TurnManager turns)
        {
            var a = new EndPhaseAction();
            a.FillSnapshot(seat, turns);
            return a;
        }

        public static PassPriorityAction PassPriority(BoardManager.Seat seat, TurnManager turns)
        {
            var a = new PassPriorityAction();
            a.FillSnapshot(seat, turns);
            return a;
        }

        public static NormalSummonAction NormalSummon(BoardManager.Seat seat, TurnManager turns, string handCardId, int monsterZoneIndex)
        {
            var a = new NormalSummonAction { handCardId = handCardId, monsterZoneIndex = monsterZoneIndex };
            a.FillSnapshot(seat, turns);
            return a;
        }

        public static DeclareAttackAction DeclareAttack(BoardManager.Seat seat, TurnManager turns, string attackerId, string targetId)
        {
            var a = new DeclareAttackAction { attackerId = attackerId, targetId = targetId };
            a.FillSnapshot(seat, turns);
            return a;
        }

        public static ActivateEffectAction ActivateEffect(BoardManager.Seat seat, TurnManager turns, string sourceId, string effectId)
        {
            var a = new ActivateEffectAction { sourceInstanceId = sourceId, effectId = effectId };
            a.FillSnapshot(seat, turns);
            return a;
        }

        public static SetCardAction SetToMonster(BoardManager.Seat seat, TurnManager turns, string handCardId, int mzIndex)
        {
            var a = new SetCardAction { handCardId = handCardId, destination = SetDestination.MonsterZone, zoneIndex = mzIndex };
            a.FillSnapshot(seat, turns);
            return a;
        }

        public static SetCardAction SetToST(BoardManager.Seat seat, TurnManager turns, string handCardId, int stIndex)
        {
            var a = new SetCardAction { handCardId = handCardId, destination = SetDestination.SpellTrapZone, zoneIndex = stIndex };
            a.FillSnapshot(seat, turns);
            return a;
        }
        
        public static ChangePositionAction ChangePosition(BoardManager.Seat seat, TurnManager turns, string cardId, BattlePosition to, bool faceUp)
        {
            var a = new ChangePositionAction { monsterId = cardId, to = to, /* faceUp handled by action */ };
            // we pass faceUp via action’s 'faceUp' when doing flip; for pure pos change we keep current face.
            a.faceUp = faceUp;
            a.FillSnapshot(seat, turns);
            return a;
        }

// Flip Summon = FD → FU Attack
        public static ChangePositionAction FlipSummon(BoardManager.Seat seat, TurnManager turns, string cardId)
        {
            var a = new ChangePositionAction { monsterId = cardId, to = BattlePosition.Attack, faceUp = true };
            a.FillSnapshot(seat, turns);
            return a;
        }
    }
}
