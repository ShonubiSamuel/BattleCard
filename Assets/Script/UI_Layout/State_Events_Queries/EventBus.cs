// EventBus.cs
// Central event hub: card movement, summons, destruction, LP changes, phases/turns, chain, battle.
// Register this in ServiceLocator (recommended): ServiceLocator.Register(EventBus.Global);

using System;
using System.Collections.Generic;
using UnityEngine;
using YGO.Duel.Board;
using YGO.Duel.Foundation;
using YGO.Duel.Rules;
using YGO.Duel.Chain;

using Card = YGO.Duel.Cards.Card;

namespace YGO.Duel.Foundation
{
    // -------- Event payloads --------

    public enum SummonType { Normal, Tribute, Flip, Special }
    public enum DestroyReason { Battle, Effect, Rule, Cost, Release, SendToGY }
    public enum FaceChangeReason { Manual, Effect, BattleFlip }
    
    public readonly struct ZoneMove
    {
        public readonly BoardManager.ZoneId From;
        public readonly BoardManager.ZoneId To;
        public ZoneMove(BoardManager.ZoneId from, BoardManager.ZoneId to) { From = from; To = to; }
        public override string ToString() => $"{From} → {To}";
    }

    public sealed class CardMovedEvent : EventArgs
    {
        public Card Card;
        public ZoneMove Move;
    }
    public sealed class SummonEvent : EventArgs
    {
        public Card Card;
        public BoardManager.Seat Controller;
        public SummonType Type;
        public int ZoneIndex; // MZ index if relevant
    }
    public sealed class DestroyEvent : EventArgs
    {
        public Card Card;
        public DestroyReason Reason;
        public BoardManager.Seat FormerController;
    }
    public sealed class LifePointsChangedEvent : EventArgs
    {
        public BoardManager.Seat Seat;
        public int Previous;
        public int Current;
        public int Delta => Current - Previous;
    }
    public sealed class PhaseChangedEvent : EventArgs
    {
        public RuleSet.Phase Previous;
        public RuleSet.Phase Current;
        public BoardManager.Seat TurnPlayer;
        public int TurnNumber;
    }
    public sealed class TurnEvent : EventArgs
    {
        public BoardManager.Seat TurnPlayer;
        public int TurnNumber;
    }
    public sealed class ChainLinkEvent : EventArgs
    {
        public ChainLink Link;
    }
    public sealed class ChainClearedEvent : EventArgs { }
    
    // EventBus.cs  — replace/expand the AttackDeclaredEvent payload
    public sealed class AttackDeclaredEvent : EventArgs
    {
        public YGO.Duel.Battle.IBattler Attacker;   // logic side
        public YGO.Duel.Battle.IBattler Target;     // null = direct

        public YGO.Duel.Cards.Card AttackerCard;    // convenience for visuals
        public YGO.Duel.Cards.Card TargetCard;      // null for direct
    }
    
    
    public sealed class BattleDamageEvent : EventArgs
    {
        public BoardManager.Seat Victim;
        public int Amount;
    }
    
    
    public sealed class CardsDrawnEvent : EventArgs
    {
        public BoardManager.Seat Seat;
        public IReadOnlyList<Card> Cards;
        public string Reason;
    }

    public sealed class CardsDiscardedEvent : EventArgs
    {
        public BoardManager.Seat Seat;
        public IReadOnlyList<Card> Cards;
        public string Reason;
    }
    
    // EventBus.cs — add near other payloads
    

    public sealed class CardFaceChangedEvent : EventArgs
    {
        public Card Card;
        public bool IsFaceUp;
        public FaceChangeReason Reason;
    }


    
  

    // -------- EventBus --------

    public sealed class EventBus
    {
        public static EventBus Global { get; } = new EventBus();

        private readonly DuelLogger _logger;

        public EventBus(DuelLogger logger = null) { _logger = logger ?? new DuelLogger(); }

        // Card lifecycle
        public event EventHandler<CardMovedEvent> OnCardMoved;
        public event EventHandler<SummonEvent> OnSummoned;
        public event EventHandler<DestroyEvent> OnDestroyed;

        // LP / turn / phase
        public event EventHandler<LifePointsChangedEvent> OnLifePointsChanged;
        public event EventHandler<TurnEvent> OnTurnStarted;
        public event EventHandler<TurnEvent> OnTurnEnded;
        public event EventHandler<PhaseChangedEvent> OnPhaseChanged;

        // Chain
        public event EventHandler<ChainLinkEvent> OnChainLinkAdded;
        public event EventHandler<ChainLinkEvent> OnChainResolved;
        public event EventHandler<ChainClearedEvent> OnChainCleared;

        // Battle
        public event EventHandler<AttackDeclaredEvent> OnAttackDeclared;
        public event EventHandler<BattleDamageEvent>  OnBattleDamage;
        
        // Aggregated multi-card events
        public event EventHandler<CardsDrawnEvent>    OnCardsDrawn;
        public event EventHandler<CardsDiscardedEvent> OnCardsDiscarded;
        
        public event EventHandler<CardFaceChangedEvent> OnCardFaceChanged;


        // ---- Raise helpers (wrap + log) ----

        public void RaiseCardMoved(Card card, ZoneMove mv)
        {
            _logger.LogText("Event.CardMoved", $"{card?.Name} {mv}", source: nameof(EventBus));
            OnCardMoved?.Invoke(this, new CardMovedEvent { Card = card, Move = mv });
        }

        public void RaiseSummoned(Card card, BoardManager.Seat ctrl, SummonType type, int mzIndex)
        {
            _logger.LogText("Event.Summon", $"{type} {card?.Name} @MZ[{mzIndex}] P{(ctrl==BoardManager.Seat.P1?1:2)}", source: nameof(EventBus));
            OnSummoned?.Invoke(this, new SummonEvent { Card = card, Controller = ctrl, Type = type, ZoneIndex = mzIndex });
        }

        public void RaiseCardDestroyed(Card card, DestroyReason reason, BoardManager.Seat former)
        {
            _logger.LogText("Event.Destroy", $"{card?.Name} reason={reason}", source: nameof(EventBus));
            OnDestroyed?.Invoke(this, new DestroyEvent { Card = card, Reason = reason, FormerController = former });
        }

        public void RaiseLPChanged(BoardManager.Seat seat, int previous, int current)
        {
            _logger.LogText("Event.LP", $"P{(seat==BoardManager.Seat.P1?1:2)} {previous}→{current} ({current-previous:+#;-#;0})", source: nameof(EventBus));
            OnLifePointsChanged?.Invoke(this, new LifePointsChangedEvent { Seat = seat, Previous = previous, Current = current });
        }

        public void RaisePhaseChanged(RuleSet.Phase prev, RuleSet.Phase cur, BoardManager.Seat tp, int turn)
        {
            _logger.LogText("Event.Phase", $"{prev}→{cur} (P{(tp==BoardManager.Seat.P1?1:2)} T{turn})", source: nameof(EventBus));
            OnPhaseChanged?.Invoke(this, new PhaseChangedEvent { Previous = prev, Current = cur, TurnPlayer = tp, TurnNumber = turn });
        }

        public void RaiseTurnStarted(BoardManager.Seat tp, int turn)
        {
            _logger.LogText("Event.TurnStart", $"P{(tp==BoardManager.Seat.P1?1:2)} T{turn}", source: nameof(EventBus));
            OnTurnStarted?.Invoke(this, new TurnEvent { TurnPlayer = tp, TurnNumber = turn });
        }

        public void RaiseTurnEnded(BoardManager.Seat tp, int turn)
        {
            _logger.LogText("Event.TurnEnd", $"P{(tp==BoardManager.Seat.P1?1:2)} T{turn}", source: nameof(EventBus));
            OnTurnEnded?.Invoke(this, new TurnEvent { TurnPlayer = tp, TurnNumber = turn });
        }

        public void RaiseChainLinkAdded(ChainLink link)
        {
            _logger.LogText("Event.ChainAdd", link?.ToString() ?? "(null)", source: nameof(EventBus));
            OnChainLinkAdded?.Invoke(this, new ChainLinkEvent { Link = link });
        }

        public void RaiseChainResolved(ChainLink link)
        {
            _logger.LogText("Event.ChainResolve", link?.ToString() ?? "(null)", source: nameof(EventBus));
            OnChainResolved?.Invoke(this, new ChainLinkEvent { Link = link });
        }

        public void RaiseChainCleared()
        {
            _logger.LogText("Event.ChainCleared", "Chain empty", source: nameof(EventBus));
            OnChainCleared?.Invoke(this, new ChainClearedEvent());
        }

        public void RaiseBattleDamage(BoardManager.Seat victim, int amount)
        {
            _logger.LogText("Event.BattleDamage", $"P{(victim==BoardManager.Seat.P1?1:2)} -{amount}", source: nameof(EventBus));
            OnBattleDamage?.Invoke(this, new BattleDamageEvent { Victim = victim, Amount = amount });
        }
        
        public void RaiseCardsDrawn(BoardManager.Seat seat, IReadOnlyList<Card> cards, string reason)
        {
            _logger.LogText("Event.CardsDrawn",
                $"P{(seat==BoardManager.Seat.P1?1:2)} drew {(cards?.Count ?? 0)}",
                data:$"reason={reason}", source: nameof(EventBus));

            OnCardsDrawn?.Invoke(this, new CardsDrawnEvent { Seat = seat, Cards = cards, Reason = reason });

            // Also emit per-card movement (Deck -> Hand) for listeners that only track moves
            var from = new BoardManager.ZoneId(seat, BoardManager.CardZone.Deck);
            var to   = new BoardManager.ZoneId(seat, BoardManager.CardZone.Hand);
            if (cards != null)
                foreach (var c in cards) RaiseCardMoved(c, new ZoneMove(from, to));
        }

        public void RaiseCardsDiscarded(BoardManager.Seat seat, IReadOnlyList<Card> cards, string reason)
        {
            _logger.LogText("Event.CardsDiscarded",
                $"P{(seat==BoardManager.Seat.P1?1:2)} discarded {(cards?.Count ?? 0)}",
                data:$"reason={reason}", source: nameof(EventBus));

            OnCardsDiscarded?.Invoke(this, new CardsDiscardedEvent { Seat = seat, Cards = cards, Reason = reason });

            // Also emit per-card movement (Hand -> GY) for listeners that only track moves
            var from = new BoardManager.ZoneId(seat, BoardManager.CardZone.Hand);
            var to   = new BoardManager.ZoneId(seat, BoardManager.CardZone.Graveyard);
            if (cards != null)
                foreach (var c in cards) RaiseCardMoved(c, new ZoneMove(from, to));
        }
        
        // EventBus.cs  — add this overload (or replace your existing RaiseAttackDeclared)
        public void RaiseAttackDeclared(YGO.Duel.Battle.IBattler attacker, YGO.Duel.Battle.IBattler target)
        {
            _logger.LogText("Event.AttackDeclared", target == null ? "Direct" : "Targeted", source: nameof(EventBus));

            YGO.Duel.Cards.Card aCard = null, tCard = null;
            if (attacker is YGO.Duel.Battle.CardBattlerAdapter ca) aCard = ca.RuntimeCard;
            if (target   is YGO.Duel.Battle.CardBattlerAdapter ct) tCard = ct.RuntimeCard;

            OnAttackDeclared?.Invoke(this, new AttackDeclaredEvent
            {
                Attacker = attacker,
                Target   = target,
                AttackerCard = aCard,
                TargetCard   = tCard
            });
        }
        public void RaiseCardFaceChanged(Card card, bool isFaceUp, FaceChangeReason reason)
        {
            _logger.LogText("Event.Face", $"{card?.Name} {(isFaceUp ? "Face-Up" : "Face-Down")} ({reason})",
                source: nameof(EventBus));
            OnCardFaceChanged?.Invoke(this, new CardFaceChangedEvent { Card = card, IsFaceUp = isFaceUp, Reason = reason });
        }
        
        // EventBus.cs — add near other “Chain” helpers
        public void RaiseCardActivated(Card card, RuleSet.SpellSpeed speed, string effectId = "")
        {
            _logger.LogText("Event.Activate",
                $"{card?.Name} (Speed {((int)speed)})",
                data: $"effect={effectId}", source: nameof(EventBus));
            // You can also RaiseChainLinkAdded(...) here if your ChainLink carries the same info.
        }

        public void RaiseCardEffectResolved(Card card, string effectId = "")
        {
            _logger.LogText("Event.EffectResolved",
                $"{card?.Name}",
                data: $"effect={effectId}", source: nameof(EventBus));
            // And then RaiseChainResolved(...) if you keep the ChainLink around.
        }
 

    }
}