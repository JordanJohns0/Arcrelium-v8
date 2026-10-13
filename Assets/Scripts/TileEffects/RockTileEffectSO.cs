using UnityEngine;

namespace TileEffects
{
    /// <summary>
    /// Rock: a full obstacle that takes up the tile, so no one can move there and routes cannot
    /// pass through it. Permanent until removed.
    /// </summary>
    [CreateAssetMenu(fileName = "RockTileEffect", menuName = "Arcrelium/Tile Effects/Rock")]
    public class RockTileEffectSO : TileEffectSO
    {
    }
}