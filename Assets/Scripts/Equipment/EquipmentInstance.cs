namespace FantasyBrawl.Equipment
{
    /// <summary>
    /// A unique runtime copy of an EquipmentData template. Two EquipmentInstances wrapping
    /// the same EquipmentData are still distinct objects.
    /// </summary>
    public class EquipmentInstance
    {
        public EquipmentData Data { get; }

        public EquipmentInstance(EquipmentData data)
        {
            Data = data;
        }
    }
}
