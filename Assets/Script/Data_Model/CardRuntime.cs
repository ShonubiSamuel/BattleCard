// CardRuntime.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using YGO.Duel.Board;  // for Seat enums if you want to link
using YGO.Duel.Data;

namespace YGO.Duel.Model
{
    [Serializable]
    public sealed class CardRuntime
    {
        // Identity
        public Guid instanceGuid = Guid.NewGuid();
        public CardDefinition def;

        // Control/visibility
        public BoardManager.Seat owner;
        public BoardManager.Seat controller;

        public bool isFaceUp = true;
        public bool isSet    = false;

        // Position / combat (use your BattlePosition enum if you have one)
        public YGO.Duel.Battle.BattlePosition position = YGO.Duel.Battle.BattlePosition.Attack;

        // Zone tracking (optional: mirror your BoardManager zone enum if desired)
        public string currentZone = "Deck"; // Deck, Hand, Monster, SpellTrap, Graveyard, Banished, Extra, Field, Pendulum

        // Dynamic stats
        public int currentATK;
        public int currentDEF;

        // Flags / counters
        public bool hasAttackedThisTurn = false;
        public Dictionary<string, int> counters = new Dictionary<string, int>();

        public CardRuntime(CardDefinition definition, BoardManager.Seat ownerSeat)
        {
            def = definition;
            owner = controller = ownerSeat;
            ResetToBaseStats();
        }

        public void ResetToBaseStats()
        {
            currentATK = Mathf.Max(-1, def.baseATK);
            currentDEF = Mathf.Max(-1, def.baseDEF);
        }

        public override string ToString()
        {
            var name = def != null ? def.cardName : "Unknown";
            return $"{name} [{currentZone}] P{(controller == BoardManager.Seat.P1 ? "1" : "2")} {(isFaceUp ? "Face-up" : "Set")}";
        }

        // Example helpers you can wire to your systems:
        public bool IsMonster => def != null && def.IsMonster;
        public bool IsSpell   => def != null && def.IsSpell;
        public bool IsTrap    => def != null && def.IsTrap;
    }
}
