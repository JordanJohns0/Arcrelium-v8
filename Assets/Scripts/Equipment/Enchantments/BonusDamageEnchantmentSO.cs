using Cards;
using Characters;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// Adds bonus damage to cards while equipped. By default only applies to the
    /// special cards granted by the same equipment ("your equipment's cards hit harder");
    /// disable OnlyOwnCards to boost every card the wearer plays.
    /// </summary>
    [CreateAssetMenu(fileName = "BonusDamageEnchantment", menuName = "Arcrelium/Enchantments/Bonus Damage")]
    public class BonusDamageEnchantmentSO : EquipmentEnchantmentSO
    {
        [Tooltip("Extra damage added to qualifying card damage.")]
        public int BonusDamage = 1;

        [Tooltip("If true, only cards granted by the enchanted equipment get the bonus. If false, every card the wearer plays gets it.")]
        public bool OnlyOwnCards = true;

        public override int GetDamageBonus(EquipmentInstance equipment, CardData playedCard)
        {
            if (playedCard == null || BonusDamage <= 0) return 0;
            if (!OnlyOwnCards) return BonusDamage;

            var ownCards = equipment?.Data?.Cards;
            if (ownCards == null) return 0;

            foreach (CardData card in ownCards)
            {
                if (card == playedCard) return BonusDamage;
            }
            return 0;
        }
    }
}
