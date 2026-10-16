using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cards
{
    /// <summary>
    /// A character's personal deck: draw pile + discard pile. When the draw pile empties,
    /// the discard pile is reshuffled back in automatically (standard deckbuilder behavior).
    /// The deck is built at runtime from equipment — there are no Inspector-configured cards.
    /// </summary>
    [Serializable]
    public class Deck
    {
        private List<CardData> startingCards = new List<CardData>();

        private readonly List<CardInstance> drawPile = new List<CardInstance>();
        private readonly List<CardInstance> discardPile = new List<CardInstance>();
        private bool shuffled;

        public IReadOnlyList<CardInstance> DrawPile => drawPile;
        public IReadOnlyList<CardInstance> DiscardPile => discardPile;

        /// <summary>
        /// Optional cost adjustment applied to every card this deck creates (e.g. an equipment
        /// enchantment that makes its own cards cost 1 less). Returns a delta added to the
        /// card's base cost. Set by the owning Character when it is initialized.
        /// </summary>
        public System.Func<CardData, int> CostAdjuster;

        /// <summary>
        /// Fired when the discard pile is shuffled back into the draw pile mid-match (the
        /// "you shuffled your deck" moment). Not fired for the initial match-start shuffle.
        /// </summary>
        public event Action OnShuffled;

        /// <summary>
        /// Sets the cards that will populate the draw pile on Shuffle.
        /// Called by Character.Initialize from equipment cards.
        /// </summary>
        public void SetStartingCards(List<CardData> cards)
        {
            startingCards = cards ?? new List<CardData>();
            shuffled = false;
        }

        public void Shuffle()
        {
            drawPile.Clear();
            discardPile.Clear();

            foreach (CardData data in startingCards)
            {
                drawPile.Add(CreateInstance(data));
            }

            for (int i = drawPile.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (drawPile[i], drawPile[j]) = (drawPile[j], drawPile[i]);
            }

            shuffled = true;
        }

        /// <summary>
        /// Adds a card to the deck (e.g. granted by an equipment enchantment). Before the
        /// first Shuffle it only joins the starting list; afterwards a live copy is also
        /// inserted at a random position in the draw pile so it can be drawn this match.
        /// </summary>
        public void AddCard(CardData data)
        {
            if (data == null) return;

            startingCards.Add(data);

            if (!shuffled) return;

            var instance = CreateInstance(data);
            drawPile.Insert(UnityEngine.Random.Range(0, drawPile.Count + 1), instance);
        }

        /// <summary>
        /// Removes one copy of a card from the deck (e.g. revoked on unequip). Removes it
        /// from the starting list and one live copy from the draw or discard pile; copies
        /// already drawn into a hand stay playable until used.
        /// </summary>
        public bool RemoveCard(CardData data)
        {
            if (data == null) return false;

            bool removedFromPiles = false;

            for (int i = drawPile.Count - 1; i >= 0; i--)
            {
                if (drawPile[i].Data == data)
                {
                    drawPile.RemoveAt(i);
                    removedFromPiles = true;
                    break;
                }
            }

            if (!removedFromPiles)
            {
                for (int i = discardPile.Count - 1; i >= 0; i--)
                {
                    if (discardPile[i].Data == data)
                    {
                        discardPile.RemoveAt(i);
                        removedFromPiles = true;
                        break;
                    }
                }
            }

            return startingCards.Remove(data) || removedFromPiles;
        }

        public CardInstance Draw()
        {
            if (drawPile.Count == 0)
            {
                ReshuffleDiscardIntoDraw();
            }

            if (drawPile.Count == 0) return null;

            CardInstance card = drawPile[0];
            drawPile.RemoveAt(0);
            return card;
        }

        /// <summary>
        /// Removes and returns the top card of the draw pile WITHOUT reshuffling the discard
        /// pile back in (steal effects: the victim's deck loses the card, so they can never
        /// get it back). Null when the draw pile is empty.
        /// </summary>
        public CardInstance TakeTopCard()
        {
            if (drawPile.Count == 0) return null;

            CardInstance card = drawPile[0];
            drawPile.RemoveAt(0);
            return card;
        }

        /// <summary>
        /// Shuffles a specific card into the draw pile. If the deck hasn't been shuffled yet,
        /// the card is added to the starting cards instead (it will appear on the next Shuffle).
        /// </summary>
        public void ShuffleIn(CardData data)
        {
            if (data == null) return;
            startingCards.Add(data);
            if (!shuffled) return;
            var instance = CreateInstance(data);
            int index = UnityEngine.Random.Range(0, drawPile.Count + 1);
            drawPile.Insert(index, instance);
        }

        public void Discard(CardInstance card)
        {
            discardPile.Add(card);
        }

        /// <summary>
        /// Looks up a starting card by name so clients can rebuild synced hands.
        /// </summary>
        public CardData GetCardDataByName(string cardName)
        {
            foreach (CardData data in startingCards)
            {
                if (data != null && data.CardName == cardName) return data;
            }
            return null;
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

            OnShuffled?.Invoke();
        }

        private CardInstance CreateInstance(CardData data)
        {
            var instance = new CardInstance(data);
            if (CostAdjuster != null && data != null)
            {
                int delta = CostAdjuster(data);
                if (delta != 0) instance.SetCostOffset(delta);
            }
            return instance;
        }
    }
}
