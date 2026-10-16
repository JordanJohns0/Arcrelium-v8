using Characters;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// Makes the wearer take no damage from the selected damage sources. With the default
    /// mask this covers both standing in fire and the explosion that fire detonates on oil.
    /// </summary>
    [CreateAssetMenu(fileName = "DamageImmunityEnchantment", menuName = "Arcrelium/Enchantments/Damage Immunity")]
    public class DamageImmunityEnchantmentSO : EquipmentEnchantmentSO
    {
        [Tooltip("Damage sources the wearer is completely immune to while this is equipped.")]
        public DamageType ImmuneTo = DamageType.Fire | DamageType.Explosion;

        public override bool IsImmuneTo(DamageType type)
        {
            return type != DamageType.None && (ImmuneTo & type) != 0;
        }
    }
}
