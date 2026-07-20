using System;
using System.Collections.Generic;

namespace Cards
{
    /// <summary>
    /// The cards a character is currently holding and able to play.
    /// </summary>
    public class Hand
    {
        private readonly List<CardInstance> cards = new List<CardInstance>();

        public IReadOnlyList<CardInstance> Cards => cards;
        public int MaxCapacity { get; set; } = 6;
        public bool IsFull => cards.Count >= MaxCapacity;

        public event Action OnHandChanged;

        public void AddCard(CardInstance card)
        {
            cards.Add(card);
            OnHandChanged?.Invoke();
        }

        public bool RemoveCard(CardInstance card)
        {
            bool removed = cards.Remove(card);
            if (removed) OnHandChanged?.Invoke();
            return removed;
        }

        public bool Contains(CardInstance card) => cards.Contains(card);
    }
}
