// SimpleCardStatProvider.cs
using YGO.Duel.UI;
using Card = YGO.Duel.Cards.Card;

public sealed class SimpleCardStatProvider : ICardStatProvider
{
    public bool TryGetStats(Card card, out int atk, out int def, out int level, out string typeLine)
    {
        atk = def = level = 0; typeLine = "";
        if (card?.Def == null) return false;

        if (card.Def.IsMonster)
        {
            level = card.Def.level;
            atk   = card.Def.baseATK >= 0 ? card.Def.baseATK : -1;
            def   = card.Def.baseDEF >= 0 ? card.Def.baseDEF : -1;
            typeLine = "";
        }
        else
        {
            // Spells/Traps → no ATK/DEF; show a type line instead
            atk = def = -1;
            level = 0;
            typeLine = card.Def.IsSpell ? "Spell" : (card.Def.IsTrap ? "Trap" : "");
        }
        return true;
    }

    public string GetDisplayName(Card card) => card?.Def?.cardName ?? card?.Name ?? "(Card)";
}