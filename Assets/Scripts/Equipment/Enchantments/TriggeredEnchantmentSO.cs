using System.Collections.Generic;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// An enchantment whose entire behaviour is one or more equipment triggers. Everything is
    /// configured in the inspector (timing, effect, condition, once-per-turn/match), so new
    /// trigger enchantments need no extra code — just a new asset.
    /// </summary>
    [CreateAssetMenu(fileName = "TriggeredEnchantment", menuName = "Arcrelium/Enchantments/Triggered")]
    public class TriggeredEnchantmentSO : EquipmentEnchantmentSO
    {
        [Tooltip("Triggers that fire while this enchantment is worn. Each may carry its own condition.")]
        public List<EquipmentTrigger> TriggerList = new List<EquipmentTrigger>();

        public override void CollectTriggers(List<EquipmentTrigger> collected)
        {
            if (TriggerList == null) return;
            for (int i = 0; i < TriggerList.Count; i++)
            {
                if (TriggerList[i] != null) collected.Add(TriggerList[i]);
            }
        }
    }
}
