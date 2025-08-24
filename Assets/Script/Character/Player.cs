// // Player.cs
// // Runtime player facade: LP, flags, draw helper, optional per-player timer.
// // Wraps BoardManager state to avoid duplicating the source of truth.
//
// using System;
// using System.Collections.Generic;
// using UnityEngine;
// using YGO.Duel.Board;
// using YGO.Duel.Foundation;
// using YGO.Duel.Rules;
// using Card = YGO.Duel.Cards.Card;
//
// namespace YGO.Duel.Runtime
// {
//     public sealed class Player
//     {
//         public readonly BoardManager.Seat Seat;
//         private readonly BoardManager _board;
//         private readonly DuelLogger _logger;
//
//         // Optional delegate supplied by TurnManager so we can answer IsTurnPlayer.
//         private Func<bool> _isTurnPlayerProvider = null;
//
//         // Per-player decision time bank (optional; TurnManager can still run a global per-turn timer)
//         public bool UseTimeBank { get; set; } = false;
//         public float TimeBankSeconds { get; private set; } = 0f;
//
//         // Convenience accessors
//         private BoardManager.PlayerState PS => _board.Players[(int)Seat];
//         public string DisplayName => PS.DisplayName;
//
//         public int LifePoints
//         {
//             get => PS.LifePoints;
//             private set => PS.LifePoints = Mathf.Max(0, value);
//         }
//
//         public bool NormalSummonUsedThisTurn
//         {
//             get => PS.NormalSummonUsedThisTurn;
//             set => PS.NormalSummonUsedThisTurn = value;
//         }
//
//         public bool IsTurnPlayer => _isTurnPlayerProvider != null && _isTurnPlayerProvider();
//
//         public Player(BoardManager board, BoardManager.Seat seat, DuelLogger logger)
//         {
//             _board  = board  ?? throw new ArgumentNullException(nameof(board));
//             _logger = logger ?? new DuelLogger();
//             Seat    = seat;
//         }
//
//         /// <summary>Let TurnManager inject a provider so this player knows whether it's currently their turn.</summary>
//         public void SetIsTurnPlayerProvider(Func<bool> provider) => _isTurnPlayerProvider = provider;
//
//         /// <summary>Subtract LP; returns false if not enough LP when 'strict' is true.</summary>
//         public bool PayLP(int amount, bool strict = false)
//         {
//             amount = Mathf.Max(0, amount);
//             if (strict && LifePoints < amount) return false;
//
//             int before = LifePoints;
//             LifePoints = before - amount;
//
//             _logger.LogText("LP.Pay", $"{DisplayName} pays {amount} LP",
//                 data: $"before={before}; after={LifePoints}; seat={Seat}", source: nameof(Player));
//             return true;
//         }
//
//         /// <summary>Draw N cards (silent no-op if deck is empty). Returns the drawn cards (may be fewer than N).</summary>
//         public List<Card> Draw(int n)
//         {
//             var list = new List<Card>(Mathf.Max(0, n));
//             for (int i = 0; i < n; i++)
//             {
//                 var c = _board.DrawOne(Seat);
//                 if (c != null)
//                 {
//                     list.Add(c);
//                     _logger.LogText("Draw.Card", $"{DisplayName} drew 1 card",
//                         data: $"card={c.Name}; hand={_board.Zones[(int)Seat].Hand.Count}", source: nameof(Player));
//                 }
//                 else
//                 {
//                     _logger.LogText("Draw.Empty", $"{DisplayName} attempted to draw from empty deck",
//                         source: nameof(Player));
//                     break;
//                 }
//             }
//             return list;
//         }
//
//         // ---- Optional per-player time bank (e.g., chess clock style) ----
//
//         public void SetTimeBank(float seconds)
//         {
//             TimeBankSeconds = Mathf.Max(0f, seconds);
//             UseTimeBank = seconds > 0f;
//         }
//
//         public void TickTimeBank(float deltaTime)
//         {
//             if (!UseTimeBank || TimeBankSeconds <= 0f) return;
//             TimeBankSeconds = Mathf.Max(0f, TimeBankSeconds - Mathf.Max(0f, deltaTime));
//             if (Mathf.Approximately(TimeBankSeconds, 0f))
//                 _logger.LogText("Timer.TimeBankEmpty", $"{DisplayName}'s time bank expired", source: nameof(Player));
//         }
//
//         // ---- Helpers to access zones quickly ----
//
//         public BoardManager.PlayerZones Z => _board.Zones[(int)Seat];
//
//         public override string ToString() => $"{DisplayName} (P{(Seat==BoardManager.Seat.P1 ? "1":"2")})";
//     }
// }
