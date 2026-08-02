using System.Collections.Generic;
using UnityEngine;
using Cards;

namespace Equipment
{
    /// <summary>
    /// A fixed-slot snapshot of a player's equipped items. Applied to a character at match
    /// start to build its deck, and serialized as equipment names (fixed slot order) for
    /// JSON persistence and Steam transfer. Each player's loadout stays private — only the
    /// equipped names leave the machine, and only to the host.
    /// </summary>
    [System.Serializable]
    public class EquipmentLoadout
    {
        public const int SlotCount = 8;

        public EquipmentData Head;
        public EquipmentData Chest;
        public EquipmentData Feet;
        public EquipmentData LeftHand;
        public EquipmentData RightHand;
        public EquipmentData Trinket1;
        public EquipmentData Trinket2;
        public EquipmentData Trinket3;

        public EquipmentData Get(EquipmentSlotType slot, int index = 0)
        {
            switch (slot)
            {
                case EquipmentSlotType.Head: return Head;
                case EquipmentSlotType.Chest: return Chest;
                case EquipmentSlotType.Feet: return Feet;
                case EquipmentSlotType.Hand: return index == 0 ? LeftHand : RightHand;
                case EquipmentSlotType.Trinket:
                    if (index == 0) return Trinket1;
                    if (index == 1) return Trinket2;
                    return Trinket3;
                default: return null;
            }
        }

        public void Set(EquipmentSlotType slot, int index, EquipmentData data)
        {
            switch (slot)
            {
                case EquipmentSlotType.Head: Head = data; break;
                case EquipmentSlotType.Chest: Chest = data; break;
                case EquipmentSlotType.Feet: Feet = data; break;
                case EquipmentSlotType.Hand:
                    if (index == 0) LeftHand = data;
                    else RightHand = data;
                    break;
                case EquipmentSlotType.Trinket:
                    if (index == 0) Trinket1 = data;
                    else if (index == 1) Trinket2 = data;
                    else Trinket3 = data;
                    break;
            }
        }

        public List<EquipmentData> GetAllItems()
        {
            var items = new List<EquipmentData>
            {
                Head, Chest, Feet, LeftHand, RightHand, Trinket1, Trinket2, Trinket3
            };
            return items;
        }

        public List<CardData> GetAllCards()
        {
            var cards = new List<CardData>();
            AddCards(Head, cards);
            AddCards(Chest, cards);
            AddCards(Feet, cards);
            AddCards(LeftHand, cards);
            AddCards(RightHand, cards);
            AddCards(Trinket1, cards);
            AddCards(Trinket2, cards);
            AddCards(Trinket3, cards);
            return cards;
        }

        private static void AddCards(EquipmentData data, List<CardData> cards)
        {
            if (data == null || data.Cards == null) return;
            cards.AddRange(data.Cards);
        }

        /// <summary>
        /// Exports equipped item names in fixed slot order:
        /// head, chest, feet, left hand, right hand, trinket 1, trinket 2, trinket 3.
        /// Empty slots export as "".
        /// </summary>
        public string[] ToNames()
        {
            return new[]
            {
                NameOrEmpty(Head),
                NameOrEmpty(Chest),
                NameOrEmpty(Feet),
                NameOrEmpty(LeftHand),
                NameOrEmpty(RightHand),
                NameOrEmpty(Trinket1),
                NameOrEmpty(Trinket2),
                NameOrEmpty(Trinket3)
            };
        }

        private static string NameOrEmpty(EquipmentData data)
        {
            return data != null ? data.EquipmentName : "";
        }

        /// <summary>
        /// Resolves a fixed-slot name array back into a loadout. Names that cannot be
        /// resolved (unknown or library missing) become empty slots.
        /// </summary>
        public static EquipmentLoadout FromNames(string[] names, GameDataLibrary library)
        {
            var loadout = new EquipmentLoadout();
            if (names == null || library == null) return loadout;

            loadout.Head = library.GetEquipmentByName(names.Length > 0 ? names[0] : null);
            loadout.Chest = library.GetEquipmentByName(names.Length > 1 ? names[1] : null);
            loadout.Feet = library.GetEquipmentByName(names.Length > 2 ? names[2] : null);
            loadout.LeftHand = library.GetEquipmentByName(names.Length > 3 ? names[3] : null);
            loadout.RightHand = library.GetEquipmentByName(names.Length > 4 ? names[4] : null);
            loadout.Trinket1 = library.GetEquipmentByName(names.Length > 5 ? names[5] : null);
            loadout.Trinket2 = library.GetEquipmentByName(names.Length > 6 ? names[6] : null);
            loadout.Trinket3 = library.GetEquipmentByName(names.Length > 7 ? names[7] : null);
            return loadout;
        }

        /// <summary>Copies every slot from another loadout (used to hydrate a serialized field).</summary>
        public void Apply(EquipmentLoadout other)
        {
            if (other == null) return;
            Head = other.Head;
            Chest = other.Chest;
            Feet = other.Feet;
            LeftHand = other.LeftHand;
            RightHand = other.RightHand;
            Trinket1 = other.Trinket1;
            Trinket2 = other.Trinket2;
            Trinket3 = other.Trinket3;
        }

        public void Clear()
        {
            Head = null;
            Chest = null;
            Feet = null;
            LeftHand = null;
            RightHand = null;
            Trinket1 = null;
            Trinket2 = null;
            Trinket3 = null;
        }

        public bool IsEmpty =>
            Head == null && Chest == null && Feet == null &&
            LeftHand == null && RightHand == null &&
            Trinket1 == null && Trinket2 == null && Trinket3 == null;
    }
}
