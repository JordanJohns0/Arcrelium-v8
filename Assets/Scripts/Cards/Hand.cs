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
        public int MaxCapacity { get; set; } = 10;
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

        /// <summary>
        /// Replaces the whole hand with <paramref name="newCards"/> in a single change (one
        /// OnHandChanged), so bound UI sees one update rather than one per card.
        /// </summary>
        public void SetCards(IEnumerable<CardInstance> newCards)
        {
            cards.Clear();
            if (newCards != null) cards.AddRange(newCards);
            OnHandChanged?.Invoke();
        }

        /// <summary>
        /// Fires OnHandChanged without changing the card list. Used when a property of the
        /// cards changed in place (e.g. a cost effect) so bound UI refreshes.
        /// </summary>
        public void NotifyChanged() => OnHandChanged?.Invoke();
    }
}
