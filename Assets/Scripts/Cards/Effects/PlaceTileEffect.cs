using UnityEngine;
using HexGrid;
using TileEffects;

namespace Cards.Effects
{
    /// <summary>
    /// Places a persistent tile effect (oil, fire, rock...) on the targeted cell. The exact
    /// effect is a reference to a TileEffectSO asset (e.g. Environment/Oil), so the card
    /// authoring decides what appears: "Grease" sets Definition to the Oil asset.
    /// Requires the board's TileEffectSystem to be present; without one (editor-only tests)
    /// the play resolves but places nothing.
    /// </summary>
    [CreateAssetMenu(fileName = "PlaceTileEffect", menuName = "Arcrelium/Effects/Place Tile Effect")]
    public class PlaceTileEffect : CardEffectSO
    {
        public TileEffectSO Definition;

        public override bool CanExecute(CardContext context)
        {
            if (context.TargetCell == null) return false;
            if (context.Source == null || context.Source.CurrentCell == null) return false;

            return context.Source.CurrentCell.Coordinates.DistanceTo(context.TargetCell.Coordinates)
                <= context.EffectiveRange;
        }

        public override void Execute(CardContext context)
        {
            if (context.TargetCell == null || Definition == null) return;
            var system = TileEffectSystem.Instance;
            if (system == null) return;

            // Don't stack the same definition on a tile — one oil stays one oil, and a
            // second splash of fire on the same tile should not double its burn.
            if (context.TargetCell.HasEffect(Definition)) return;

            system.PlaceEffect(context.TargetCell, Definition);
        }
    }
}