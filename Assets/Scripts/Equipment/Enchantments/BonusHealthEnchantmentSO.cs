using Characters;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// Grants +X Max Health while equipped; the wearer also gains the same amount of
    /// current health on equip and loses it again on unequip (clamped to the reduced max).
    /// </summary>
    [CreateAssetMenu(fileName = "BonusHealthEnchantment", menuName = "Arcrelium/Enchantments/Bonus Health")]
    public class BonusHealthEnchantmentSO : EquipmentEnchantmentSO
    {
        [Tooltip("Max health added while equipped.")]
        public int BonusMaxHealth = 2;

        public override void OnEquipped(EquipmentInstance equipment, Character wearer)
        {
            if (wearer == null || wearer.Stats == null) return;

            wearer.Stats.MaxHealth += BonusMaxHealth;
            wearer.Stats.CurrentHealth += Mathf.Max(0, BonusMaxHealth);
            wearer.Stats.NotifyChanged();
        }

        public override void OnUnequipped(EquipmentInstance equipment, Character wearer)
        {
            if (wearer == null || wearer.Stats == null) return;

            wearer.Stats.MaxHealth -= BonusMaxHealth;
            wearer.Stats.CurrentHealth = Mathf.Clamp(wearer.Stats.CurrentHealth, 0, wearer.Stats.MaxHealth);
            wearer.Stats.NotifyChanged();
        }
    }
}
