// DirectAttackValidator.cs  (rename the class)
using System;
using UnityEngine;
using YGO.Duel.Board;

namespace YGO.Duel.Battle
{
    public interface IBoardQuery
    {
        bool OpponentControlsAnyMonsters(BoardManager.Seat seat);
    }

    public sealed class DirectAttackValidator
    {
        private readonly IBoardQuery _board;
        public DirectAttackValidator(IBoardQuery boardQuery)
        {
            _board = boardQuery ?? throw new ArgumentNullException(nameof(boardQuery));
        }
        
        public bool CanDirectAttack(IBattler attacker)
        {
            if (attacker == null || !attacker.IsOnField || !attacker.IsFaceUp) return false;
            if (attacker.IsDirectAttackAllowed) return true;

            bool oppHasMons = _board.OpponentControlsAnyMonsters(attacker.Controller);
             Debug.Log($"[DirectAttackValidator] oppHasMons={oppHasMons} for {attacker.Name}");
            return !oppHasMons;
        }

    }
}