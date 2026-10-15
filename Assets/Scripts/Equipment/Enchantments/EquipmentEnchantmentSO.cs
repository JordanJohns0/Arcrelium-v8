using System.Collections.Generic;
using Cards;
using Characters;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// Base class for equipment enchantments: passive modifiers that only work while
    /// their parent equipment stays equipped. Create one asset per enchantment and
    /// reference it from an EquipmentData's Enchantments list.
    ///
    /// Lifecycle hooks fire on equip/unequip (including loadout application at match
    /// start). Query hooks (e.g. GetDamageBonus) are consulted by the game systems
    /// while the enchantment's equipment is worn.
    /// </summary>
    public abstract class EquipmentEnchantmentSO : ScriptableObject
    {
        public string EnchantmentName = "New Enchantment";
        [TextArea] public string Description;

        [Header("Targets")]
        [Tooltip("Slots this enchantment can be applied to. Leave all ticked to allow any slot.")]
        public EnchantTargetSlots AllowedSlots = EnchantTargetSlots.Any;

        [Tooltip("Fewest cards the piece must grant (its own Cards list). 0 = no minimum.")]
        [Min(0)] public int MinCards;

        [Tooltip("Most cards the piece may grant. -1 = no maximum; 0 = only pieces that grant no cards.")]
        [Min(-1)] public int MaxCards = -1;

        [Tooltip("Whether the piece must (or must not) have triggers.")]
        public TargetRequirement Triggers = TargetRequirement.Any;

        [Tooltip("Whether the piece must (or must not) already come with enchantments built in.")]
        public TargetRequirement BuiltInEnchantments = TargetRequirement.Any;

        [Tooltip("Whether the piece must be equipped now, or must be in the bag.")]
        public Placement Placement = Placement.Any;

        [Tooltip("If any are listed, only these exact pieces can receive this enchantment.")]
        public List<EquipmentData> OnlyItems = new List<EquipmentData>();

        [Tooltip("These exact pieces can never receive this enchantment.")]
        public List<EquipmentData> NeverItems = new List<EquipmentData>();

        /// <summary>How an enchantment is shown to the player: "Name: Description" (just the name when there is no description).</summary>
        public string DisplayLine => string.IsNullOrEmpty(Description)
            ? EnchantmentName
            : $"{EnchantmentName}: {Description}";

        /// <summary>
        /// The first restriction <paramref name="item"/> breaks, phrased for the player, or null when
        /// it may receive this enchantment. <paramref name="equipped"/> says whether it is worn now.
        /// </summary>
        public string RestrictionFor(EquipmentData item, bool equipped)
        {
            if (item == null) return "No item.";

            if (NeverItems != null && NeverItems.Contains(item))
                return $"{item.EquipmentName} can't take this enchantment.";
            if (OnlyItems != null && OnlyItems.Count > 0 && !OnlyItems.Contains(item))
                return $"Only specific pieces can take this enchantment.";

            if (!SlotAllowed(item.SlotType))
                return $"Can only go on {SlotList(AllowedSlots)} pieces.";

            int cards = item.Cards != null ? item.Cards.Count : 0;
            if (cards < MinCards)
                return MinCards == 1 ? "Needs a piece that grants at least 1 card." : $"Needs a piece that grants at least {MinCards} cards.";
            if (MaxCards >= 0 && cards > MaxCards)
                return MaxCards == 0 ? "Only works on pieces that grant no cards." : $"Only works on pieces that grant at most {MaxCards} cards.";

            bool hasTriggers = item.Triggers != null && item.Triggers.Count > 0;
            if (Triggers == TargetRequirement.MustHave && !hasTriggers) return "Needs a piece with triggers.";
            if (Triggers == TargetRequirement.MustNotHave && hasTriggers) return "Only works on pieces without triggers.";

            bool hasBuiltIn = item.Enchantments != null && item.Enchantments.Count > 0;
            if (BuiltInEnchantments == TargetRequirement.MustHave && !hasBuiltIn) return "Needs a piece with a built-in enchantment.";
            if (BuiltInEnchantments == TargetRequirement.MustNotHave && hasBuiltIn) return "Only works on pieces without a built-in enchantment.";

            if (Placement == Placement.MustBeEquipped && !equipped) return "Must be equipped to receive this.";
            if (Placement == Placement.MustBeInBag && equipped) return "Must be in the bag to receive this.";

            return null;
        }

        /// <summary>True when <see cref="RestrictionFor"/> finds no problem with the piece.</summary>
        public bool CanApplyTo(EquipmentData item, bool equipped) => RestrictionFor(item, equipped) == null;

        /// <summary>A one-line description of the targets, for the enchant screen. Empty when there are no restrictions.</summary>
        public string TargetSummary()
        {
            var parts = new List<string>();
            if (AllowedSlots != EnchantTargetSlots.Any) parts.Add(SlotList(AllowedSlots));
            if (MinCards > 0) parts.Add(MinCards == 1 ? "grants at least 1 card" : $"grants at least {MinCards} cards");
            if (MaxCards == 0) parts.Add("grants 0 cards");
            else if (MaxCards > 0) parts.Add($"grants at most {MaxCards} cards");
            if (Triggers == TargetRequirement.MustHave) parts.Add("has triggers");
            if (Triggers == TargetRequirement.MustNotHave) parts.Add("no triggers");
            if (BuiltInEnchantments == TargetRequirement.MustHave) parts.Add("has a built-in enchantment");
            if (BuiltInEnchantments == TargetRequirement.MustNotHave) parts.Add("no built-in enchantment");
            if (Placement == Placement.MustBeEquipped) parts.Add("equipped");
            if (Placement == Placement.MustBeInBag) parts.Add("in the bag");
            if (OnlyItems != null && OnlyItems.Count > 0) parts.Add("specific pieces only");
            return parts.Count == 0 ? "" : "Applies to: " + string.Join(", ", parts);
        }

        private bool SlotAllowed(EquipmentSlotType slot)
        {
            EnchantTargetSlots bit = SlotBit(slot);
            return (AllowedSlots & bit) != 0;
        }

        private static EnchantTargetSlots SlotBit(EquipmentSlotType slot)
        {
            switch (slot)
            {
                case EquipmentSlotType.Head: return EnchantTargetSlots.Head;
                case EquipmentSlotType.Chest: return EnchantTargetSlots.Chest;
                case EquipmentSlotType.Feet: return EnchantTargetSlots.Feet;
                case EquipmentSlotType.Hand: return EnchantTargetSlots.Hand;
                case EquipmentSlotType.Trinket: return EnchantTargetSlots.Trinket;
                default: return EnchantTargetSlots.None;
            }
        }

        private static string SlotList(EnchantTargetSlots slots)
        {
            var names = new List<string>();
            foreach (EquipmentSlotType slot in System.Enum.GetValues(typeof(EquipmentSlotType)))
            {
                if ((slots & SlotBit(slot)) != 0) names.Add(slot.ToString());
            }
            return names.Count == 0 ? "no" : string.Join(" / ", names);
        }

        /// <summary>Called when the enchanted equipment is put on.</summary>
        public virtual void OnEquipped(EquipmentInstance equipment, Character wearer) { }

        /// <summary>Called when the enchanted equipment is taken off. Must undo whatever OnEquipped did.</summary>
        public virtual void OnUnequipped(EquipmentInstance equipment, Character wearer) { }

        /// <summary>
        /// Extra damage added to damage dealt by <paramref name="playedCard"/> while the
        /// enchanted equipment is worn. Consulted by ActionResolver before card steps execute.
        /// </summary>
        public virtual int GetDamageBonus(EquipmentInstance equipment, CardData playedCard) => 0;

        /// <summary>
        /// Cards this enchantment adds to the wearer's deck while equipped. Collected by
        /// EquipmentManager.GetAllCards so deck building at match start includes them.
        /// </summary>
        public virtual void CollectGrantedCards(List<CardData> cards) { }
    }
}
