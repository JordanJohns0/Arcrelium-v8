using UnityEngine;
using HexGrid;

namespace Equipment.Effects
{
    /// <summary>
    /// Deals damage to all enemies within range of the equipment owner.
    /// </summary>
    [CreateAssetMenu(fileName = "AOEDamageEffect", menuName = "/Equipment Effects/AOE Damage")]
    public class AOEDamageEffect : EquipmentEffect
    {
        public int Damage = 1;
        public int Range = 1;

        public override void Execute(EquipmentContext context)
        {
            if (context.Source == null || context.Grid == null) return;

            HexCoordinates origin = context.Source.CurrentCell.Coordinates;

            foreach (HexCell cell in context.Grid.GetCellsInRange(origin, Range))
            {
                if (cell.Occupant == null) continue;
                if (cell.Occupant == context.Source) continue;
                if (cell.Occupant.OwnerPlayerId == context.Source.OwnerPlayerId) continue;

                cell.Occupant.TakeDamage(Damage);
            }
        }
    }
}
