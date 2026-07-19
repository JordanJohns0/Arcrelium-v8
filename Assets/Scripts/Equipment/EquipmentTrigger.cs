using System;
using UnityEngine;

namespace FantasyBrawl.Equipment
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
    }
}
