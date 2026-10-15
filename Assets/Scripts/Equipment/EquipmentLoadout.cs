using System.Collections.Generic;
using UnityEngine;
using Cards;

namespace Equipment
{
    /// <summary>
    /// Enchantments granted to a single piece of equipment during a run, keyed by the
    /// equipment template. Held on the player's <see cref="EquipmentLoadout"/> so it travels
    /// with the loadout over the network and through match setup without ever mutating the
    /// shared <see cref="EquipmentData"/> asset.
    /// </summary>
    [System.Serializable]
    public class AppliedEnchantment
    {
        public EquipmentData equipment;
        public List<EquipmentEnchantmentSO> enchantments = new List<EquipmentEnchantmentSO>();
    }

    /// <summary>
    /// Name-serialized form of an <see cref="AppliedEnchantment"/> for saves and network
    /// transfer (Unity's JsonUtility cannot serialize the ScriptableObject references).
    /// </summary>
    [System.Serializable]
    public class EnchantmentAssignment
    {
        public string equipmentName;
        public List<string> enchantmentNames = new List<string>();
    }

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

        /// <summary>
        /// Enchantments applied to this player's equipment for the current run, keyed by
        /// equipment (both equipped items and ones still in the inventory). A piece may hold
        /// several enchantments. Not slot-bound so it survives equipping/unequipping.
        /// </summary>
        public List<AppliedEnchantment> enchantments = new List<AppliedEnchantment>();

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

        /// <summary>True if the item is equipped in any slot of this loadout.</summary>
        public bool ContainsItem(EquipmentData item)
        {
            foreach (EquipmentData equipped in GetAllItems())
            {
                if (equipped == item) return true;
            }
            return false;
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

        private void AddCards(EquipmentData data, List<CardData> cards)
        {
            if (data == null) return;
            if (data.Cards != null)
                cards.AddRange(data.Cards);

            foreach (var enchantment in GetEnchantments(data))
                enchantment?.CollectGrantedCards(cards);
        }

        /// <summary>Enchantments applied to the given equipment this run (never null).</summary>
        public IReadOnlyList<EquipmentEnchantmentSO> GetEnchantments(EquipmentData item)
        {
            if (item == null) return System.Array.Empty<EquipmentEnchantmentSO>();
            foreach (var entry in enchantments)
            {
                if (entry != null && entry.equipment == item)
                    return entry.enchantments;
            }
            return System.Array.Empty<EquipmentEnchantmentSO>();
        }

        /// <summary>True when the given equipment already carries this exact enchantment.</summary>
        public bool HasEnchantment(EquipmentData item, EquipmentEnchantmentSO enchantment)
        {
            if (item == null || enchantment == null) return false;
            foreach (var entry in enchantments)
            {
                if (entry != null && entry.equipment == item && entry.enchantments.Contains(enchantment))
                    return true;
            }
            return false;
        }

        /// <summary>Applies an enchantment to a piece of equipment (stacking is allowed).</summary>
        public void AddEnchantment(EquipmentData item, EquipmentEnchantmentSO enchantment)
        {
            if (item == null || enchantment == null) return;

            AppliedEnchantment entry = null;
            foreach (var candidate in enchantments)
            {
                if (candidate != null && candidate.equipment == item) { entry = candidate; break; }
            }

            if (entry == null)
            {
                entry = new AppliedEnchantment { equipment = item };
                enchantments.Add(entry);
            }
            entry.enchantments.Add(enchantment);
        }

        /// <summary>Name-serialized enchantment map for saves and network transfer.</summary>
        public List<EnchantmentAssignment> ToEnchantmentAssignments()
        {
            var result = new List<EnchantmentAssignment>();
            foreach (var entry in enchantments)
            {
                if (entry == null || entry.equipment == null) continue;

                var assignment = new EnchantmentAssignment { equipmentName = entry.equipment.EquipmentName };
                foreach (var enchantment in entry.enchantments)
                {
                    if (enchantment != null)
                        assignment.enchantmentNames.Add(enchantment.EnchantmentName);
                }
                if (assignment.enchantmentNames.Count > 0)
                    result.Add(assignment);
            }
            return result;
        }

        /// <summary>Rehydrates the enchantment map from names (unknown names are dropped).</summary>
        public void ApplyEnchantmentAssignments(List<EnchantmentAssignment> assignments, GameDataLibrary library)
        {
            enchantments.Clear();
            if (assignments == null || library == null) return;

            foreach (var assignment in assignments)
            {
                if (assignment == null) continue;
                var equipment = library.GetEquipmentByName(assignment.equipmentName);
                if (equipment == null || assignment.enchantmentNames == null) continue;

                foreach (var name in assignment.enchantmentNames)
                {
                    var enchantment = library.GetEnchantmentByName(name);
                    if (enchantment != null)
                        AddEnchantment(equipment, enchantment);
                }
            }
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

            enchantments.Clear();
            foreach (var entry in other.enchantments)
            {
                if (entry == null) continue;
                enchantments.Add(new AppliedEnchantment
                {
                    equipment = entry.equipment,
                    enchantments = new List<EquipmentEnchantmentSO>(entry.enchantments)
                });
            }
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
            enchantments.Clear();
        }

        public bool IsEmpty =>
            Head == null && Chest == null && Feet == null &&
            LeftHand == null && RightHand == null &&
            Trinket1 == null && Trinket2 == null && Trinket3 == null;
    }
}
