using System.Collections.Generic;

namespace Equipment
{
    /// <summary>
    /// A unique runtime copy of an EquipmentData template. Two EquipmentInstances wrapping
    /// the same EquipmentData are still distinct objects.
    ///
    /// Besides the template's own <see cref="EquipmentData.Enchantments"/>, an instance can
    /// carry run-time "extra" enchantments granted during a run (e.g. by the enchantment
    /// station). Those are kept on the instance so the shared EquipmentData asset is never
    /// modified.
    /// </summary>
    public class EquipmentInstance
    {
        public EquipmentData Data { get; }

        private readonly List<EquipmentEnchantmentSO> extraEnchantments = new List<EquipmentEnchantmentSO>();

        public EquipmentInstance(EquipmentData data)
        {
            Data = data;
        }

        /// <summary>Enchantments granted to this instance at run time (not on the template).</summary>
        public IReadOnlyList<EquipmentEnchantmentSO> ExtraEnchantments => extraEnchantments;

        /// <summary>All enchantments that apply to this instance: template + run-time extras.</summary>
        public IEnumerable<EquipmentEnchantmentSO> AllEnchantments
        {
            get
            {
                if (Data != null && Data.Enchantments != null)
                {
                    foreach (var enchantment in Data.Enchantments)
                        if (enchantment != null) yield return enchantment;
                }

                foreach (var enchantment in extraEnchantments)
                {
                    if (enchantment != null) yield return enchantment;
                }
            }
        }

        /// <summary>Adds run-time enchantments. Null entries are ignored.</summary>
        public void AddEnchantments(IEnumerable<EquipmentEnchantmentSO> enchantments)
        {
            if (enchantments == null) return;
            foreach (var enchantment in enchantments)
            {
                if (enchantment != null)
                    extraEnchantments.Add(enchantment);
            }
        }
    }
}
