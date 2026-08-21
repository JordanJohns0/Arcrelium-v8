using System.Collections.Generic;
using Cards;
using Characters;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// Base class for equipment enchantments: passive modifiers that only work while
    /// their parent equipment stays equipped. Create one asset per enchantment and
    /// reference it from an EquipmentData's Enchantments list.
    ///
    /// Lifecycle hooks fire on equip/unequip (including loadout application at match
    /// start). Query hooks (e.g. GetDamageBonus) are consulted by the game systems
    /// while the enchantment's equipment is worn.
    /// </summary>
    public abstract class EquipmentEnchantmentSO : ScriptableObject
    {
        public string EnchantmentName = "New Enchantment";
        [TextArea] public string Description;

        /// <summary>Called when the enchanted equipment is put on.</summary>
        public virtual void OnEquipped(EquipmentInstance equipment, Character wearer) { }

        /// <summary>Called when the enchanted equipment is taken off. Must undo whatever OnEquipped did.</summary>
        public virtual void OnUnequipped(EquipmentInstance equipment, Character wearer) { }

        /// <summary>
        /// Extra damage added to damage dealt by <paramref name="playedCard"/> while the
        /// enchanted equipment is worn. Consulted by ActionResolver before card steps execute.
        /// </summary>
        public virtual int GetDamageBonus(EquipmentInstance equipment, CardData playedCard) => 0;

        /// <summary>
        /// Cards this enchantment adds to the wearer's deck while equipped. Collected by
        /// EquipmentManager.GetAllCards so deck building at match start includes them.
        /// </summary>
        public virtual void CollectGrantedCards(List<CardData> cards) { }
    }
}
