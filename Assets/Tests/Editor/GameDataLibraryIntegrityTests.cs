using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Cards;
using Equipment;

namespace Tests.Equipment
{
    /// <summary>
    /// Network play resolves cards/equipment BY NAME from the GameDataLibrary. That only
    /// ever shows the correct item if every asset is registered and no two assets share a
    /// name — these tests lock both invariants so popups can never go wrong silently.
    /// </summary>
    public class GameDataLibraryIntegrityTests
    {
        private static GameDataLibrary LoadLibrary()
        {
            return AssetDatabase.LoadAssetAtPath<GameDataLibrary>("Assets/Resources/GameDataLibrary.asset");
        }

        [Test]
        public void LibraryAsset_ExistsInResources()
        {
            Assert.IsNotNull(LoadLibrary(),
                "GameDataLibrary must live at Assets/Resources/GameDataLibrary.asset for runtime lookup.");
        }

        [Test]
        public void EveryCardAsset_IsRegisteredInTheLibrary()
        {
            var library = LoadLibrary();
            var unregistered = AllAssets<CardData>()
                .Where(card => card != null && !library.allCards.Contains(card))
                .Select(card => card.name)
                .ToList();

            CollectionAssert.IsEmpty(unregistered,
                "Card assets missing from GameDataLibrary (they would render as fallbacks on other machines): "
                + string.Join(", ", unregistered));
        }

        [Test]
        public void EveryEquipmentAsset_IsRegisteredInTheLibrary()
        {
            var library = LoadLibrary();
            var unregistered = AllAssets<EquipmentData>()
                .Where(equipment => equipment != null && !library.allEquipment.Contains(equipment))
                .Select(equipment => equipment.name)
                .ToList();

            CollectionAssert.IsEmpty(unregistered,
                "Equipment assets missing from GameDataLibrary: " + string.Join(", ", unregistered));
        }

        [Test]
        public void CardNames_AreUniqueAndNonEmpty()
        {
            var seen = new Dictionary<string, string>();
            var problems = new List<string>();
            foreach (CardData card in AllAssets<CardData>())
            {
                if (card == null) continue;
                if (string.IsNullOrWhiteSpace(card.CardName))
                {
                    problems.Add($"'{card.name}' has an empty CardName");
                    continue;
                }
                if (seen.TryGetValue(card.CardName, out string other))
                    problems.Add($"'{card.CardName}' is used by both '{other}' and '{card.name}'");
                else
                    seen[card.CardName] = card.name;
            }

            CollectionAssert.IsEmpty(problems,
                "Duplicate or empty card names make name-based lookups ambiguous: " + string.Join("; ", problems));
        }

        [Test]
        public void EquipmentNames_AreUniqueAndNonEmpty()
        {
            var seen = new Dictionary<string, string>();
            var problems = new List<string>();
            foreach (EquipmentData equipment in AllAssets<EquipmentData>())
            {
                if (equipment == null) continue;
                if (string.IsNullOrWhiteSpace(equipment.EquipmentName))
                {
                    problems.Add($"'{equipment.name}' has an empty EquipmentName");
                    continue;
                }
                if (seen.TryGetValue(equipment.EquipmentName, out string other))
                    problems.Add($"'{equipment.EquipmentName}' is used by both '{other}' and '{equipment.name}'");
                else
                    seen[equipment.EquipmentName] = equipment.name;
            }

            CollectionAssert.IsEmpty(problems,
                "Duplicate or empty equipment names break loadout/inventory resolution: " + string.Join("; ", problems));
        }

        private static IEnumerable<T> AllAssets<T>() where T : Object
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                yield return AssetDatabase.LoadAssetAtPath<T>(path);
            }
        }
    }
}
