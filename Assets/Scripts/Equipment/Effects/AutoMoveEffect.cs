using UnityEngine;
using FantasyBrawl.HexGrid;

namespace FantasyBrawl.Equipment.Effects
{
    /// <summary>
    /// Moves the equipment owner to a random valid nearby cell.
    /// </summary>
    [CreateAssetMenu(fileName = "AutoMoveEffect", menuName = "FantasyBrawl/Equipment Effects/Auto Move")]
    public class AutoMoveEffect : EquipmentEffect
    {
        public int Range = 1;

        public override void Execute(EquipmentContext context)
        {
            if (context.Source == null || context.Grid == null) return;
            if (context.Source.CurrentCell == null) return;

            HexCoordinates origin = context.Source.CurrentCell.Coordinates;
            var validCells = new System.Collections.Generic.List<HexCell>();

            foreach (HexCell cell in context.Grid.GetCellsInRange(origin, Range))
            {
                if (cell == context.Source.CurrentCell) continue;
                if (!cell.IsWalkable) continue;
                validCells.Add(cell);
            }

            if (validCells.Count > 0)
            {
                HexCell destination = validCells[Random.Range(0, validCells.Count)];
                context.Source.PlaceOnCell(destination);
            }
        }
    }
}
