using UnityEngine;

namespace TileEffects
{
    /// <summary>
    /// Oil: slippery ground that slides a character one tile past it when they enter, and
    /// explodes the moment fire is placed on the tile (consuming the oil).
    /// The Slippery flag (set on the asset) drives both behaviors.
    /// </summary>
    [CreateAssetMenu(fileName = "OilTileEffect", menuName = "Arcrelium/Tile Effects/Oil")]
    public class OilTileEffectSO : TileEffectSO
    {
    }
}