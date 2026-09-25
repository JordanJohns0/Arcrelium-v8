using UnityEngine;

namespace TileEffects
{
    /// <summary>
    /// Fire: burns anyone who enters or starts a turn on the tile, lasts Duration rounds,
    /// and explodes when placed on an oil tile (consuming the oil).
    /// </summary>
    [CreateAssetMenu(fileName = "FireTileEffect", menuName = "Arcrelium/Tile Effects/Fire")]
    public class FireTileEffectSO : TileEffectSO
    {
        public int FireDamage = 2;
        public int ExplosionDamage = 3;
        public int ExplosionRadius = 1;

        public override void OnCharacterEntered(TileEffectContext context)
        {
            if (context.Character == null || !context.Character.Stats.IsAlive) return;
            context.Character.TakeDamage(FireDamage);
            Debug.Log($"[TileEffects] {context.Character.name} steps into fire on {context.Cell.Coordinates} and takes {FireDamage} damage.");
        }

        public override void OnCharacterTurnStarted(TileEffectContext context)
        {
            if (context.Character == null || !context.Character.Stats.IsAlive) return;
            context.Character.TakeDamage(FireDamage);
            Debug.Log($"[TileEffects] {context.Character.name} starts their turn in fire on {context.Cell.Coordinates} and takes {FireDamage} damage.");
        }

        public override void OnEffectAddedToCell(TileEffectContext context)
        {
            if (context.Cell != null
                && context.Cell.HasEffect(effect => effect.Slippery)
                && context.System != null)
            {
                context.System.IgniteOil(context.Cell, ExplosionDamage, ExplosionRadius);
            }
        }
    }
}