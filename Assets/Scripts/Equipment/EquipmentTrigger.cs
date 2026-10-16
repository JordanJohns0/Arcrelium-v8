using System;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// A single trigger on a piece of equipment: when it fires (timing) and what it does (effect).
    /// Equipment can have multiple triggers for complex interactions.
    /// </summary>
    [Serializable]
    public class EquipmentTrigger
    {
        public TriggerTiming Timing;
        public EquipmentEffect Effect;

        [Tooltip("When true, the trigger only fires while it is the wearer's own turn.")]
        public bool OnlyOnWearersTurn;

        [Tooltip("Optional gate. When set, the trigger only fires while the condition is true.")]
        public EquipmentTriggerCondition Condition;

        [Tooltip("Fire at most once per turn (resets at start of the wearer's turn).")]
        public bool OncePerTurn;

        [Tooltip("Fire at most once per combat (never re-fires after the first success).")]
        public bool OncePerMatch;
    }
}
