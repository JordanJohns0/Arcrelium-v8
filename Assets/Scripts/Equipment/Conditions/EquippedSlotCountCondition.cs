using UnityEngine;

namespace Equipment
{
    [CreateAssetMenu(menuName = "Equipment/Trigger Conditions/Equipped Slot Count")]
    public class EquippedSlotCountCondition : EquipmentTriggerCondition
    {
        public EquipmentSlotType Slot = EquipmentSlotType.Trinket;
        public int Min = 1;
        [Tooltip("Set to -1 for no upper bound.")]
        public int Max = -1;

        public override bool Evaluate(EquipmentContext context)
        {
            if (context.Source == null || context.Source.Equipment == null) return false;
            var equipped = context.Source.Equipment.GetEquippedInSlot(Slot);
            int count = 0;
            for (int i = 0; i < equipped.Count; i++)
            {
                if (equipped[i] != null) count++;
            }
            if (count < Min) return false;
            if (Max >= 0 && count > Max) return false;
            return true;
        }
    }
}
