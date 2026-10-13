using System;
using UnityEngine;

namespace Characters
{
    /// <summary>
    /// Base stat block for a character. Kept as plain data so cards/effects can read
    /// and modify it without caring about MonoBehaviour lifecycle.
    ///
    /// Damage flow: Block absorbs first, then Health takes the rest.
    /// </summary>
    [System.Serializable]
    public class CharacterStats
    {
        public string CharacterName = "Character";
        public int MaxHealth = 10;
        public int CurrentHealth;
        public int Block = 0;

        public event Action<CharacterStats> OnStatsChanged;

        public void NotifyChanged()
        {
            OnStatsChanged?.Invoke(this);
        }

        public void Initialize()
        {
            CurrentHealth = MaxHealth;
            Block = 0;
            OnStatsChanged?.Invoke(this);
        }

        public bool IsAlive => CurrentHealth > 0;

        /// <summary>
        /// Deals damage: block absorbs first, then health takes the rest.
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

            // Health takes whatever is left
            if (remaining > 0)
            {
                CurrentHealth = Mathf.Max(0, CurrentHealth - remaining);
            }

            OnStatsChanged?.Invoke(this);
        }

        public void Heal(int amount)
        {
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            OnStatsChanged?.Invoke(this);
        }

        /// <summary>
        /// Instant defeat regardless of block (execution-style effects). Clears block and
        /// zeroes health so the character dies.
        /// </summary>
        public void Kill()
        {
            Block = 0;
            CurrentHealth = 0;
            OnStatsChanged?.Invoke(this);
        }

        public void GainBlock(int amount)
        {
            Block = Mathf.Min(MaxHealth, Block + amount);
            OnStatsChanged?.Invoke(this);
        }
    }
}
