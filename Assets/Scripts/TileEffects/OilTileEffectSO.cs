using UnityEngine;

namespace TileEffects
{
    /// <summary>
    /// Oil: slippery ground that slides a character one tile past it when they enter, and
    /// explodes the moment fire is placed on the tile (consuming the oil). Symmetrically,
    /// dumping oil onto a tile that is already burning ignites it too, so order never matters.
    /// The Slippery flag (set on the asset) drives both behaviors.
    /// </summary>
    [CreateAssetMenu(fileName = "OilTileEffect", menuName = "Arcrelium/Tile Effects/Oil")]
    public class OilTileEffectSO : TileEffectSO
    {
        public override void OnEffectAddedToCell(TileEffectContext context)
        {
            if (context.Cell == null || context.System == null) return;

            foreach (TileEffectInstance existing in context.Cell.Effects)
            {
                if (existing.Data is FireTileEffectSO fire)
                {
                    context.System.IgniteOil(context.Cell, fire.ExplosionDamage, fire.ExplosionRadius);
                    return;
                }
            }
        }
    }
}