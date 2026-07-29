namespace Cards
{
    public class CardInstance
    {
        private static int nextId;

        public int Id { get; }
        public CardData Data { get; }

        public CardInstance(CardData data)
        {
            Id = System.Threading.Interlocked.Increment(ref nextId);
            Data = data;
        }
    }
}
