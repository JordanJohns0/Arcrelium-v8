using Cards;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// Multiplies the damage of every card played by the wearer. By default only the
    /// enchanted equipment's own cards are doubled; uncheck OwnCardsOnly to double all cards.
    /// </summary>
    [CreateAssetMenu(fileName = "DamageMultiplierEnchantment", menuName = "Arcrelium/Enchantments/Damage Multiplier")]
    public class DamageMultiplierEnchantmentSO : EquipmentEnchantmentSO
    {
        [Tooltip("Damage multiplier. 2 = double damage.")]
        public float Multiplier = 2f;

        [Tooltip("When true, only cards belonging to the enchanted equipment are multiplied.")]
        public bool OwnCardsOnly = true;

        public override float GetDamageMultiplier(EquipmentInstance equipment, CardData playedCard)
        {
            if (playedCard == null) return 1f;
            if (OwnCardsOnly && !BelongsTo(equipment, playedCard)) return 1f;
            return Mathf.Max(0f, Multiplier);
        }

        private static bool BelongsTo(EquipmentInstance equipment, CardData card)
        {
            if (equipment?.Data?.Cards == null) return false;
            for (int i = 0; i < equipment.Data.Cards.Count; i++)
            {
                if (equipment.Data.Cards[i] == card) return true;
            }
            return false;
        }
    }
}
