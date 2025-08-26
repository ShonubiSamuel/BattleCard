// DefaultBattlerResolver.cs
using YGO.Duel.Cards;

namespace YGO.Duel.Battle
{
    public sealed class DefaultBattlerResolver : IBattlerResolver
    {
        public IBattler Resolve(Card card)
            => card == null ? null : new CardBattlerAdapter(card);
    }
}