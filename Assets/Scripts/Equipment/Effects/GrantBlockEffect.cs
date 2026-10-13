using UnityEngine;
using Characters;

namespace Equipment.Effects
{
    /// <summary>
    /// Grants the equipment owner block by a flat amount. Routes through
    /// Character.GainBlock so OnBlockGained triggers (e.g. Restorative Shield's
    /// heal-on-gain-block) also fire when block comes from equipment.
    /// </summary>
    [CreateAssetMenu(fileName = "GrantBlockEffect", menuName = "Arcrelium/Equipment Effects/Grant Block")]
    public class GrantBlockEffect : EquipmentEffect
    {
        public int Amount = 1;

        public override void Execute(EquipmentContext context)
        {
            if (context.Source == null) return;
            context.Source.GainBlock(Amount);
        }
    }
}