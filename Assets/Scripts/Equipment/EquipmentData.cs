using System.Collections.Generic;
using UnityEngine;
using Cards;

namespace Equipment
{
    /// <summary>
    /// Definition of a piece of equipment. Create as an asset (right-click > Create >  > Equipment).
    /// Equipment may have cards (added to the character's deck when equipped) and/or triggers
    /// (effects that fire at specific moments during gameplay).
    /// </summary>
    [CreateAssetMenu(fileName = "NewEquipment", menuName = "Arcrelium/Equipment")]
    public class EquipmentData : ScriptableObject
    {
        public string EquipmentName = "New Equipment";
        [TextArea] public string Description;
        public Sprite Icon;

        [Tooltip("Which slot this equipment goes into. Hand equipment can be placed in either hand slot.")]
        public EquipmentSlotType SlotType;

        [Tooltip("Cards added to the character's deck when this equipment is equipped. May be empty.")]
        public List<CardData> Cards = new List<CardData>();

        [Tooltip("Triggered effects that fire during gameplay (e.g. start of turn, on damage taken).")]
        public List<EquipmentTrigger> Triggers = new List<EquipmentTrigger>();
    }
}
