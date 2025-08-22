// Token.cs
// Runtime token monster: created by effects, not stored in decks. Disappears when it leaves the field.

using System;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Battle;
using YGO.Duel.Model.Contracts;

namespace YGO.Duel.Model
{
    /// <summary>
    /// Lightweight runtime token that participates in battle and respects token rules (no GY/banished/hand).
    /// </summary>
    [Serializable]
    public sealed class Token :
        IBattler,             // battle adapter (you already defined)
        IDestructible,        // can be destroyed by battle/effects
        IBanishable,          // banish attempts (tokens just vanish)
        IGraveMovable         // "send to grave" attempts (tokens just vanish)
    {
        // Identity / description (for logs and UI only)
        public string Name { get; private set; } = "Token";
        public string ShortDescription { get; private set; } = "Generated Token";

        // Control
        public BoardManager.Seat Controller { get; private set; }
        public BoardManager.Seat Owner      { get; private set; }

        // Visibility / field presence
        public bool IsOnField  { get; private set; } = true;
        public bool IsFaceUp   { get; private set; } = true;

        // Targetability flags (tweak via effects)
        public bool IsAttackTargetable { get; set; } = true;

        // Battle flags
        public bool CanAttackThisTurn  { get; set; } = true;
        public bool HasAttackedThisTurn { get; set; } = false;
        public bool IsDirectAttackAllowed { get; set; } = false;
        public bool HasPiercing { get; set; } = false;

        // Stats
        public int ATK { get; private set; }
        public int DEF { get; private set; }
        public BattlePosition Position { get; set; } = BattlePosition.Attack;

        // Lifecycle events (board/FX/UI can subscribe)
        public event Action<Token> OnRemovedFromField;        // fired when token vanishes/removed
        public event Action<Token, RemoveReason> OnVanish;    // reasoned removal (battle, effect, rule)

        // Construction is internal; use factory to keep creation consistent
        private Token() { }

        public static Token Create(
            string name,
            int atk,
            int def,
            BoardManager.Seat owner,
            BoardManager.Seat? controller = null,
            bool faceUp = true,
            string shortDesc = "Generated Token")
        {
            var t = new Token
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Token" : name,
                ATK = Mathf.Max(0, atk),
                DEF = Mathf.Max(0, def),
                Owner = owner,
                Controller = controller ?? owner,
                IsFaceUp = faceUp,
                ShortDescription = shortDesc
            };
            return t;
        }

        // ---------------- IBattler implementation ----------------

        public void DestroyByBattle() => Vanish(RemoveReason.Battle);

        public void SendToGraveyard()
        {
            // Tokens never reach GY; they vanish immediately by rule.
            Vanish(RemoveReason.GraveyardRedirect);
        }

        public void InflictBattleDamage(int amount, BoardManager.Seat playerDamaged)
        {
            // Hook your LP system here. We keep it decoupled: raise an event or call a service.
            // For now, just Debug.Log for visibility (replace with your LP system):
            Debug.Log($"[TOKEN] {Name} inflicts {amount} battle damage to {playerDamaged}");
        }

        public void AfterDamageStep()
        {
            // Optional: post-damage step hooks (tokens rarely need custom logic).
        }

        // ---------------- Entity contracts (destruction/move) ----------------

        public void DestroyByEffect(string source = null)
        {
            Vanish(RemoveReason.Effect, source);
        }

        public void Banish(bool faceDown = false, string source = null)
        {
            // Tokens cannot be banished to a zone; they simply disappear.
            Vanish(RemoveReason.BanishRedirect, source);
        }

        public void SendToGrave(string source = null)
        {
            // Tokens cannot exist in GY; they simply disappear.
            Vanish(RemoveReason.GraveyardRedirect, source);
        }

        // ---------------- Internals ----------------

        private void Vanish(RemoveReason reason, string source = null)
        {
            if (!IsOnField) return;

            IsOnField = false;
            IsAttackTargetable = false;
            CanAttackThisTurn = false;

            OnRemovedFromField?.Invoke(this);
            OnVanish?.Invoke(this, reason);

            Debug.Log($"[TOKEN] Vanish: {Name} ({reason}){(string.IsNullOrEmpty(source) ? "" : $" by {source}")}");
        }
    }
}
