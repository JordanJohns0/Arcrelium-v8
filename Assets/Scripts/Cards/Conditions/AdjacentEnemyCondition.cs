using Characters;
using HexGrid;

namespace Cards.Conditions
{
    [UnityEngine.CreateAssetMenu(fileName = "AdjacentEnemyCondition", menuName = "Arcrelium/Conditions/Adjacent Enemy")]
    public class AdjacentEnemyCondition : CardConditionSO
    {
        public override bool Evaluate(CardContext context)
        {
            if (context.Source == null || context.Grid == null) return false;
            if (context.Source.CurrentCell == null) return false;

            foreach (HexCell neighbor in context.Grid.GetNeighbors(context.Source.CurrentCell.Coordinates))
            {
                Character occupant = neighbor.Occupant;
                if (occupant != null && occupant.IsEnemyOf(context.Source))
                    return true;
            }
            return false;
        }
    }
}
