using UnityEngine;
using TileEffects;

namespace Equipment.Effects
{
    /// <summary>
    /// Places a persistent fire effect on the context's target cell (e.g. the cell the
    /// wearer just left). Requires the board's TileEffectSystem to be present; without one
    /// (editor-only tests) the trigger resolves but places nothing.
    /// </summary>
    [CreateAssetMenu(fileName = "PlaceFireTileEffect", menuName = "Arcrelium/Equipment Effects/Place Fire Tile")]
    public class PlaceFireTileEffect : EquipmentEffect
    {
        public TileEffectSO FireDefinition;

        public override void Execute(EquipmentContext context)
        {
            if (context.TargetCell == null || FireDefinition == null) return;
            var system = TileEffectSystem.Instance;
            if (system == null) return;

            system.PlaceEffect(context.TargetCell, FireDefinition);
        }
    }
}