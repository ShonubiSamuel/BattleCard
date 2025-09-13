using System;
using System.Collections.Generic;
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Chain;
using YGO.Duel.Chain.YGO.Duel.Chain;
using YGO.Duel.Effects;

namespace YGO.Duel.Runtime.Actions
{
    /// <summary>
    /// Announce/activate a Spell/Trap (places a link on the chain).
    /// - Normal Spell (SS1) from hand during your Main Phase while chain empty.
    /// - Quick-Play Spell (SS2) from hand only on your turn; from STZ on either turn.
    /// - Traps (SS2/3) only from STZ, must NOT have been set this turn.
    /// </summary>
    [Serializable]
    public sealed class ActivateSpellTrapAction : GameAction
    {
        public override ActionType Type => ActionType.Custom; // or add ActionType.Activate
        public string sourceInstanceId;
        public string effectId = ""; // optional selector if a card has multiple effects
        public RuleSet.Timing timing = RuleSet.Timing.OpenGameState;

        public override bool Validate(ActionContext ctx, out string reason)
        {
            reason = "";
            if (ctx?.Board == null || ctx?.Rules == null || ctx?.Turns == null) { reason = "Context missing"; return false; }

            var card = ActionUtil.ResolveCard(ctx, sourceInstanceId, seat, out reason);
            if (card == null) return false;
            if (card.Controller != seat) { reason = "Not your card"; return false; }

            var def = card.Def;
            if (def == null || !(def.IsSpell || def.IsTrap)) { reason = "Not a Spell/Trap"; return false; }

            bool inHand = card.CurrentZone == BoardManager.CardZone.Hand;
            bool inSTZ  = card.CurrentZone == BoardManager.CardZone.SpellTrap;
            if (!inHand && !inSTZ) { reason = "Card must be in Hand or S/T zone"; return false; }

            // NEW: use declared speed (no handle build here)
            var speed = def.GetDeclaredSpeed(effectId);

            var state  = new RuleAdapters.DuelStateAdapter(ctx.Turns);
            var player = new RuleAdapters.RulePlayerAdapter(seat, ctx.Turns, ctx.Board);
            bool isControllerTurn = player.IsTurnPlayer;

            if (def.IsTrap && card.WasSetThisTurn) { reason = "Trap was set this turn"; return false; }

            if (def.IsSpell && speed == RuleSet.SpellSpeed.Two && inHand && !isControllerTurn)
            { reason = "Quick-Play from hand only on your turn"; return false; }

            if (!ctx.Rules.CanActivateEffect(speed, state, timing, isControllerTurn))
            { reason = "Activation not allowed at this timing"; return false; }

            if (!ServiceLocator.TryGet<IChainManager>(out var chain) || chain == null)
            { reason = "Chain manager missing"; return false; }

            return true;
        }

        public override bool Execute(ActionContext ctx, out string error)
        {
            error = "";
            if (!Validate(ctx, out error)) return false;

            var card = ActionUtil.ResolveCard(ctx, sourceInstanceId, seat, out error);
            if (card == null) return false;
            var def = card.Def;

            // Move Normal/Quick-Play Spells Hand → STZ face-up on activation
            if (card.CurrentZone == BoardManager.CardZone.Hand && def.IsSpell)
            {
                var z = ctx.Board.Zones[(int)seat];
                int free = FindFreeSTIndex(z.SpellsTraps);
                if (free < 0) { error = "No free S/T zone"; return false; }

                if (!z.Hand.Remove(card)) { error = "Failed to leave Hand"; return false; }
                if (!z.SpellsTraps[free].Add(card)) { error = "Failed to enter STZ"; return false; }

                var from = new BoardManager.ZoneId(seat, BoardManager.CardZone.Hand);
                var to   = new BoardManager.ZoneId(seat, BoardManager.CardZone.SpellTrap, free);

                card.SetController(seat);
                card.CurrentZone = BoardManager.CardZone.SpellTrap;
                card.ZoneIndex   = free;
                card.FlipFaceUp(true);

                if (ServiceLocator.TryGet<EventBus>(out var busMv) && busMv != null)
                    busMv.RaiseCardMoved(card, new ZoneMove(from, to));

                if (ServiceLocator.TryGet<EventBus>(out var busFace) && busFace != null)
                    busFace.RaiseCardFaceChanged(card, true, FaceChangeReason.Manual);
            }
            // If already in STZ and face-down, flip face-up as part of activation
            else if (card.CurrentZone == BoardManager.CardZone.SpellTrap && !card.IsFaceUp)
            {
                card.FlipFaceUp(true);
                if (ServiceLocator.TryGet<EventBus>(out var busFace2) && busFace2 != null)
                    busFace2.RaiseCardFaceChanged(card, true, FaceChangeReason.Manual);
            }

            var handle = def.GetHandleFromBlueprint(card, effectId);
            if (!ServiceLocator.TryGet<IChainManager>(out var chain) || chain == null)
            { error = "Chain manager missing"; return false; }

            var addArgs = new AddLinkArgs(
                activator: seat,
                source: card,
                sourceId: card.InstanceId,
                isCardSource: true,
                effect: handle,
                targets: new List<ITargetRef>(0), // pass your locked targets if any
                timing: timing,
                summaryOverride: null
            );

            // ActivateSpellTrapAction.Execute(...)
            if (!chain.TryAddLink(addArgs, out var link, out var why))
            { error = $"Chain rejected: {why}"; return false; }

// DEV: auto-resolve immediately for testing
            chain.ResolveAll();

            if (ServiceLocator.TryGet<EventBus>(out var bus) && bus != null)
                bus.RaiseCardActivated(card, handle.Speed, effectId);

            ctx.Logger?.LogText("Action.Activate",
                $"Activate {card.Name} (SS{(int)handle.Speed})",
                data:$"effect={effectId}", source:nameof(ActivateSpellTrapAction));

            return true;
        }

        // Works with both new Top() and legacy .Card
        private int FindFreeSTIndex(object[] st)
        {
            if (st == null) return -1;
            for (int i = 0; i < st.Length; i++)
            {
                var mTop = st[i].GetType().GetMethod("Top");
                if (mTop != null && mTop.Invoke(st[i], null) == null) return i;

                var fld = st[i].GetType().GetField("Card");
                if (fld != null && fld.GetValue(st[i]) == null) return i;
            }
            return -1;
        }
    }
}