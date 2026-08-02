using UnityEngine;
using Cards;

namespace Equipment.Effects
{
    /// <summary>
    /// Increases the effective range of movement cards by a flat amount.
    /// Fires on OnBeforeCardPlay so the bonus is applied before range validation.
    /// </summary>
    [CreateAssetMenu(fileName = "MovementRangeBoostEffect", menuName = "Arcrelium/Equipment Effects/Movement Range Boost")]
    public class MovementRangeBoostEffect : EquipmentEffect
    {
        public int BonusRange = 1;

        public override void Execute(EquipmentContext context)
        {
            context.CardContext.BonusRange += BonusRange;
        }
    }
}
