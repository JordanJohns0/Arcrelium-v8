using System.Collections.Generic;
using UnityEngine;
using Cards;

namespace Equipment
{
    /// <summary>
    /// Master asset holding every card and piece of equipment in the game, resolvable by
    /// name. Used to render/equip items another player owns that this machine has never
    /// seen, and to rehydrate saved inventories and incoming loadouts.
    ///
    /// Create once per project (right-click > Create >  > Game Data Library) and place it
    /// in a Resources folder. Falls back to the deck's own cards when the asset is absent.
    /// </summary>
    [CreateAssetMenu(fileName = "GameDataLibrary", menuName = "Arcrelium/Game Data Library")]
    public class GameDataLibrary : ScriptableObject
    {
        public List<CardData> allCards = new List<CardData>();
        public List<EquipmentData> allEquipment = new List<EquipmentData>();
        [Header("Ambient visuals")]
        [Tooltip("Prefab shown on tiles covered in fire.")]
        public GameObject fireEffectVisualPrefab;
        [Tooltip("Prefab shown on tiles slicked with oil.")]
        public GameObject oilEffectVisualPrefab;

        private static GameDataLibrary instance;
        private static bool instanceSearched;

        public static GameDataLibrary Instance
        {
            get
            {
                if (instance == null && !instanceSearched)
                {
                    instanceSearched = true;
                    instance = Resources.Load<GameDataLibrary>("GameDataLibrary");
                }
                return instance;
            }
        }

        /// <summary>Test seam: installs an explicit library and disables the Resources fallback.</summary>
        public static void SetInstance(GameDataLibrary library)
        {
            instance = library;
            instanceSearched = library != null;
        }

        public CardData GetCardByName(string cardName)
        {
            if (string.IsNullOrEmpty(cardName)) return null;

            // Prefer this asset's own list (standalone/tested instances behave exactly
            // as before), then fall back to the global instance.
            CardData direct = FindCard(this, cardName);
            if (direct != null) return direct;

            var lib = Instance;
            if (!ReferenceEquals(lib, this))
            {
                if (lib == null)
                {
                    // One retry: a stale SetInstance(null) (test seam) or an earlier failed
                    // load must not permanently blind name resolution for the session.
                    instanceSearched = false;
                    lib = Instance;
                }
                CardData global = FindCard(lib, cardName);
                if (global != null) return global;
            }
            return null;
        }

        public EquipmentData GetEquipmentByName(string equipmentName)
        {
            if (string.IsNullOrEmpty(equipmentName)) return null;

            EquipmentData direct = FindEquipment(this, equipmentName);
            if (direct != null) return direct;

            var lib = Instance;
            if (!ReferenceEquals(lib, this))
            {
                if (lib == null)
                {
                    instanceSearched = false;
                    lib = Instance;
                }
                EquipmentData global = FindEquipment(lib, equipmentName);
                if (global != null) return global;
            }
            return null;
        }

        private static CardData FindCard(GameDataLibrary lib, string cardName)
        {
            if (lib == null || lib.allCards == null) return null;
            foreach (var c in lib.allCards)
            {
                if (c != null && c.CardName == cardName) return c;
            }
            return null;
        }

        private static EquipmentData FindEquipment(GameDataLibrary lib, string equipmentName)
        {
            if (lib == null || lib.allEquipment == null) return null;
            foreach (var e in lib.allEquipment)
            {
                if (e != null && e.EquipmentName == equipmentName) return e;
            }
            return null;
        }
    }
}
