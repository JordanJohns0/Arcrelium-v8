using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyBrawl.Cards
{
    /// <summary>
    /// A character's personal deck: draw pile + discard pile. When the draw pile empties,
    /// the discard pile is reshuffled back in automatically (standard deckbuilder behavior).
    /// </summary>
    [Serializable]
    public class Deck
    {
        [SerializeField] private List<CardData> startingCards = new List<CardData>();

        private readonly List<CardInstance> drawPile = new List<CardInstance>();
        private readonly List<CardInstance> discardPile = new List<CardInstance>();

        public IReadOnlyList<CardInstance> DrawPile => drawPile;
        public IReadOnlyList<CardInstance> DiscardPile => discardPile;
        public bool HasCards => drawPile.Count > 0 || discardPile.Count > 0;

        /// <summary>
        /// Replaces the Inspector-configured starting cards with a runtime list (e.g. built
        /// from equipped items). Call before Shuffle().
        /// </summary>
        public void SetStartingCards(List<CardData> cards)
        {
            startingCards = cards ?? new List<CardData>();
        }

        public void Shuffle()
        {
            drawPile.Clear();
            discardPile.Clear();

            foreach (CardData data in startingCards)
            {
                drawPile.Add(new CardInstance(data));
            }

            for (int i = drawPile.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (drawPile[i], drawPile[j]) = (drawPile[j], drawPile[i]);
            }
        }

        public CardInstance Draw()
        {
            if (drawPile.Count == 0)
            {
                ReshuffleDiscardIntoDraw();
            }

            if (drawPile.Count == 0) return null; // deck and discard both empty

            CardInstance card = drawPile[0];
            drawPile.RemoveAt(0);
            return card;
        }

        public void Discard(CardInstance card)
        {
            discardPile.Add(card);
        }

        private void ReshuffleDiscardIntoDraw()
        {
            if (discardPile.Count == 0) return;

            drawPile.AddRange(discardPile);
            discardPile.Clear();

            for (int i = drawPile.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (drawPile[i], drawPile[j]) = (drawPile[j], drawPile[i]);
            }
        }
    }
}
