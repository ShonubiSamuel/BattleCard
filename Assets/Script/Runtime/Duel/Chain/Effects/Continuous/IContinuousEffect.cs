// IContinuousEffect.cs
using System.Collections.Generic;
using YGO.Duel.Foundation;
using YGO.Duel.Cards;
using YGO.Duel.Board;

namespace YGO.Duel.Effects
{
    public interface IStatModifier
    {
        bool AppliesTo(Card card);
        int DeltaATK(Card card);
        int DeltaDEF(Card card);
    }

    public interface IContinuousEffect
    {
        IEnumerable<IStatModifier> GetStatModifiers();
        void OnInstall(EventBus bus);
        void OnUninstall(EventBus bus);
    }

    // Example stat modifier: +X ATK for certain races and a controller
    public sealed class AtkBuffForRaces : IStatModifier
    {
        private readonly BoardManager.Seat _owner;
        private readonly int _bonus;
        public AtkBuffForRaces(BoardManager.Seat owner, int bonus) { _owner = owner; _bonus = bonus; }

        public bool AppliesTo(Card c)
        {
            if (c == null || !c.IsMonsterRuntime) return false;
            if (c.Controller != _owner) return false;
            var race = c.Def?.monsterRace?.name ?? "";
            return race.Contains("Beast") || race.Contains("Beast-Warrior") || race.Contains("Plant"); // simple tag check
        }

        public int DeltaATK(Card c) => AppliesTo(c) ? _bonus : 0;
        public int DeltaDEF(Card c) => 0;
    }
}