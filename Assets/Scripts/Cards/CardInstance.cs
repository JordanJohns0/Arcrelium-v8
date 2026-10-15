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

        /// <summary>The card's unmodified cost, straight from the shared CardData asset.</summary>
        public int BaseCost => Data != null ? Data.Cost : 0;

        /// <summary>True while this copy's cost differs from its base cost.</summary>
        public bool IsCostModified => Cost != BaseCost;

        public CardInstance(CardData data)
        {
            Id = System.Threading.Interlocked.Increment(ref nextId);
            Data = data;
            Cost = BaseCost;
        }

        /// <summary>Drops any per-instance cost change, restoring the card's base cost.</summary>
        public void ResetCost()
        {
            Cost = BaseCost;
        }
    }
}
