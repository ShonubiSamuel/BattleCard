// IBattlerResolver.cs
using YGO.Duel.Cards;

namespace YGO.Duel.Battle
{
    public interface IBattlerResolver
    {
        IBattler Resolve(Card card);
    }
}