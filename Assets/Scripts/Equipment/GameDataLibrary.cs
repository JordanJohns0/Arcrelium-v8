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
            foreach (var c in allCards)
            {
                if (c != null && c.CardName == cardName) return c;
            }
            return null;
        }

        public EquipmentData GetEquipmentByName(string equipmentName)
        {
            if (string.IsNullOrEmpty(equipmentName)) return null;
            foreach (var e in allEquipment)
            {
                if (e != null && e.EquipmentName == equipmentName) return e;
            }
            return null;
        }
    }
}
