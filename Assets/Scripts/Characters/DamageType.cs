using System;

namespace Characters
{
    /// <summary>
    /// Categories of damage so equipment can be made immune to specific sources. A single
    /// value is passed with each damage instance; enchantments keep a mask of the types they
    /// negate (see <see cref="Equipment.DamageImmunityEnchantmentSO"/>).
    /// </summary>
    [Flags]
    public enum DamageType
    {
        /// <summary>Unspecified damage (e.g. a plain card hit). Never blocked by immunity masks.</summary>
        None = 0,
        Physical = 1 << 0,
        Fire = 1 << 1,
        Explosion = 1 << 2,
    }
}
