using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// An optional gate on a trigger. When an equipment trigger (or an enchanted trigger)
    /// carries one, it only fires while Evaluate returns true for the current context — so
    /// "while your deck is empty" or "while 3 trinkets are equipped" are data, not code.
    /// </summary>
    public abstract class EquipmentTriggerCondition : ScriptableObject
    {
        public abstract bool Evaluate(EquipmentContext context);
    }
}
