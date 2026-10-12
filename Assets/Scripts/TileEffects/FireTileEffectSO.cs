using UnityEngine;

namespace TileEffects
{
    /// <summary>
    /// Fire: burns anyone on the tile when it appears, when they enter it, or when they start
    /// a turn there; lasts Duration rounds, and explodes when placed on an oil tile (consuming
    /// the oil).
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
                // Fire meeting oil detonates instead of burning the occupant directly; the
                // blast already damages everyone within radius (including this tile).
                context.System.IgniteOil(context.Cell, ExplosionDamage, ExplosionRadius);
                return;
            }

            var occupant = context.Character;
            if (occupant != null && occupant.Stats.IsAlive)
            {
                occupant.TakeDamage(FireDamage);
                Debug.Log($"[TileEffects] {occupant.name} is engulfed as fire appears on {context.Cell.Coordinates} and takes {FireDamage} damage.");
            }
        }
    }
}