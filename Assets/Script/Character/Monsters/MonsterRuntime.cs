// MonsterRuntime.cs
// Lightweight runtime entity for a face-up monster on the field.
// Implements IBattler so BattleManager can use it directly.
// Internally delegates side-effects (destroy, damage, etc.) to CardBattlerAdapter.

using System;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Battle;
using YGO.Duel.Cards;
using YGO.Duel.Foundation;

namespace YGO.Duel.Battle
{
    public sealed class MonsterRuntime : IBattler
    {
        public readonly Card CardRef;
        public BoardManager.Seat Controller => CardRef.Controller;
        public int ZoneIndex { get; internal set; } = -1;

        // Combat flags (you can push temporary buffs here later)
        public bool CanAttackThisTurn { get; set; } = true;
        public bool HasAttackedThisTurn { get; set; } = false;
        public bool IsAttackTargetable { get; set; } = true;
        public bool IsDirectAttackAllowed { get; set; } = false;
        public bool HasPiercing { get; set; } = false;

        // Cached presentation handle (optional; used by selection/raycast)
        public Transform ActorRoot { get; internal set; } // set by registry when spawned

        // IBattler stats proxy
        public string Name => CardRef?.Name ?? "(Monster)";
        public int ATK => CardRef?.CurrentATK ?? 0;
        public int DEF => CardRef?.CurrentDEF ?? 0;

        // Position proxy
        public BattlePosition Position
        {
            get => CardRef != null && CardRef.Position == CardBattlePosition.Defense
                ? BattlePosition.Defense : BattlePosition.Attack;
            set
            {
                if (CardRef == null) return;
                // Prefer PositionManager if available (keeps OPT/flip rules intact)
                if (ServiceLocator.TryGet<PositionManager>(out var pos) && pos != null)
                {
                    pos.RequestPositionChange(
                        CardRef,
                        value == BattlePosition.Attack ? BattlePosition.Attack : BattlePosition.Defense,
                        faceUp: true,
                        out _);
                }
                else
                {
                    CardRef.SetPosition(
                        value == BattlePosition.Attack ? CardBattlePosition.Attack : CardBattlePosition.Defense,
                        faceUp: true);
                }
            }
        }

        public bool IsOnField => CardRef?.IsOnField == true;
        public bool IsFaceUp  => CardRef?.IsFaceUp  == true;

        // Delegate side-effects to the existing CardBattlerAdapter to avoid duplicate logic
        private readonly CardBattlerAdapter _adapter;

        public MonsterRuntime(Card card, int mzIndex)
        {
            CardRef = card ?? throw new ArgumentNullException(nameof(card));
            ZoneIndex = mzIndex;
            _adapter = new CardBattlerAdapter(CardRef);
        }

        // ---- IBattler actions ----
        public void DestroyByBattle()           => _adapter.DestroyByBattle();
        public void SendToGraveyard()           => _adapter.SendToGraveyard();
        public void InflictBattleDamage(int a, BoardManager.Seat p) => _adapter.InflictBattleDamage(a, p);
        public void AfterDamageStep()           => _adapter.AfterDamageStep();

        // ---- Helpers ----
        public override string ToString() => $"MonsterRuntime({Name}, P{(Controller==BoardManager.Seat.P1?1:2)}, MZ[{ZoneIndex}])";
    }
}
