using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Characters;
using Equipment;
using Game;

namespace Tests.Equipment
{
    using Cards = global::Cards;

    /// <summary>Test-only effect that counts how many times it has executed.</summary>
    public class CountingEffect : EquipmentEffect
    {
        public int Count;

        public override void Execute(EquipmentContext context) => Count++;
    }

    [TestFixture]
    public class GrantCardEnchantmentListTests
    {
        private GrantCardEnchantmentSO enchantment;
        private Cards.CardData first;
        private Cards.CardData second;
        private Cards.CardData legacy;

        [SetUp]
        public void SetUp()
        {
            enchantment = ScriptableObject.CreateInstance<GrantCardEnchantmentSO>();
            first = ScriptableObject.CreateInstance<Cards.CardData>();
            first.CardName = "First";
            second = ScriptableObject.CreateInstance<Cards.CardData>();
            second.CardName = "Second";
            legacy = ScriptableObject.CreateInstance<Cards.CardData>();
            legacy.CardName = "Legacy";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enchantment);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(legacy);
        }

        [Test]
        public void CollectGrantedCards_IncludesEveryListedCard()
        {
            enchantment.GrantedCards = new List<Cards.CardData> { first, second };

            var collected = new List<Cards.CardData>();
            enchantment.CollectGrantedCards(collected);

            CollectionAssert.AreEquivalent(new[] { first, second }, collected);
        }

        [Test]
        public void CollectGrantedCards_ListPlusLegacy_CollectsBoth()
        {
            enchantment.GrantedCards = new List<Cards.CardData> { first };
            enchantment.GrantedCard = legacy;

            var collected = new List<Cards.CardData>();
            enchantment.CollectGrantedCards(collected);

            CollectionAssert.AreEquivalent(new[] { first, legacy }, collected);
        }

        [Test]
        public void CollectGrantedCards_TwoLists_DoNotDuplicateSameCard()
        {
            enchantment.GrantedCards = new List<Cards.CardData> { first };
            enchantment.GrantedCard = first;

            var collected = new List<Cards.CardData>();
            enchantment.CollectGrantedCards(collected);

            Assert.AreEqual(1, collected.Count, "A card listed both ways must only be granted once.");
        }
    }

    [TestFixture]
    public class EnchantmentTriggerIntegrationTests
    {
        private static readonly FieldInfo characterEquipmentField =
            typeof(Character).GetField("equipmentManager", BindingFlags.NonPublic | BindingFlags.Instance);

        private EquipmentManager manager;
        private Character character;
        private readonly List<Object> trackedAssets = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            character = TestHelper.CreateCharacter(0, 0, 0);

            var go = new GameObject("TestEquipmentManager");
            manager = go.AddComponent<EquipmentManager>();
            characterEquipmentField.SetValue(character, manager);
            manager.Initialize(character);
        }

        [TearDown]
        public void TearDown()
        {
            if (manager != null) Object.DestroyImmediate(manager.gameObject);
            if (character != null)
            {
                Object.DestroyImmediate(character.CurrentCell.gameObject);
                Object.DestroyImmediate(character.gameObject);
            }
            foreach (Object asset in trackedAssets)
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }
            trackedAssets.Clear();
        }

        private EquipmentData MakeEquip(EquipmentSlotType slot, string name)
        {
            var data = ScriptableObject.CreateInstance<EquipmentData>();
            data.EquipmentName = name;
            data.SlotType = slot;
            trackedAssets.Add(data);
            return data;
        }

        private TriggeredEnchantmentSO MakeTriggeredEnchantment(TriggerTiming timing, out CountingEffect effect)
        {
            effect = ScriptableObject.CreateInstance<CountingEffect>();
            var enchantment = ScriptableObject.CreateInstance<TriggeredEnchantmentSO>();
            enchantment.TriggerList = new List<EquipmentTrigger>
            {
                new EquipmentTrigger { Timing = timing, Effect = effect }
            };
            trackedAssets.Add(effect);
            trackedAssets.Add(enchantment);
            return enchantment;
        }

        [Test]
        public void GetTriggers_IncludesEnchantmentTriggers()
        {
            var item = MakeEquip(EquipmentSlotType.Trinket, "Charged Charm");
            var enchantment = MakeTriggeredEnchantment(TriggerTiming.StartOfTurn, out _);
            item.Enchantments.Add(enchantment);

            manager.Equip(item);

            List<EquipmentTrigger> triggers = manager.GetTriggers(TriggerTiming.StartOfTurn);
            Assert.AreEqual(1, triggers.Count, "Enchantment triggers must be folded into GetTriggers.");
        }

        [Test]
        public void FireTriggers_RunsEnchantmentTriggerEffect()
        {
            var item = MakeEquip(EquipmentSlotType.Trinket, "Charged Charm");
            var enchantment = MakeTriggeredEnchantment(TriggerTiming.StartOfTurn, out CountingEffect effect);
            item.Enchantments.Add(enchantment);
            manager.Equip(item);

            manager.FireTriggers(TriggerTiming.StartOfTurn);

            Assert.AreEqual(1, effect.Count);
        }

        [Test]
        public void OncePerMatchTrigger_FiresOnlyOnce()
        {
            var item = MakeEquip(EquipmentSlotType.Trinket, "Sealed Charm");
            var enchantment = MakeTriggeredEnchantment(TriggerTiming.StartOfTurn, out CountingEffect effect);
            enchantment.TriggerList[0].OncePerMatch = true;
            item.Enchantments.Add(enchantment);
            manager.Equip(item);

            manager.FireTriggers(TriggerTiming.StartOfTurn);
            manager.FireTriggers(TriggerTiming.StartOfTurn);

            Assert.AreEqual(1, effect.Count, "A once-per-match trigger must never fire twice.");
        }

        [Test]
        public void OncePerTurnTrigger_DoesNotRepeatBeforeTurnReset()
        {
            var item = MakeEquip(EquipmentSlotType.Trinket, "Sealed Charm");
            var enchantment = MakeTriggeredEnchantment(TriggerTiming.OnGainBlock, out CountingEffect effect);
            enchantment.TriggerList[0].OncePerTurn = true;
            item.Enchantments.Add(enchantment);
            manager.Equip(item);

            character.GainBlock(1);
            character.GainBlock(1);

            Assert.AreEqual(1, effect.Count, "A once-per-turn trigger must not repeat until the turn resets.");
        }

        [Test]
        public void Condition_BlocksTriggerWhenFalse()
        {
            var item = MakeEquip(EquipmentSlotType.Trinket, "Empty-Deck Charm");
            var enchantment = MakeTriggeredEnchantment(TriggerTiming.StartOfTurn, out CountingEffect effect);
            var condition = ScriptableObject.CreateInstance<DeckSizeCondition>();
            condition.Region = DeckRegion.DrawPile;
            condition.Min = 1; // deck is empty in this test
            enchantment.TriggerList[0].Condition = condition;
            trackedAssets.Add(condition);
            item.Enchantments.Add(enchantment);
            manager.Equip(item);

            manager.FireTriggers(TriggerTiming.StartOfTurn);

            Assert.AreEqual(0, effect.Count, "Condition must gate the trigger off while it is false.");
        }

        [Test]
        public void Condition_AllowsTriggerWhenTrue()
        {
            var item = MakeEquip(EquipmentSlotType.Trinket, "Empty-Deck Charm");
            var enchantment = MakeTriggeredEnchantment(TriggerTiming.StartOfTurn, out CountingEffect effect);
            var condition = ScriptableObject.CreateInstance<DeckSizeCondition>();
            condition.Region = DeckRegion.DrawPile;
            condition.Min = 0; // no minimum -> always true
            condition.Max = 0; // and deck is empty -> still true
            enchantment.TriggerList[0].Condition = condition;
            trackedAssets.Add(condition);
            item.Enchantments.Add(enchantment);
            manager.Equip(item);

            manager.FireTriggers(TriggerTiming.StartOfTurn);

            Assert.AreEqual(1, effect.Count);
        }

        [Test]
        public void OnShuffleTrigger_FiresWhenDiscardReshuffles()
        {
            var item = MakeEquip(EquipmentSlotType.Trinket, "Shuffle Charm");
            var enchantment = MakeTriggeredEnchantment(TriggerTiming.OnShuffle, out CountingEffect effect);
            item.Enchantments.Add(enchantment);
            manager.Equip(item);

            var card = ScriptableObject.CreateInstance<Cards.CardData>();
            card.CardName = "Solo";
            trackedAssets.Add(card);
            character.Deck.SetStartingCards(new List<Cards.CardData> { card });
            character.Deck.Shuffle();

            Cards.CardInstance drawn = character.Deck.Draw();
            character.Deck.Discard(drawn);
            character.Deck.Draw(); // draw pile empty -> reshuffle discard -> OnShuffled

            Assert.AreEqual(1, effect.Count, "OnShuffle triggers must fire when the discard is reshuffled in.");
        }

        [Test]
        public void OnShuffleTrigger_DoesNotFireOnInitialShuffle()
        {
            var item = MakeEquip(EquipmentSlotType.Trinket, "Shuffle Charm");
            var enchantment = MakeTriggeredEnchantment(TriggerTiming.OnShuffle, out CountingEffect effect);
            item.Enchantments.Add(enchantment);
            manager.Equip(item);

            var card = ScriptableObject.CreateInstance<Cards.CardData>();
            card.CardName = "Solo";
            trackedAssets.Add(card);
            character.Deck.SetStartingCards(new List<Cards.CardData> { card });
            character.Deck.Shuffle();

            Assert.AreEqual(0, effect.Count, "The match-start shuffle must not count as a shuffle trigger.");
        }

        [Test]
        public void DoubleGrantedCards_DoublesOwnAndEnchantmentGrantedCards()
        {
            var own = ScriptableObject.CreateInstance<Cards.CardData>();
            own.CardName = "Own";
            var granted = ScriptableObject.CreateInstance<Cards.CardData>();
            granted.CardName = "Granted";
            trackedAssets.Add(own);
            trackedAssets.Add(granted);

            var item = MakeEquip(EquipmentSlotType.Trinket, "Twin Charm");
            item.Cards = new List<Cards.CardData> { own };

            var grant = ScriptableObject.CreateInstance<GrantCardEnchantmentSO>();
            grant.GrantedCards = new List<Cards.CardData> { granted };
            var doubler = ScriptableObject.CreateInstance<DoubleGrantedCardsEnchantmentSO>();
            item.Enchantments.Add(grant);
            item.Enchantments.Add(doubler);
            trackedAssets.Add(grant);
            trackedAssets.Add(doubler);
            manager.Equip(item);

            List<Cards.CardData> all = manager.GetAllCards();

            Assert.AreEqual(4, all.Count, "One own + one granted, doubled, is four.");
            Assert.AreEqual(2, CountOf(all, own));
            Assert.AreEqual(2, CountOf(all, granted));
        }

        [Test]
        public void DamageMultiplier_AppliesToOwnCardsOnly()
        {
            var own = ScriptableObject.CreateInstance<Cards.CardData>();
            own.CardName = "Own";
            var foreign = ScriptableObject.CreateInstance<Cards.CardData>();
            foreign.CardName = "Foreign";
            trackedAssets.Add(own);
            trackedAssets.Add(foreign);

            var item = MakeEquip(EquipmentSlotType.Hand, "Keen Blade");
            item.Cards = new List<Cards.CardData> { own };
            var enchantment = ScriptableObject.CreateInstance<DamageMultiplierEnchantmentSO>();
            enchantment.Multiplier = 2f;
            enchantment.OwnCardsOnly = true;
            item.Enchantments.Add(enchantment);
            trackedAssets.Add(enchantment);

            manager.Equip(item, EquipmentSlotType.Hand, 0);

            Assert.AreEqual(2f, manager.GetTotalCardDamageMultiplier(own));
            Assert.AreEqual(1f, manager.GetTotalCardDamageMultiplier(foreign));
        }

        [Test]
        public void DecreaseEquipmentCardCost_ReportsNegativeDeltaForOwnCards()
        {
            var own = ScriptableObject.CreateInstance<Cards.CardData>();
            own.CardName = "Own";
            var foreign = ScriptableObject.CreateInstance<Cards.CardData>();
            foreign.CardName = "Foreign";
            trackedAssets.Add(own);
            trackedAssets.Add(foreign);

            var item = MakeEquip(EquipmentSlotType.Hand, "Light Blade");
            item.Cards = new List<Cards.CardData> { own };
            var enchantment = ScriptableObject.CreateInstance<DecreaseEquipmentCardCostEnchantmentSO>();
            enchantment.Amount = 1;
            item.Enchantments.Add(enchantment);
            trackedAssets.Add(enchantment);

            manager.Equip(item, EquipmentSlotType.Hand, 0);

            Assert.AreEqual(-1, manager.GetCardCostDelta(own));
            Assert.AreEqual(0, manager.GetCardCostDelta(foreign));
        }

        private static int CountOf(List<Cards.CardData> cards, Cards.CardData target)
        {
            int count = 0;
            foreach (Cards.CardData card in cards)
                if (card == target) count++;
            return count;
        }
    }

    [TestFixture]
    public class DeckShuffleAndCostTests
    {
        private readonly List<Object> trackedAssets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in trackedAssets)
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }
            trackedAssets.Clear();
        }

        private Cards.CardData MakeCard(string name, int cost)
        {
            var card = ScriptableObject.CreateInstance<Cards.CardData>();
            card.CardName = name;
            card.Cost = cost;
            trackedAssets.Add(card);
            return card;
        }

        [Test]
        public void OnShuffled_FiresOnlyWhenDiscardReshuffles()
        {
            var deck = new Cards.Deck();
            var card = MakeCard("A", 1);
            deck.SetStartingCards(new List<Cards.CardData> { card });

            int shuffles = 0;
            deck.OnShuffled += () => shuffles++;

            deck.Shuffle();
            Assert.AreEqual(0, shuffles, "Initial Shuffle must not raise OnShuffled.");

            Cards.CardInstance drawn = deck.Draw();
            deck.Discard(drawn);
            deck.Draw();

            Assert.AreEqual(1, shuffles);
        }

        [Test]
        public void CostAdjuster_BakesOffsetIntoCreatedInstances()
        {
            var deck = new Cards.Deck();
            var cheapCard = MakeCard("Cheap", 3);
            deck.CostAdjuster = data => data == cheapCard ? -1 : 0;
            deck.SetStartingCards(new List<Cards.CardData> { cheapCard });

            deck.Shuffle();

            Assert.AreEqual(2, deck.DrawPile[0].BaseCost, "The discount must be baked into the created instance.");
        }

        [Test]
        public void SetCostOffset_ResetsRuntimeCostAndSurvivesResetCost()
        {
            var data = MakeCard("Base", 4);
            var instance = new Cards.CardInstance(data);

            instance.SetCostOffset(-2);
            Assert.AreEqual(2, instance.BaseCost);

            instance.Cost = 0; // a transient in-hand discount
            instance.ResetCost();
            Assert.AreEqual(2, instance.Cost, "ResetCost must restore the discounted base, not the raw card cost.");
        }

        [Test]
        public void BaseCost_NeverGoesNegative()
        {
            var data = MakeCard("Tiny", 1);
            var instance = new Cards.CardInstance(data);
            instance.SetCostOffset(-5);
            Assert.AreEqual(0, instance.BaseCost);
        }
    }
}
