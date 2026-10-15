using System;

namespace Equipment
{
    /// <summary>Slots an enchantment may target. Combine with | (Any = all of them).</summary>
    [Flags]
    public enum EnchantTargetSlots
    {
        None = 0,
        Head = 1 << 0,
        Chest = 1 << 1,
        Feet = 1 << 2,
        Hand = 1 << 3,
        Trinket = 1 << 4,
        Any = Head | Chest | Feet | Hand | Trinket
    }

    /// <summary>A yes/no/don't-care restriction on a piece of equipment.</summary>
    public enum TargetRequirement
    {
        Any,
        MustHave,
        MustNotHave
    }

    /// <summary>Whether a piece must be worn right now, or must be in the bag.</summary>
    public enum Placement
    {
        Any,
        MustBeEquipped,
        MustBeInBag
    }
}
