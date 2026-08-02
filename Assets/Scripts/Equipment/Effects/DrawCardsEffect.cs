using UnityEngine;

namespace Equipment.Effects
{
    /// <summary>
    /// Draws extra cards for the equipment owner.
    /// </summary>
    [CreateAssetMenu(fileName = "DrawCardsEffect", menuName = "Arcrelium/Equipment Effects/Draw Cards")]
    public class DrawCardsEffect : EquipmentEffect
    {
        public int Amount = 1;

        public override void Execute(EquipmentContext context)
        {
            if (context.Source == null) return;
            context.Source.DrawCards(Amount);
        }
    }
}
