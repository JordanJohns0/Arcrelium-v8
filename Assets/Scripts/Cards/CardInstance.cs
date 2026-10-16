using UnityEngine;

namespace Cards
{
    public class CardInstance
    {
        private static int nextId;

        public int Id { get; }
        public CardData Data { get; }

        /// <summary>
        /// The cost of THIS copy of the card, independent of the shared CardData asset.
        /// Starts at the card's base cost; effects (e.g. "set its cost to 1" or "decrease
        /// the cost of cards in your hand") can change it per instance.
        /// </summary>
        public int Cost { get; set; }

        /// <summary>
        /// A lasting cost change baked into this copy at creation (e.g. "this equipment's
        /// cards cost 1 less"). Unlike <see cref="Cost"/>, it survives ResetCost, so a card
        /// keeps the discount every time it returns to a hand.
        /// </summary>
        public int CostOffset { get; private set; }

        /// <summary>The card's unmodified cost plus any baked-in <see cref="CostOffset"/>, floored at 0.</summary>
        public int BaseCost => Mathf.Max(0, (Data != null ? Data.Cost : 0) + CostOffset);

        /// <summary>True while this copy's cost differs from its base cost.</summary>
        public bool IsCostModified => Cost != BaseCost;

        public CardInstance(CardData data)
        {
            Id = System.Threading.Interlocked.Increment(ref nextId);
            Data = data;
            Cost = BaseCost;
        }

        /// <summary>Bakes a lasting cost change into this copy (e.g. an equipment discount) and resets Cost to match.</summary>
        public void SetCostOffset(int offset)
        {
            CostOffset = offset;
            Cost = BaseCost;
        }

        /// <summary>Drops any per-instance cost change, restoring the card's base cost.</summary>
        public void ResetCost()
        {
            Cost = BaseCost;
        }
    }
}
