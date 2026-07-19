using UnityEngine;

namespace FantasyBrawl.Characters
{
    /// <summary>
    /// Base stat block for a character. Kept as plain data so cards/effects can read
    /// and modify it without caring about MonoBehaviour lifecycle.
    /// </summary>
    [System.Serializable]
    public class CharacterStats
    {
        public string CharacterName = "Character";
        public int MaxHealth = 10;
        public int CurrentHealth;
        public int BaseMovement = 2;
        public int BaseAttack = 2;
        public int Armor = 0;

        public void Initialize()
        {
            CurrentHealth = MaxHealth;
        }

        public bool IsAlive => CurrentHealth > 0;

        public void ApplyDamage(int amount)
        {
            int mitigated = Mathf.Max(0, amount - Armor);
            CurrentHealth = Mathf.Max(0, CurrentHealth - mitigated);
        }

        public void Heal(int amount)
        {
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        }
    }
}
