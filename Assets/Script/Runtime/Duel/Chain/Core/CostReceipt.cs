using System.Collections.Generic;

namespace YGO.Duel.Chain
{
    public sealed class CostReceipt
    {
        public string Description;         // “Paid 500 LP”, “Tributed 1 monster”
        public int Amount;                 // numeric amount (LP, counters, etc.)
        public List<string> CardNames;     // cards used/tributed
        public string PayerSeat;           // “P1” / “P2”
        public string Extra;               // free-form (e.g., “from hand”, “from field”)

        public override string ToString()
        {
            var list = CardNames ?? new List<string>();
            return $"{Description} (Amt={Amount}, By={PayerSeat}, Cards=[{string.Join(", ", list)}])";
        }
    }
}