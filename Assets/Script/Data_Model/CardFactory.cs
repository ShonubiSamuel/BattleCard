// CardFactory.cs (optional)
using YGO.Duel.Data;
using YGO.Duel.Model;
using YGO.Duel.Board;

namespace YGO.Duel.Data
{
    public static class CardFactory
    {
        public static CardRuntime Instantiate(CardDefinition def, BoardManager.Seat owner)
            => new CardRuntime(def, owner);
    }
}