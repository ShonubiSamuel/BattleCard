using UnityEngine;
using YGO.Duel.Foundation;
using YGO.Duel.UI;
using Card = YGO.Duel.Cards.Card;
using Def  = YGO.Duel.Data.CardDefinition;

public sealed class SimpleCardStatProvider : ICardStatProvider
{

    public string GetDisplayName(Card card)
    {
        var d = card?.Def;
        return d ? (d.cardName) : card?.ToString() ?? "(null)";
    }

    public bool TryGetStats(Card card, out int atk, out int def, out int level, out string typeLine)
    {
        atk = def = level = -1; typeLine = "";
        var d = card?.Def;
        if (!d) return false;

        // Monster?
        if (d.IsMonster)
        {
            // Map to runtime-friendly values (we added these names earlier)
            level = (d.level > 0) ? d.level : (d.rank > 0 ? d.rank : d.linkRating);
            atk   = d.baseATK;
            def   = d.baseDEF; // will be -1 for Links
            typeLine = BuildTypeLine(d);
            return true;
        }

        // Non-monsters: return type line only
        typeLine = BuildTypeLine(d);
        return true;
    }

    private static string BuildTypeLine(Def d)
    {
        if (d.IsMonster)
        {
            var attr = d.attribute ? d.attribute.displayName : "";
            var race = d.monsterRace ? d.monsterRace.displayName : "";
            return string.IsNullOrEmpty(attr) && string.IsNullOrEmpty(race) ? "Monster" : $"{attr}/{race}";
        }
        else if (d.IsSpell) return "Spell";
        else if (d.IsTrap)  return "Trap";
        return "";
    }
}