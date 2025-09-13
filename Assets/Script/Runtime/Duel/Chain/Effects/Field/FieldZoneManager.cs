// FieldZoneManager.cs
using YGO.Duel.Board;
using YGO.Duel.Cards;
using YGO.Duel.Foundation;

namespace YGO.Duel.Effects
{
    public sealed class FieldZoneManager
    {
        private readonly BoardManager _board;
        private readonly DuelLogger _log;
        private readonly ContinuousEffectService _effects;

        public FieldZoneManager(BoardManager board, DuelLogger log, ContinuousEffectService continuous)
        {
            _board = board;
            _log = log ?? new DuelLogger();
            _effects = continuous;
        }

        public bool PlaceField(BoardManager.Seat seat, Card fieldCard, out string why)
        {
            why = "";
            var z = _board.Zones[(int)seat];

            if (z.Field == null) { why = "Field zone disabled"; return false; }

            // If a field already exists, send it away (GY by rule)
            var cur = z.Field.Top();
            if (cur != null)
            {
                // uninstall any continuous layer tied to old field
                _effects.UninstallAll(cur);
                z.Field.RemoveTop(); // remove from field
                z.Graveyard.Add(cur);
            }

            // Place the new field (assumes the card is already moved to STZ/Field by the action if needed)
            z.Field.Add(fieldCard);
            fieldCard.CurrentZone = BoardManager.CardZone.Field;
            fieldCard.ZoneIndex = 0;
            fieldCard.FlipFaceUp(true);

            _log.LogText("Field.Replace", $"{fieldCard.Name} @ {seat}", source:nameof(FieldZoneManager));
            return true;
        }
    }
}