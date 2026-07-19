using UnityEngine;

namespace FantasyBrawl.Equipment
{
    /// <summary>
    /// Base class for equipment trigger effects as ScriptableObject assets, so designers
    /// can create e.g. "HealEffect.asset", "DrawCardsEffect.asset" and drop them onto
    /// EquipmentData without touching code. Subclass this for each behavior.
    /// </summary>
    public abstract class EquipmentEffect : ScriptableObject
    {
        [TextArea] public string Description;

        public abstract void Execute(EquipmentContext context);
    }
}
