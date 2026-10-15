using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Cards;
using Equipment;

namespace Tests.Equipment
{
    [TestFixture]
    public class EnchantmentTargetTests
    {
        private readonly List<Object> tracked = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in tracked)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            tracked.Clear();
        }

        private EquipmentEnchantmentSO Enchant()
        {
            var so = ScriptableObject.CreateInstance<GrantCardEnchantmentSO>();
            so.EnchantmentName = "Test Enchantment";
            tracked.Add(so);
            return so;
        }

        private EquipmentData Item(EquipmentSlotType slot = EquipmentSlotType.Chest, string name = "Item")
        {
            var data = ScriptableObject.CreateInstance<EquipmentData>();
            data.EquipmentName = name;
            data.SlotType = slot;
            tracked.Add(data);
            return data;
        }

        private CardData Card()
        {
            CardData card = TestHelper.MakeCard("Test Card");
            tracked.Add(card);
            return card;
        }

        [Test]
        public void Unrestricted_AppliesToEveryPiece()
        {
            EquipmentEnchantmentSO enchant = Enchant();
            EquipmentData chest = Item(EquipmentSlotType.Chest, "Plate");
            EquipmentData boots = Item(EquipmentSlotType.Feet, "Boots");

            Assert.IsTrue(enchant.CanApplyTo(chest, false));
            Assert.IsTrue(enchant.CanApplyTo(boots, true), "A default enchantment has no restrictions.");
            Assert.IsNull(enchant.RestrictionFor(chest, false));
            Assert.AreEqual("", enchant.TargetSummary());
        }

        [Test]
        public void SlotRestriction_AllowsOnlyListedSlots()
        {
            EquipmentEnchantmentSO enchant = Enchant();
            enchant.AllowedSlots = EnchantTargetSlots.Chest;
            EquipmentData chest = Item(EquipmentSlotType.Chest, "Plate");
            EquipmentData boots = Item(EquipmentSlotType.Feet, "Boots");

            Assert.IsTrue(enchant.CanApplyTo(chest, false));
            Assert.IsFalse(enchant.CanApplyTo(boots, false));
            StringAssert.Contains("Chest", enchant.RestrictionFor(boots, false));
        }

        [Test]
        public void SlotRestriction_FlagsCombine()
        {
            EquipmentEnchantmentSO enchant = Enchant();
            enchant.AllowedSlots = EnchantTargetSlots.Head | EnchantTargetSlots.Chest;
            EquipmentData helm = Item(EquipmentSlotType.Head, "Helm");
            EquipmentData chest = Item(EquipmentSlotType.Chest, "Plate");
            EquipmentData boots = Item(EquipmentSlotType.Feet, "Boots");

            Assert.IsTrue(enchant.CanApplyTo(helm, false));
            Assert.IsTrue(enchant.CanApplyTo(chest, false));
            Assert.IsFalse(enchant.CanApplyTo(boots, false));
        }

        [Test]
        public void MinCards_RequiresAtLeastThatManyCards()
        {
            EquipmentEnchantmentSO enchant = Enchant();
            enchant.MinCards = 1;
            EquipmentData bare = Item();
            EquipmentData loaded = Item();
            loaded.Cards.Add(Card());

            Assert.IsFalse(enchant.CanApplyTo(bare, false), "A piece granting no cards cannot take a card-granting enchantment.");
            Assert.IsTrue(enchant.CanApplyTo(loaded, false));
            StringAssert.Contains("at least 1 card", enchant.RestrictionFor(bare, false));
        }

        [Test]
        public void MaxCardsZero_OnlyPiecesWithNoCards()
        {
            EquipmentEnchantmentSO enchant = Enchant();
            enchant.MaxCards = 0;
            EquipmentData bare = Item();
            EquipmentData loaded = Item();
            loaded.Cards.Add(Card());

            Assert.IsTrue(enchant.CanApplyTo(bare, false));
            Assert.IsFalse(enchant.CanApplyTo(loaded, false));
            StringAssert.Contains("no cards", enchant.RestrictionFor(loaded, false));
        }

        [Test]
        public void Triggers_MustHave_And_MustNotHave()
        {
            EquipmentData withTrigger = Item(name: "With");
            withTrigger.Triggers.Add(new EquipmentTrigger());
            EquipmentData without = Item(name: "Without");

            EquipmentEnchantmentSO needsTrigger = Enchant();
            needsTrigger.Triggers = TargetRequirement.MustHave;
            Assert.IsTrue(needsTrigger.CanApplyTo(withTrigger, false));
            Assert.IsFalse(needsTrigger.CanApplyTo(without, false));

            EquipmentEnchantmentSO noTrigger = Enchant();
            noTrigger.Triggers = TargetRequirement.MustNotHave;
            Assert.IsFalse(noTrigger.CanApplyTo(withTrigger, false));
            Assert.IsTrue(noTrigger.CanApplyTo(without, false));
        }

        [Test]
        public void BuiltInEnchantments_MustNotHave()
        {
            EquipmentEnchantmentSO enchant = Enchant();
            enchant.BuiltInEnchantments = TargetRequirement.MustNotHave;

            EquipmentData plain = Item(name: "Plain");
            EquipmentData magical = Item(name: "Magical");
            magical.Enchantments.Add(Enchant());

            Assert.IsTrue(enchant.CanApplyTo(plain, false));
            Assert.IsFalse(enchant.CanApplyTo(magical, false));
        }

        [Test]
        public void Placement_RequiresEquippedOrInBag()
        {
            EquipmentData item = Item();

            EquipmentEnchantmentSO equippedOnly = Enchant();
            equippedOnly.Placement = Placement.MustBeEquipped;
            Assert.IsTrue(equippedOnly.CanApplyTo(item, true));
            Assert.IsFalse(equippedOnly.CanApplyTo(item, false));

            EquipmentEnchantmentSO bagOnly = Enchant();
            bagOnly.Placement = Placement.MustBeInBag;
            Assert.IsTrue(bagOnly.CanApplyTo(item, false));
            Assert.IsFalse(bagOnly.CanApplyTo(item, true));
        }

        [Test]
        public void OnlyItems_Allowlist_And_NeverItems_Blocklist()
        {
            EquipmentData allowed = Item(name: "Allowed");
            EquipmentData other = Item(name: "Other");

            EquipmentEnchantmentSO only = Enchant();
            only.OnlyItems.Add(allowed);
            Assert.IsTrue(only.CanApplyTo(allowed, false));
            Assert.IsFalse(only.CanApplyTo(other, false), "A piece not on the allowlist is refused.");

            EquipmentEnchantmentSO never = Enchant();
            never.NeverItems.Add(other);
            Assert.IsTrue(never.CanApplyTo(allowed, false));
            Assert.IsFalse(never.CanApplyTo(other, false), "A blocklisted piece is refused.");
        }

        [Test]
        public void TargetSummary_ListsTheRestrictions()
        {
            EquipmentEnchantmentSO enchant = Enchant();
            enchant.AllowedSlots = EnchantTargetSlots.Head;
            enchant.MaxCards = 0;
            enchant.Triggers = TargetRequirement.MustNotHave;

            string summary = enchant.TargetSummary();

            StringAssert.Contains("Applies to:", summary);
            StringAssert.Contains("Head", summary);
            StringAssert.Contains("grants 0 cards", summary);
            StringAssert.Contains("no triggers", summary);
        }

        [Test]
        public void PlayerInventory_AddEnchantment_RefusesDisallowedPiece()
        {
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            tracked.Add(inventory);

            EquipmentData chest = Item(EquipmentSlotType.Chest, "Plate");
            EquipmentData boots = Item(EquipmentSlotType.Feet, "Boots");
            inventory.Unlock(chest);
            inventory.Unlock(boots);
            inventory.Loadout.Set(EquipmentSlotType.Chest, 0, chest);

            EquipmentEnchantmentSO chestOnly = Enchant();
            chestOnly.AllowedSlots = EnchantTargetSlots.Chest;

            Assert.IsTrue(inventory.AddEnchantment(chest, chestOnly));
            Assert.IsFalse(inventory.AddEnchantment(boots, chestOnly), "The inventory must refuse a piece the enchantment cannot target.");
            Assert.AreEqual(1, inventory.GetEnchantments(chest).Count);
            Assert.AreEqual(0, inventory.GetEnchantments(boots).Count);
        }
    }
}
