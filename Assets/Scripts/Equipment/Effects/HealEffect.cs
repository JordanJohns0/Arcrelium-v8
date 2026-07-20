using UnityEngine;
using Characters;

namespace Equipment.Effects
{
    /// <summary>
    /// Heals the equipment owner by a flat amount.
    /// </summary>
    [CreateAssetMenu(fileName = "HealEffect", menuName = "/Equipment Effects/Heal")]
    public class HealEffect : EquipmentEffect
    {
        public int Amount = 1;

        public override void Execute(EquipmentContext context)
        {
            if (context.Source == null) return;
            context.Source.Stats.Heal(Amount);
        }
    }
}
