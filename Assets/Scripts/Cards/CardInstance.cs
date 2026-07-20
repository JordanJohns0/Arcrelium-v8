namespace Cards
{
    /// <summary>
    /// A unique runtime copy of a CardData template. Two CardInstances wrapping the same
    /// CardData are still distinct objects, so hands and decks can hold multiple copies
    /// of the same card without aliasing issues.
    /// </summary>
    public class CardInstance
    {
        public CardData Data { get; }

        public CardInstance(CardData data)
        {
            Data = data;
        }
    }
}
