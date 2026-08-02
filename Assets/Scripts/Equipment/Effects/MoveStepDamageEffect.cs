using UnityEngine;
using HexGrid;
using Characters;

namespace Equipment.Effects
{
    /// <summary>
    /// Deals damage to every enemy adjacent to the owner each time they move to a new cell.
    /// Fires per movement step, so running in circles around an enemy pings multiple times.
    /// </summary>
    [CreateAssetMenu(fileName = "MoveStepDamageEffect", menuName = "Arcrelium/Equipment Effects/Move Step Damage")]
    public class MoveStepDamageEffect : EquipmentEffect
    {
        public int Damage = 1;

        public override void Execute(EquipmentContext context)
        {
            if (context.Source == null || context.TargetCell == null) return;
            if (context.Grid == null) return;

            foreach (HexCell neighbor in context.Grid.GetNeighbors(context.TargetCell.Coordinates))
            {
                Character occupant = neighbor.Occupant;
                if (occupant == null) continue;
                if (occupant == context.Source) continue;
                if (occupant.Team == context.Source.Team) continue;

                occupant.TakeDamage(Damage);
            }
        }
    }
}
