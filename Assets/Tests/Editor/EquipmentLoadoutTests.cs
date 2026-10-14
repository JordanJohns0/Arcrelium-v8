using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Cards;
using Equipment;

namespace Tests.Equipment
{
    [TestFixture]
    public class EquipmentLoadoutTests
    {
        private readonly List<ScriptableObject> tracked = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            PlayerInventoryProvider.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            GameDataLibrary.SetInstance(null);
            RewardPool.SetMain(null);
            PlayerInventoryProvider.ResetForTests();
            PlayerInventoryProvider.RootOverride = null;
            PlayerInventoryProvider.DefaultOverride = null;
            foreach (var so in tracked)
            {
                if (so != null) UnityEngine.Object.DestroyImmediate(so);
            }
            tracked.Clear();
        }

        private EquipmentData MakeEquipment(EquipmentSlotType slot, string name)
        {
            var data = ScriptableObject.CreateInstance<EquipmentData>();
            data.EquipmentName = name;
            data.SlotType = slot;
            data.Cards = new List<CardData>();
            tracked.Add(data);
            return data;
        }

        private CardData MakeCard(string name)
        {
            var data = ScriptableObject.CreateInstance<CardData>();
            data.CardName = name;
            tracked.Add(data);
            return data;
        }

        private GameDataLibrary MakeLibrary(params EquipmentData[] equipment)
        {
            var library = ScriptableObject.CreateInstance<GameDataLibrary>();
            library.allEquipment.AddRange(equipment);
            library.allCards.AddRange(new[] { MakeCard("Slash"), MakeCard("Defend") });
            tracked.Add(library);
            return library;
        }

        private EquipmentEnchantmentSO MakeEnchantment(string name)
        {
            var so = ScriptableObject.CreateInstance<GrantCardEnchantmentSO>();
            so.EnchantmentName = name;
            tracked.Add(so);
            return so;
        }

        private EquipmentLoadout FullLoadout()
        {
            return new EquipmentLoadout
            {
                Head = MakeEquipment(EquipmentSlotType.Head, "Helm"),
                Chest = MakeEquipment(EquipmentSlotType.Chest, "Plate"),
                Feet = MakeEquipment(EquipmentSlotType.Feet, "Boots"),
                LeftHand = MakeEquipment(EquipmentSlotType.Hand, "Sword"),
                RightHand = MakeEquipment(EquipmentSlotType.Hand, "Shield"),
                Trinket1 = MakeEquipment(EquipmentSlotType.Trinket, "Ring 1"),
                Trinket2 = MakeEquipment(EquipmentSlotType.Trinket, "Ring 2"),
                Trinket3 = MakeEquipment(EquipmentSlotType.Trinket, "Amulet")
            };
        }

        [Test]
        public void Get_ReturnsBySlotAndIndex()
        {
            var loadout = FullLoadout();
            Assert.AreEqual("Helm", loadout.Get(EquipmentSlotType.Head).EquipmentName);
            Assert.AreEqual("Plate", loadout.Get(EquipmentSlotType.Chest).EquipmentName);
            Assert.AreEqual("Boots", loadout.Get(EquipmentSlotType.Feet).EquipmentName);
            Assert.AreEqual("Sword", loadout.Get(EquipmentSlotType.Hand, 0).EquipmentName);
            Assert.AreEqual("Shield", loadout.Get(EquipmentSlotType.Hand, 1).EquipmentName);
            Assert.AreEqual("Ring 1", loadout.Get(EquipmentSlotType.Trinket, 0).EquipmentName);
            Assert.AreEqual("Ring 2", loadout.Get(EquipmentSlotType.Trinket, 1).EquipmentName);
            Assert.AreEqual("Amulet", loadout.Get(EquipmentSlotType.Trinket, 2).EquipmentName);
        }

        [Test]
        public void Set_WritesBySlotAndIndex()
        {
            var loadout = new EquipmentLoadout();
            var sword = MakeEquipment(EquipmentSlotType.Hand, "Sword");
            var shield = MakeEquipment(EquipmentSlotType.Hand, "Shield");

            loadout.Set(EquipmentSlotType.Hand, 0, sword);
            loadout.Set(EquipmentSlotType.Hand, 1, shield);
            loadout.Set(EquipmentSlotType.Trinket, 2, MakeEquipment(EquipmentSlotType.Trinket, "Amulet"));

            Assert.AreEqual(sword, loadout.LeftHand);
            Assert.AreEqual(shield, loadout.RightHand);
            Assert.AreEqual("Amulet", loadout.Trinket3.EquipmentName);
        }

        [Test]
        public void ToNames_FixedSlotOrder()
        {
            var names = FullLoadout().ToNames();
            CollectionAssert.AreEqual(
                new[] { "Helm", "Plate", "Boots", "Sword", "Shield", "Ring 1", "Ring 2", "Amulet" },
                names);
        }

        [Test]
        public void ToNames_EmptySlots_ExportAsEmptyStrings()
        {
            var names = new EquipmentLoadout().ToNames();
            Assert.AreEqual(8, names.Length);
            CollectionAssert.AreEqual(new[] { "", "", "", "", "", "", "", "" }, names);
        }

        [Test]
        public void FromNames_RoundTripsFullLoadout()
        {
            var loadout = FullLoadout();
            var library = MakeLibrary(
                loadout.Head, loadout.Chest, loadout.Feet,
                loadout.LeftHand, loadout.RightHand,
                loadout.Trinket1, loadout.Trinket2, loadout.Trinket3);

            var restored = EquipmentLoadout.FromNames(loadout.ToNames(), library);

            Assert.AreEqual("Helm", restored.Head?.EquipmentName);
            Assert.AreEqual("Plate", restored.Chest?.EquipmentName);
            Assert.AreEqual("Boots", restored.Feet?.EquipmentName);
            Assert.AreEqual("Sword", restored.LeftHand?.EquipmentName);
            Assert.AreEqual("Shield", restored.RightHand?.EquipmentName);
            Assert.AreEqual("Ring 1", restored.Trinket1?.EquipmentName);
            Assert.AreEqual("Ring 2", restored.Trinket2?.EquipmentName);
            Assert.AreEqual("Amulet", restored.Trinket3?.EquipmentName);
        }

        [Test]
        public void FromNames_UnknownNames_BecomeEmptySlots()
        {
            var library = MakeLibrary();
            var restored = EquipmentLoadout.FromNames(
                new[] { "Helm", "Plate", "Boots", "Sword", "Shield", "Ring 1", "Ring 2", "Amulet" },
                library);

            Assert.IsTrue(restored.IsEmpty);
        }

        [Test]
        public void FromNames_NullOrMissingLibrary_ReturnsEmpty()
        {
            var restored = EquipmentLoadout.FromNames(new string[8], null);
            Assert.IsNotNull(restored);
            Assert.IsTrue(restored.IsEmpty);
        }

        [Test]
        public void FromNames_NullNames_ReturnsEmpty()
        {
            var library = MakeLibrary();
            Assert.IsTrue(EquipmentLoadout.FromNames(null, library).IsEmpty);
        }

        [Test]
        public void GetAllCards_CollectsAcrossAllSlots()
        {
            var loadout = FullLoadout();
            loadout.Head.Cards = new List<CardData> { MakeCard("Slash"), MakeCard("Defend") };
            loadout.Trinket1.Cards = new List<CardData> { MakeCard("Heal") };

            var cards = loadout.GetAllCards();
            Assert.AreEqual(3, cards.Count);
        }

        [Test]
        public void GetAllCards_EmptyWhenNothingEquipped()
        {
            Assert.AreEqual(0, new EquipmentLoadout().GetAllCards().Count);
        }

        [Test]
        public void Apply_CopiesEverySlot()
        {
            var source = FullLoadout();
            var target = new EquipmentLoadout();
            target.Apply(source);
            CollectionAssert.AreEqual(source.ToNames(), target.ToNames());
        }

        [Test]
        public void Apply_Null_DoesNothing()
        {
            var target = FullLoadout();
            target.Apply(null);
            Assert.IsFalse(target.IsEmpty);
        }

        [Test]
        public void Clear_EmptiesLoadout()
        {
            var loadout = FullLoadout();
            loadout.Clear();
            Assert.IsTrue(loadout.IsEmpty);
        }

        [Test]
        public void RollUnlocked_ExcludesAlreadyOwned()
        {
            var pool = ScriptableObject.CreateInstance<RewardPool>();
            tracked.Add(pool);
            var owned = MakeEquipment(EquipmentSlotType.Head, "Helm");
            var fresh1 = MakeEquipment(EquipmentSlotType.Chest, "Plate");
            var fresh2 = MakeEquipment(EquipmentSlotType.Feet, "Boots");
            pool.rewards.AddRange(new[] { owned, fresh1, fresh2 });

            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            tracked.Add(inventory);
            inventory.Unlock(owned);

            var rolled = pool.RollUnlocked(inventory, new System.Random(123));
            Assert.IsNotNull(rolled);
            Assert.AreNotEqual(owned, rolled);
            Assert.Contains(rolled, new[] { fresh1, fresh2 });
        }

        [Test]
        public void RollUnlocked_SeededRng_IsDeterministic()
        {
            var pool = ScriptableObject.CreateInstance<RewardPool>();
            tracked.Add(pool);
            pool.rewards.AddRange(new[]
            {
                MakeEquipment(EquipmentSlotType.Head, "A"),
                MakeEquipment(EquipmentSlotType.Chest, "B"),
                MakeEquipment(EquipmentSlotType.Feet, "C")
            });
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            tracked.Add(inventory);

            var first = pool.RollUnlocked(inventory, new System.Random(42));
            var second = pool.RollUnlocked(inventory, new System.Random(42));
            Assert.AreEqual(first, second);
            Assert.IsNotNull(first);
        }

        [Test]
        public void RollUnlocked_EmptyPool_ReturnsNull()
        {
            var pool = ScriptableObject.CreateInstance<RewardPool>();
            tracked.Add(pool);
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            tracked.Add(inventory);

            Assert.IsNull(pool.RollUnlocked(inventory, new System.Random(1)));
        }

        [Test]
        public void RollUnlocked_AllOwned_ReturnsNull()
        {
            var pool = ScriptableObject.CreateInstance<RewardPool>();
            tracked.Add(pool);
            var item = MakeEquipment(EquipmentSlotType.Head, "Helm");
            pool.rewards.Add(item);

            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            tracked.Add(inventory);
            inventory.Unlock(item);

            Assert.IsNull(pool.RollUnlocked(inventory, new System.Random(1)));
        }

        [Test]
        public void RollUnlocked_NullInventory_ReturnsNull()
        {
            var pool = ScriptableObject.CreateInstance<RewardPool>();
            tracked.Add(pool);
            pool.rewards.Add(MakeEquipment(EquipmentSlotType.Head, "Helm"));

            Assert.IsNull(pool.RollUnlocked(null, new System.Random(1)));
        }

        [Test]
        public void RollUnlocked_ExcludesEquippedButNotInUnlockedList()
        {
            var equippedTrinket = MakeEquipment(EquipmentSlotType.Trinket, "Lucky Opal");
            var fresh = MakeEquipment(EquipmentSlotType.Head, "Helm");

            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            tracked.Add(inventory);
            // Equipped items are not required to sit in the unlocked list (the UI used to
            // remove them on equip); the roll must still treat them as owned.
            inventory.Loadout.Set(EquipmentSlotType.Trinket, 0, equippedTrinket);
            Assert.IsTrue(inventory.Loadout.ContainsItem(equippedTrinket));
            Assert.IsFalse(inventory.IsUnlocked(equippedTrinket));

            var pool = ScriptableObject.CreateInstance<RewardPool>();
            tracked.Add(pool);
            pool.rewards.AddRange(new[] { equippedTrinket, fresh });

            for (int i = 0; i < 20; i++)
                Assert.AreNotEqual(equippedTrinket, pool.RollUnlocked(inventory, new System.Random(i)));
        }

        [Test]
        public void RollUnlocked_AllEquipped_ReturnsNull()
        {
            var trinket = MakeEquipment(EquipmentSlotType.Trinket, "Lucky Opal");

            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            tracked.Add(inventory);
            inventory.Loadout.Set(EquipmentSlotType.Trinket, 0, trinket);

            var pool = ScriptableObject.CreateInstance<RewardPool>();
            tracked.Add(pool);
            pool.rewards.Add(trinket);

            Assert.IsNull(pool.RollUnlocked(inventory, new System.Random(1)));
        }

        [Test]
        public void GetEquipmentByName_MatchesExactName()
        {
            var library = MakeLibrary(MakeEquipment(EquipmentSlotType.Head, "Helm"));
            Assert.IsNotNull(library.GetEquipmentByName("Helm"));
            Assert.IsNull(library.GetEquipmentByName("helm"));
            Assert.IsNull(library.GetEquipmentByName(null));
        }

        [Test]
        public void GetCardByName_MatchesExactName()
        {
            var library = MakeLibrary();
            Assert.IsNotNull(library.GetCardByName("Slash"));
            Assert.IsNull(library.GetCardByName("Nope"));
        }

        private static string TempRoot()
        {
            return Path.Combine(Path.GetTempPath(), "ArcreliumLoadoutTests");
        }

        [Test]
        public void AddEnchantment_ThenGetEnchantments_ReturnsIt()
        {
            var loadout = new EquipmentLoadout();
            var sword = MakeEquipment(EquipmentSlotType.Hand, "Sword");
            var enchantment = MakeEnchantment("Sharpened");

            loadout.AddEnchantment(sword, enchantment);

            CollectionAssert.Contains(new List<EquipmentEnchantmentSO>(loadout.GetEnchantments(sword)), enchantment);
            Assert.IsTrue(loadout.HasEnchantment(sword, enchantment));
        }

        [Test]
        public void GetEnchantments_UnknownItem_ReturnsEmpty()
        {
            var loadout = new EquipmentLoadout();
            var sword = MakeEquipment(EquipmentSlotType.Hand, "Sword");
            var other = MakeEquipment(EquipmentSlotType.Hand, "Axe");
            loadout.AddEnchantment(sword, MakeEnchantment("Sharpened"));

            Assert.AreEqual(0, loadout.GetEnchantments(other).Count);
            CollectionAssert.IsEmpty(loadout.GetEnchantments(null));
        }

        [Test]
        public void AddEnchantment_StacksMultipleOnSameItem()
        {
            var loadout = new EquipmentLoadout();
            var sword = MakeEquipment(EquipmentSlotType.Hand, "Sword");
            var first = MakeEnchantment("Sharpened");
            var second = MakeEnchantment("Blessed");

            loadout.AddEnchantment(sword, first);
            loadout.AddEnchantment(sword, second);

            Assert.AreEqual(2, loadout.GetEnchantments(sword).Count, "Enchantments must stack on one piece.");
        }

        [Test]
        public void AddEnchantment_IgnoresNullArguments()
        {
            var loadout = new EquipmentLoadout();
            var sword = MakeEquipment(EquipmentSlotType.Hand, "Sword");

            loadout.AddEnchantment(sword, null);
            loadout.AddEnchantment(null, MakeEnchantment("Sharpened"));

            Assert.AreEqual(0, loadout.enchantments.Count);
        }

        [Test]
        public void ToEnchantmentAssignments_RoundTripsThroughLibrary()
        {
            var loadout = new EquipmentLoadout();
            var sword = MakeEquipment(EquipmentSlotType.Hand, "Sword");
            var first = MakeEnchantment("Sharpened");
            var second = MakeEnchantment("Blessed");
            loadout.AddEnchantment(sword, first);
            loadout.AddEnchantment(sword, second);

            var library = MakeLibrary(sword);
            library.allEnchantments.AddRange(new[] { first, second });

            var assignments = loadout.ToEnchantmentAssignments();
            Assert.AreEqual(1, assignments.Count);
            Assert.AreEqual("Sword", assignments[0].equipmentName);
            CollectionAssert.AreEquivalent(new[] { "Sharpened", "Blessed" }, assignments[0].enchantmentNames);

            var restored = new EquipmentLoadout();
            restored.ApplyEnchantmentAssignments(assignments, library);

            var resolved = new List<EquipmentEnchantmentSO>(restored.GetEnchantments(sword));
            Assert.AreEqual(2, resolved.Count);
            CollectionAssert.Contains(resolved, first);
            CollectionAssert.Contains(resolved, second);
        }

        [Test]
        public void ApplyEnchantmentAssignments_UnknownNames_AreDropped()
        {
            var sword = MakeEquipment(EquipmentSlotType.Hand, "Sword");
            var enchantment = MakeEnchantment("Sharpened");
            var library = MakeLibrary(sword);
            library.allEnchantments.Add(enchantment);

            var loadout = new EquipmentLoadout();
            loadout.ApplyEnchantmentAssignments(new List<EnchantmentAssignment>
            {
                new EnchantmentAssignment
                {
                    equipmentName = "Sword",
                    enchantmentNames = new List<string> { "Sharpened", "Missing" }
                },
                new EnchantmentAssignment
                {
                    equipmentName = "Missing Equipment",
                    enchantmentNames = new List<string> { "Sharpened" }
                }
            }, library);

            Assert.AreEqual(1, loadout.enchantments.Count);
            Assert.AreEqual(1, loadout.GetEnchantments(sword).Count);
        }

        [Test]
        public void Apply_CopiesEnchantmentsWithoutAliasing()
        {
            var source = new EquipmentLoadout();
            var sword = MakeEquipment(EquipmentSlotType.Hand, "Sword");
            var enchantment = MakeEnchantment("Sharpened");
            source.AddEnchantment(sword, enchantment);

            var copy = new EquipmentLoadout();
            copy.Apply(source);

            Assert.AreEqual(1, copy.GetEnchantments(sword).Count);
            // Mutating the source's innermost list must not reach into the copy.
            source.enchantments[0].enchantments.Add(MakeEnchantment("Blessed"));
            Assert.AreEqual(1, copy.GetEnchantments(sword).Count, "Apply must deep-copy the enchantment lists.");
        }

        [Test]
        public void Clear_RemovesEnchantments()
        {
            var loadout = new EquipmentLoadout();
            var sword = MakeEquipment(EquipmentSlotType.Hand, "Sword");
            loadout.Set(EquipmentSlotType.Hand, 0, sword);
            loadout.AddEnchantment(sword, MakeEnchantment("Sharpened"));

            loadout.Clear();

            Assert.AreEqual(0, loadout.enchantments.Count);
            Assert.IsTrue(loadout.IsEmpty);
        }

        [Test]
        public void GetAllCards_IncludesEnchantmentGrantedCards()
        {
            var loadout = new EquipmentLoadout();
            var sword = MakeEquipment(EquipmentSlotType.Hand, "Sword");
            loadout.Set(EquipmentSlotType.Hand, 0, sword);
            var granted = MakeCard("Granted Strike");
            var enchantment = ScriptableObject.CreateInstance<GrantCardEnchantmentSO>();
            enchantment.EnchantmentName = "Granting";
            enchantment.GrantedCard = granted;
            tracked.Add(enchantment);
            loadout.AddEnchantment(sword, enchantment);

            var cards = loadout.GetAllCards();

            CollectionAssert.Contains(cards, granted,
                "Loadout deck building must include cards granted by its enchantments.");
        }

        [Test]
        public void Provider_SaveThenLoad_RoundTripsEnchantments()
        {
            PlayerInventoryProvider.RootOverride = TempRoot();
            Directory.CreateDirectory(TempRoot());

            var library = MakeLibrary();
            var helm = MakeEquipment(EquipmentSlotType.Head, "Helm");
            var enchantment = MakeEnchantment("Blessed");
            library.allEquipment.Add(helm);
            library.allEnchantments.Add(enchantment);
            GameDataLibrary.SetInstance(library);

            const ulong steamId = 6161;
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            tracked.Add(inventory);
            inventory.Unlock(helm);
            inventory.Loadout.Set(EquipmentSlotType.Head, 0, helm);
            inventory.AddEnchantment(helm, enchantment);
            PlayerInventoryProvider.Save(steamId, inventory);

            PlayerInventoryProvider.ResetForTests();
            var loaded = PlayerInventoryProvider.GetFor(steamId);

            var resolved = new List<EquipmentEnchantmentSO>(loaded.Loadout.GetEnchantments(loaded.Loadout.Head));
            Assert.AreEqual(1, resolved.Count, "Enchantments must round-trip through the save file.");
            Assert.AreEqual("Blessed", resolved[0].EnchantmentName);

            string path = PlayerInventoryProvider.GetPath(steamId);
            if (File.Exists(path)) File.Delete(path);
        }

        [Test]
        public void Provider_SaveThenLoad_RoundTripsUnlocksAndLoadout()
        {
            PlayerInventoryProvider.RootOverride = TempRoot();
            Directory.CreateDirectory(TempRoot());

            var library = MakeLibrary();
            var helm = MakeEquipment(EquipmentSlotType.Head, "Helm");
            var plate = MakeEquipment(EquipmentSlotType.Chest, "Plate");
            library.allEquipment.AddRange(new[] { helm, plate });
            GameDataLibrary.SetInstance(library);

            const ulong steamId = 5555;
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            tracked.Add(inventory);
            inventory.Unlock(helm);
            inventory.Unlock(plate);
            inventory.Loadout.Set(EquipmentSlotType.Head, 0, helm);
            inventory.Loadout.Set(EquipmentSlotType.Chest, 0, plate);
            inventory.AddGold(37);

            PlayerInventoryProvider.Save(steamId, inventory);

            // A fresh process (no cache) loads straight from disk.
            PlayerInventoryProvider.ResetForTests();
            var loaded = PlayerInventoryProvider.GetFor(steamId);

            Assert.AreEqual(2, loaded.UnlockedEquipment.Count);
            Assert.AreEqual("Helm", loaded.Loadout.Head?.EquipmentName);
            Assert.AreEqual("Plate", loaded.Loadout.Chest?.EquipmentName);
            Assert.AreEqual(37, loaded.Gold, "Gold must round-trip through the save file.");

            string path = PlayerInventoryProvider.GetPath(steamId);
            Assert.IsTrue(File.Exists(path));
            File.Delete(path);
        }

        [Test]
        public void Provider_NewAccount_StartsWithTemplateUnlocksAndLoadout()
        {
            var template = ScriptableObject.CreateInstance<PlayerInventory>();
            tracked.Add(template);
            var helm = MakeEquipment(EquipmentSlotType.Head, "Helm");
            var plate = MakeEquipment(EquipmentSlotType.Chest, "Plate");
            template.Unlock(helm);
            template.Unlock(plate);
            template.Loadout.Set(EquipmentSlotType.Head, 0, helm);
            template.Loadout.Set(EquipmentSlotType.Chest, 0, plate);
            PlayerInventoryProvider.DefaultOverride = template;
            PlayerInventoryProvider.RootOverride = TempRoot();
            Directory.CreateDirectory(TempRoot());

            var fresh = PlayerInventoryProvider.GetFor(4242);

            Assert.AreEqual(2, fresh.UnlockedEquipment.Count);
            Assert.AreEqual(helm, fresh.Loadout.Head);
            Assert.AreEqual(plate, fresh.Loadout.Chest);

            string path = PlayerInventoryProvider.GetPath(4242);
            if (File.Exists(path)) File.Delete(path);
        }

        [Test]
        public void Provider_ExistingSave_IgnoresTemplateLoadout()
        {
            PlayerInventoryProvider.RootOverride = TempRoot();
            Directory.CreateDirectory(TempRoot());
            var library = MakeLibrary();
            var helm = MakeEquipment(EquipmentSlotType.Head, "Helm");
            library.allEquipment.Add(helm);
            GameDataLibrary.SetInstance(library);

            const ulong steamId = 7777;
            var saved = ScriptableObject.CreateInstance<PlayerInventory>();
            tracked.Add(saved);
            saved.Unlock(helm);
            PlayerInventoryProvider.Save(steamId, saved);

            var template = ScriptableObject.CreateInstance<PlayerInventory>();
            tracked.Add(template);
            template.Unlock(MakeEquipment(EquipmentSlotType.Feet, "Boots"));
            template.Loadout.Set(EquipmentSlotType.Head, 0, MakeEquipment(EquipmentSlotType.Head, "Template Helm"));
            PlayerInventoryProvider.DefaultOverride = template;

            PlayerInventoryProvider.ResetForTests();
            var loaded = PlayerInventoryProvider.GetFor(steamId);

            Assert.AreEqual(1, loaded.UnlockedEquipment.Count);
            Assert.IsNull(loaded.Loadout.Head);

            string path = PlayerInventoryProvider.GetPath(steamId);
            if (File.Exists(path)) File.Delete(path);
        }

        [Test]
        public void Provider_GetFor_CachesSameInstance()
        {
            var first = PlayerInventoryProvider.GetFor(77);
            var second = PlayerInventoryProvider.GetFor(77);
            Assert.AreSame(first, second);

            PlayerInventoryProvider.ResetForTests();
            var third = PlayerInventoryProvider.GetFor(77);
            Assert.AreNotSame(first, third);
        }

        [Test]
        public void Provider_LoadInto_MissingFile_ReturnsFalse()
        {
            PlayerInventoryProvider.RootOverride = TempRoot();
            Directory.CreateDirectory(TempRoot());

            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            tracked.Add(inventory);
            Assert.IsFalse(PlayerInventoryProvider.LoadInto(inventory, 12345));
        }

        [Test]
        public void Provider_LoadInto_CorruptFile_ReturnsFalse()
        {
            PlayerInventoryProvider.RootOverride = TempRoot();
            Directory.CreateDirectory(TempRoot());

            string path = PlayerInventoryProvider.GetPath(999);
            File.WriteAllText(path, "{ not valid json!!");

            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            tracked.Add(inventory);
            try
            {
                Assert.IsFalse(PlayerInventoryProvider.LoadInto(inventory, 999));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void ResetToTemplate_ClearsCacheAndAppliesTemplate()
        {
            PlayerInventoryProvider.RootOverride = TempRoot();
            Directory.CreateDirectory(TempRoot());

            var template = ScriptableObject.CreateInstance<PlayerInventory>();
            tracked.Add(template);
            var templateHelm = MakeEquipment(EquipmentSlotType.Head, "Template Helm");
            template.Unlock(templateHelm);
            template.Loadout.Set(EquipmentSlotType.Head, 0, templateHelm);
            PlayerInventoryProvider.DefaultOverride = template;

            const ulong steamId = 8888;

            // First call populates cache with template defaults.
            var before = PlayerInventoryProvider.GetFor(steamId);
            Assert.AreEqual(templateHelm, before.Loadout.Head);

            // Simulate mid-run progress: equip a different item and unlock extra gear.
            var upgradedHelm = MakeEquipment(EquipmentSlotType.Head, "Upgraded Helm");
            before.Loadout.Set(EquipmentSlotType.Head, 0, upgradedHelm);
            before.Unlock(upgradedHelm);
            Assert.AreEqual(upgradedHelm, before.Loadout.Head);

            // ResetToTemplate should wipe the progress.
            PlayerInventoryProvider.ResetToTemplate(steamId);
            var after = PlayerInventoryProvider.GetFor(steamId);

            Assert.AreEqual(templateHelm, after.Loadout.Head,
                "Loadout must revert to the template after reset.");
            Assert.AreEqual(1, after.UnlockedEquipment.Count,
                "Unlocked list must match the template after reset.");
            Assert.IsTrue(after.IsUnlocked(templateHelm),
                "Template items must be in the unlocked list.");
            Assert.IsFalse(after.IsUnlocked(upgradedHelm),
                "Non-template items must not survive the reset.");

            string path = PlayerInventoryProvider.GetPath(steamId);
            if (File.Exists(path)) File.Delete(path);
        }

        [Test]
        public void ResetToTemplate_SavesToDiskSoFreshProcessGetsTemplate()
        {
            PlayerInventoryProvider.RootOverride = TempRoot();
            Directory.CreateDirectory(TempRoot());

            var template = ScriptableObject.CreateInstance<PlayerInventory>();
            tracked.Add(template);
            var templateShield = MakeEquipment(EquipmentSlotType.Hand, "Wooden Shield");
            template.Unlock(templateShield);
            template.Loadout.Set(EquipmentSlotType.Hand, 0, templateShield);
            PlayerInventoryProvider.DefaultOverride = template;

            const ulong steamId = 9999;

            // Load, modify, then reset.
            var inv = PlayerInventoryProvider.GetFor(steamId);
            inv.Loadout.Set(EquipmentSlotType.Hand, 0,
                MakeEquipment(EquipmentSlotType.Hand, "Legendary Sword"));
            PlayerInventoryProvider.ResetToTemplate(steamId);

            // Simulate a fresh process: clear cache and reload from disk.
            PlayerInventoryProvider.ResetForTests();
            var reloaded = PlayerInventoryProvider.GetFor(steamId);

            Assert.AreEqual(templateShield, reloaded.Loadout.LeftHand,
                "After a fresh load from disk, the loadout must match the template.");

            string path = PlayerInventoryProvider.GetPath(steamId);
            if (File.Exists(path)) File.Delete(path);
        }

        [Test]
        public void ResetToTemplate_MissingTemplate_DoesNotThrow()
        {
            PlayerInventoryProvider.RootOverride = TempRoot();
            Directory.CreateDirectory(TempRoot());
            PlayerInventoryProvider.DefaultOverride = ScriptableObject.CreateInstance<PlayerInventory>();
            tracked.Add(PlayerInventoryProvider.DefaultOverride);
            GameDataLibrary.SetInstance(null);

            // Should not throw even with an empty template (no unlocks, no loadout).
            PlayerInventoryProvider.ResetToTemplate(12345);

            var inv = PlayerInventoryProvider.GetFor(12345);
            Assert.IsNotNull(inv);
            Assert.IsTrue(inv.UnlockedEquipment == null || inv.UnlockedEquipment.Count == 0);

            string path = PlayerInventoryProvider.GetPath(12345);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
