using System.Collections.Generic;
using UnityEngine;

namespace Equipment
{
    public enum DeckRegion { DrawPile, DiscardPile, DrawAndDiscard }

    [CreateAssetMenu(menuName = "Equipment/Trigger Conditions/Deck Size")]
    public class DeckSizeCondition : EquipmentTriggerCondition
    {
        public DeckRegion Region = DeckRegion.DrawPile;
        public int Min = 0;
        [Tooltip("Set to -1 for no upper bound.")]
        public int Max = -1;

        public override bool Evaluate(EquipmentContext context)
        {
            if (context.Source == null || context.Source.Deck == null) return false;
            int count = CountIn(context.Source.Deck);
            if (count < Min) return false;
            if (Max >= 0 && count > Max) return false;
            return true;
        }

        private int CountIn(Cards.Deck deck)
        {
            return Region switch
            {
                DeckRegion.DrawPile => deck.DrawPile.Count,
                DeckRegion.DiscardPile => deck.DiscardPile.Count,
                _ => deck.DrawPile.Count + deck.DiscardPile.Count,
            };
        }
    }
}
