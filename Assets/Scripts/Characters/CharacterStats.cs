using UnityEngine;

namespace Characters
{
    /// <summary>
    /// Base stat block for a character. Kept as plain data so cards/effects can read
    /// and modify it without caring about MonoBehaviour lifecycle.
    ///
    /// Damage flow: Block absorbs first, then Armor mitigates, then Health takes the rest.
    /// </summary>
    [System.Serializable]
    public class CharacterStats
    {
        public string CharacterName = "Character";
        public int MaxHealth = 10;
        public int CurrentHealth;
        public int Armor = 0;
        public int Block = 0;

        public void Initialize()
        {
            CurrentHealth = MaxHealth;
            Block = 0;
        }

        public bool IsAlive => CurrentHealth > 0;

        /// <summary>
        /// Deals damage: block absorbs first, then armor mitigates, then health takes the rest.
        /// </summary>
        public void ApplyDamage(int amount)
        {
            int remaining = amount;

            // Block absorbs first
            if (Block > 0)
            {
                int absorbed = Mathf.Min(Block, remaining);
                Block -= absorbed;
                remaining -= absorbed;
            }

            // Armor mitigates the rest
            if (remaining > 0 && Armor > 0)
            {
                remaining = Mathf.Max(0, remaining - Armor);
            }

            // Health takes whatever is left
            if (remaining > 0)
            {
                CurrentHealth = Mathf.Max(0, CurrentHealth - remaining);
            }
        }

        public void Heal(int amount)
        {
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        }

        public void GainBlock(int amount)
        {
            Block += amount;
        }
    }
}
