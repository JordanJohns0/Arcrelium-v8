using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// Creates and persists a private PlayerInventory per Steam account. Unlocks and the
    /// equipped loadout survive across sessions. Each machine only ever touches its own
    /// inventory — the host never sees another player's items. When a player has no save
    /// yet, the starter inventory (Assets/Resources/PlayerInventory.asset) is used as a
    /// template so new accounts begin with the default gear.
    /// </summary>
    public static class PlayerInventoryProvider
    {
        private const string FilePrefix = "arcrelium_inventory_";
        private static readonly Dictionary<ulong, PlayerInventory> cache = new Dictionary<ulong, PlayerInventory>();

        /// <summary>Storage root, overrideable by tests. Null = Application.persistentDataPath.</summary>
        internal static string RootOverride;

        /// <summary>Starter template, overrideable by tests. Null = Resources "PlayerInventory".</summary>
        internal static PlayerInventory DefaultOverride;

        public static ulong LocalPlayerKey =>
            Steamworks.SteamClient.IsValid ? Steamworks.SteamClient.SteamId.Value : 0UL;

        public static PlayerInventory GetLocal()
        {
            return GetFor(LocalPlayerKey);
        }

        public static PlayerInventory GetFor(ulong steamId)
        {
            if (cache.TryGetValue(steamId, out var inv)) return inv;

            var instance = ScriptableObject.CreateInstance<PlayerInventory>();
            bool fromSave = LoadInto(instance, steamId);
            if (!fromSave || IsEmpty(instance))
                ApplyTemplate(instance);
            EnsureLoadoutOwned(instance);
            cache[steamId] = instance;
            Debug.Log($"[PlayerInventoryProvider] GetFor({steamId}) fromSave={fromSave} defaultsLoaded={DefaultInventory() != null} unlocked={instance.UnlockedEquipment.Count} loadout={string.Join(",", instance.Loadout.ToNames())}");
            return instance;
        }

        /// <summary>The starter inventory template used for brand-new accounts.</summary>
        public static PlayerInventory DefaultInventory()
        {
            return DefaultOverride != null ? DefaultOverride : Resources.Load<PlayerInventory>("PlayerInventory");
        }

        /// <summary>True when an inventory has no progress at all — treated as a new account.</summary>
        private static bool IsEmpty(PlayerInventory inventory)
        {
            if (inventory == null) return true;
            bool noUnlocks = inventory.UnlockedEquipment == null || inventory.UnlockedEquipment.Count == 0;
            bool emptyLoadout = inventory.Loadout == null || inventory.Loadout.IsEmpty;
            return noUnlocks && emptyLoadout;
        }

        /// <summary>Clones the starter template (unlocks + loadout) into a fresh inventory.</summary>
        private static void ApplyTemplate(PlayerInventory inventory)
        {
            var defaults = DefaultInventory();
            if (defaults == null) return;
            inventory.SetUnlocked(new List<EquipmentData>(defaults.UnlockedEquipment));
            if (inventory.Loadout != null && defaults.Loadout != null)
                inventory.Loadout.Apply(defaults.Loadout);
            inventory.SetGold(defaults.Gold);
        }

        /// <summary>Guarantees equipped items count as owned, so a template loadout works even
        /// if its items were never added to the unlocked list.</summary>
        private static void EnsureLoadoutOwned(PlayerInventory inventory)
        {
            if (inventory == null || inventory.Loadout == null) return;
            foreach (var item in inventory.Loadout.GetAllItems())
            {
                if (item != null)
                    inventory.Unlock(item);
            }
        }

        public static void Save(ulong steamId, PlayerInventory inventory = null)
        {
            inventory ??= GetFor(steamId);

            var data = new InventorySaveData();
            if (inventory.UnlockedEquipment != null)
            {
                foreach (var e in inventory.UnlockedEquipment)
                    data.unlockedNames.Add(e != null ? e.EquipmentName : "");
            }
            data.loadoutNames = inventory.Loadout != null
                ? inventory.Loadout.ToNames()
                : new string[EquipmentLoadout.SlotCount];
            data.enchantments = inventory.Loadout != null
                ? inventory.Loadout.ToEnchantmentAssignments()
                : new List<EnchantmentAssignment>();
            data.gold = inventory.Gold;

            string path = GetPath(steamId);
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, JsonUtility.ToJson(data));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PlayerInventoryProvider] Failed to save inventory: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads a save into the given inventory. Returns true when a save file existed
        /// (regardless of whether it parsed fully).
        /// </summary>
        public static bool LoadInto(PlayerInventory inventory, ulong steamId)
        {
            string path = GetPath(steamId);
            if (!File.Exists(path)) return false;
            try
            {
                var data = JsonUtility.FromJson<InventorySaveData>(File.ReadAllText(path));
                var lib = GameDataLibrary.Instance;

                var unlocked = new List<EquipmentData>();
                if (data != null)
                {
                    if (data.unlockedNames != null)
                    {
                        foreach (var name in data.unlockedNames)
                        {
                            var e = lib != null ? lib.GetEquipmentByName(name) : null;
                            if (e != null && !unlocked.Contains(e)) unlocked.Add(e);
                        }
                    }
                    inventory.SetUnlocked(unlocked);
                    if (inventory.Loadout != null)
                    {
                        inventory.Loadout.Apply(EquipmentLoadout.FromNames(data.loadoutNames, lib));
                        inventory.Loadout.ApplyEnchantmentAssignments(data.enchantments, lib);
                    }
                    inventory.SetGold(data.gold);
                }
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PlayerInventoryProvider] Failed to load inventory: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Discards any cached/save file for the given player and replaces it with a fresh
        /// clone of the starter template. Called at the start of every run so each run
        /// begins with the base equipment regardless of previous progress.
        /// </summary>
        public static void ResetToTemplate(ulong steamId)
        {
            cache.Remove(steamId);
            var instance = ScriptableObject.CreateInstance<PlayerInventory>();
            ApplyTemplate(instance);
            EnsureLoadoutOwned(instance);
            cache[steamId] = instance;
            Save(steamId, instance);
        }

        public static void ResetForTests()
        {
            cache.Clear();
        }

        internal static string GetPath(ulong steamId)
        {
            string root = RootOverride ?? Application.persistentDataPath;
            return Path.Combine(root, $"{FilePrefix}{steamId}.json");
        }
    }

    [System.Serializable]
    public class InventorySaveData
    {
        public List<string> unlockedNames = new List<string>();
        public string[] loadoutNames = new string[EquipmentLoadout.SlotCount];
        public List<EnchantmentAssignment> enchantments = new List<EnchantmentAssignment>();
        public int gold;
    }
}
