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

    [TestFixture]
    public class BonusHealthEnchantmentTests
    {
        private BonusHealthEnchantmentSO enchantment;
        private EquipmentData equipment;
        private Character wearer;

        [SetUp]
        public void SetUp()
        {
            enchantment = ScriptableObject.CreateInstance<BonusHealthEnchantmentSO>();
            enchantment.BonusMaxHealth = 2;

            equipment = ScriptableObject.CreateInstance<EquipmentData>();
            equipment.EquipmentName = "Vital Charm";

            wearer = TestHelper.CreateCharacter(0, 0, 0);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(wearer.CurrentCell.gameObject);
            Object.DestroyImmediate(wearer.gameObject);
            Object.DestroyImmediate(equipment);
            Object.DestroyImmediate(enchantment);
        }

        private EquipmentInstance Instance() => new EquipmentInstance(equipment);

        [Test]
        public void OnEquipped_IncreasesMaxAndCurrentHealth()
        {
            int maxBefore = wearer.Stats.MaxHealth;
            int currentBefore = wearer.Stats.CurrentHealth;

            enchantment.OnEquipped(Instance(), wearer);

            Assert.AreEqual(maxBefore + 2, wearer.Stats.MaxHealth);
            Assert.AreEqual(currentBefore + 2, wearer.Stats.CurrentHealth);
        }

        [Test]
        public void OnUnequipped_RestoresMaxHealth()
        {
            int maxBefore = wearer.Stats.MaxHealth;
            enchantment.OnEquipped(Instance(), wearer);

            enchantment.OnUnequipped(Instance(), wearer);

            Assert.AreEqual(maxBefore, wearer.Stats.MaxHealth);
        }

        [Test]
        public void OnUnequipped_ClampsCurrentHealthToNewMax()
        {
            enchantment.OnEquipped(Instance(), wearer);
            wearer.Stats.CurrentHealth = wearer.Stats.MaxHealth - 1; // one below boosted max

            enchantment.OnUnequipped(Instance(), wearer);

            Assert.AreEqual(wearer.Stats.MaxHealth, wearer.Stats.CurrentHealth,
                "Current health above the reduced max must be clamped down.");
        }

        [Test]
        public void OnUnequipped_KeepsCurrentHealthWhenBelowMax()
        {
            enchantment.OnEquipped(Instance(), wearer);
            wearer.Stats.CurrentHealth = 4;

            enchantment.OnUnequipped(Instance(), wearer);

            Assert.AreEqual(4, wearer.Stats.CurrentHealth);
        }

        [Test]
        public void Hooks_NullWearer_DoNotThrow()
        {
            Assert.DoesNotThrow(() => enchantment.OnEquipped(Instance(), null));
            Assert.DoesNotThrow(() => enchantment.OnUnequipped(Instance(), null));
        }
    }

    [TestFixture]
    public class BonusDamageEnchantmentTests
    {
        private BonusDamageEnchantmentSO enchantment;
        private EquipmentData equipment;
        private Cards.CardData ownCard;
        private Cards.CardData foreignCard;

        [SetUp]
        public void SetUp()
        {
            enchantment = ScriptableObject.CreateInstance<BonusDamageEnchantmentSO>();
            enchantment.BonusDamage = 1;
            enchantment.OnlyOwnCards = true;

            ownCard = ScriptableObject.CreateInstance<Cards.CardData>();
            ownCard.CardName = "Own Slash";
            foreignCard = ScriptableObject.CreateInstance<Cards.CardData>();
            foreignCard.CardName = "Foreign Bolt";

            equipment = ScriptableObject.CreateInstance<EquipmentData>();
            equipment.Cards = new List<Cards.CardData> { ownCard };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enchantment);
            Object.DestroyImmediate(equipment);
            Object.DestroyImmediate(ownCard);
            Object.DestroyImmediate(foreignCard);
        }

        [Test]
        public void OnlyOwnCards_OwnCard_GetsBonus()
        {
            Assert.AreEqual(1, enchantment.GetDamageBonus(new EquipmentInstance(equipment), ownCard));
        }

        [Test]
        public void OnlyOwnCards_ForeignCard_NoBonus()
        {
            Assert.AreEqual(0, enchantment.GetDamageBonus(new EquipmentInstance(equipment), foreignCard));
        }

        [Test]
        public void AnyCards_ForeignCard_AlsoGetsBonus()
        {
            enchantment.OnlyOwnCards = false;
            Assert.AreEqual(1, enchantment.GetDamageBonus(new EquipmentInstance(equipment), foreignCard));
        }

        [Test]
        public void NullPlayedCard_NoBonus()
        {
            Assert.AreEqual(0, enchantment.GetDamageBonus(new EquipmentInstance(equipment), null));
        }

        [Test]
        public void ZeroBonus_ReturnsZero()
        {
            enchantment.BonusDamage = 0;
            Assert.AreEqual(0, enchantment.GetDamageBonus(new EquipmentInstance(equipment), ownCard));
        }
    }

    [TestFixture]
    public class GrantCardEnchantmentTests
    {
        private GrantCardEnchantmentSO enchantment;
        private EquipmentData equipment;
        private Cards.CardData grantedCard;
        private Cards.CardData baseCard;
        private Character wearer;

        [SetUp]
        public void SetUp()
        {
            enchantment = ScriptableObject.CreateInstance<GrantCardEnchantmentSO>();
            grantedCard = ScriptableObject.CreateInstance<Cards.CardData>();
            grantedCard.CardName = "Granted Strike";
            enchantment.GrantedCard = grantedCard;

            baseCard = ScriptableObject.CreateInstance<Cards.CardData>();
            baseCard.CardName = "Base Card";

            equipment = ScriptableObject.CreateInstance<EquipmentData>();

            wearer = TestHelper.CreateCharacter(0, 0, 0);
            wearer.Deck.SetStartingCards(new List<Cards.CardData> { baseCard });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(wearer.CurrentCell.gameObject);
            Object.DestroyImmediate(wearer.gameObject);
            Object.DestroyImmediate(enchantment);
            Object.DestroyImmediate(equipment);
            Object.DestroyImmediate(grantedCard);
            Object.DestroyImmediate(baseCard);
        }

        [Test]
        public void CollectGrantedCards_AddsGrantedCard()
        {
            var collected = new List<Cards.CardData>();

            enchantment.CollectGrantedCards(collected);

            Assert.Contains(grantedCard, collected);
        }

        [Test]
        public void CollectGrantedCards_NullCard_AddsNothing()
        {
            enchantment.GrantedCard = null;
            var collected = new List<Cards.CardData>();

            enchantment.CollectGrantedCards(collected);

            Assert.AreEqual(0, collected.Count);
        }

        [Test]
        public void OnEquipped_BeforeShuffle_JoinsStartingList()
        {
            enchantment.OnEquipped(new EquipmentInstance(equipment), wearer);

            wearer.Deck.Shuffle();

            bool found = false;
            foreach (Cards.CardInstance card in wearer.Deck.DrawPile)
                if (card.Data == grantedCard) found = true;
            Assert.IsTrue(found, "Granted card must be in the deck once it is shuffled.");
        }

        [Test]
        public void OnEquipped_AfterShuffle_InjectsIntoDrawPile()
        {
            wearer.Deck.Shuffle();

            enchantment.OnEquipped(new EquipmentInstance(equipment), wearer);

            Assert.AreEqual(2, wearer.Deck.DrawPile.Count, "Mid-match grant must be drawable immediately.");
        }

        [Test]
        public void OnUnequipped_RemovesCardFromDeck()
        {
            wearer.Deck.Shuffle();
            enchantment.OnEquipped(new EquipmentInstance(equipment), wearer);

            enchantment.OnUnequipped(new EquipmentInstance(equipment), wearer);

            foreach (Cards.CardInstance card in wearer.Deck.DrawPile)
                Assert.AreNotEqual(grantedCard, card.Data, "Revoked card must not remain in the draw pile.");
        }

        [Test]
        public void OnEquipped_NullWearer_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => enchantment.OnEquipped(new EquipmentInstance(equipment), null));
        }
    }

    [TestFixture]
    public class EnchantmentManagerIntegrationTests
    {
        private static readonly FieldInfo characterEquipmentField =
            typeof(Character).GetField("equipmentManager", BindingFlags.NonPublic | BindingFlags.Instance);

        private EquipmentManager manager;
        private Character character;

        [SetUp]
        public void SetUp()
        {
            character = TestHelper.CreateCharacter(0, 0, 0);

            var go = new GameObject("TestEquipmentManager");
            manager = go.AddComponent<EquipmentManager>();

            // Wire the character -> manager reference so lifecycle hooks can reach the wearer.
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

        private readonly List<Object> trackedAssets = new List<Object>();

        private EquipmentData MakeEquip(EquipmentSlotType slot, string name)
        {
            var data = ScriptableObject.CreateInstance<EquipmentData>();
            data.EquipmentName = name;
            data.SlotType = slot;
            return data;
        }

        private static BonusHealthEnchantmentSO MakeHealthEnchantment(int amount)
        {
            var enchantment = ScriptableObject.CreateInstance<BonusHealthEnchantmentSO>();
            enchantment.BonusMaxHealth = amount;
            return enchantment;
        }

        private static BonusDamageEnchantmentSO MakeDamageEnchantment(int amount, bool onlyOwn)
        {
            var enchantment = ScriptableObject.CreateInstance<BonusDamageEnchantmentSO>();
            enchantment.BonusDamage = amount;
            enchantment.OnlyOwnCards = onlyOwn;
            return enchantment;
        }

        [Test]
        public void Equip_FiresOnEquipped_StatBonusApplied()
        {
            var item = MakeEquip(EquipmentSlotType.Trinket, "Vital Charm");
            var enchantment = MakeHealthEnchantment(3);
            item.Enchantments.Add(enchantment);
            trackedAssets.Add(item);
            trackedAssets.Add(enchantment);

            int maxBefore = character.Stats.MaxHealth;
            manager.Equip(item);

            Assert.AreEqual(maxBefore + 3, character.Stats.MaxHealth);
        }

        [Test]
        public void Unequip_FiresOnUnequipped_StatBonusReverted()
        {
            var item = MakeEquip(EquipmentSlotType.Trinket, "Vital Charm");
            var enchantment = MakeHealthEnchantment(3);
            item.Enchantments.Add(enchantment);
            trackedAssets.Add(item);
            trackedAssets.Add(enchantment);
            manager.Equip(item);
            int boostedMax = character.Stats.MaxHealth;

            manager.Unequip(EquipmentSlotType.Trinket, 0);

            Assert.AreEqual(boostedMax - 3, character.Stats.MaxHealth);
        }

        [Test]
        public void ApplyLoadout_ClearAllSlots_RevertsEnchantmentBonuses()
        {
            var item = MakeEquip(EquipmentSlotType.Trinket, "Vital Charm");
            var enchantment = MakeHealthEnchantment(3);
            item.Enchantments.Add(enchantment);
            trackedAssets.Add(item);
            trackedAssets.Add(enchantment);
            manager.Equip(item);
            int maxBefore = character.Stats.MaxHealth - 3;

            manager.ApplyLoadout(null); // clears every slot

            Assert.AreEqual(maxBefore, character.Stats.MaxHealth);
        }

        [Test]
        public void GetTotalCardDamageBonus_SumsAcrossSlots()
        {
            var sharedCard = ScriptableObject.CreateInstance<Cards.CardData>();
            sharedCard.CardName = "Shared Strike";
            trackedAssets.Add(sharedCard);

            var leftHand = MakeEquip(EquipmentSlotType.Hand, "Left Weapon");
            leftHand.Cards = new List<Cards.CardData> { sharedCard };
            var rightHand = MakeEquip(EquipmentSlotType.Hand, "Right Weapon");
            rightHand.Cards = new List<Cards.CardData> { sharedCard };
            var trinket = MakeEquip(EquipmentSlotType.Trinket, "Fury Charm");

            var leftEnchant = MakeDamageEnchantment(1, true);
            var rightEnchant = MakeDamageEnchantment(1, true);
            var trinketEnchant = MakeDamageEnchantment(3, false);
            leftHand.Enchantments.Add(leftEnchant);
            rightHand.Enchantments.Add(rightEnchant);
            trinket.Enchantments.Add(trinketEnchant);
            trackedAssets.AddRange(new Object[] { leftHand, rightHand, trinket, leftEnchant, rightEnchant, trinketEnchant });

            manager.Equip(leftHand, EquipmentSlotType.Hand, 0);
            manager.Equip(rightHand, EquipmentSlotType.Hand, 1);
            manager.Equip(trinket);

            Assert.AreEqual(5, manager.GetTotalCardDamageBonus(sharedCard),
                "Both own-card enchants (1+1) plus the any-card trinket (3) must stack.");

            var foreign = ScriptableObject.CreateInstance<Cards.CardData>();
            foreign.CardName = "Foreign";
            trackedAssets.Add(foreign);
            Assert.AreEqual(3, manager.GetTotalCardDamageBonus(foreign),
                "Only the unscoped trinket enchant applies to foreign cards.");
        }

        [Test]
        public void GetAllCards_IncludesEnchantmentGrantedCards()
        {
            var granted = ScriptableObject.CreateInstance<Cards.CardData>();
            granted.CardName = "Conjured Blade";
            trackedAssets.Add(granted);

            var item = MakeEquip(EquipmentSlotType.Trinket, "Forge Amulet");
            var grant = ScriptableObject.CreateInstance<GrantCardEnchantmentSO>();
            grant.GrantedCard = granted;
            item.Enchantments.Add(grant);
            trackedAssets.Add(item);
            trackedAssets.Add(grant);
            manager.Equip(item);

            List<Cards.CardData> allCards = manager.GetAllCards();
            Assert.Contains(granted, allCards,
                "Deck building must include cards granted by enchantments, not just Data.Cards.");
        }
    }

    [TestFixture]
    public class EnchantmentActionResolverTests
    {
        private static readonly FieldInfo characterEquipmentField =
            typeof(Character).GetField("equipmentManager", BindingFlags.NonPublic | BindingFlags.Instance);

        private ActionResolver resolver;
        private EquipmentManager manager;
        private Character source;
        private Character target;

        [SetUp]
        public void SetUp()
        {
            var resolverGo = new GameObject("Resolver");
            resolver = resolverGo.AddComponent<ActionResolver>();

            source = TestHelper.CreateCharacter(0, 0, 0);
            target = TestHelper.CreateCharacter(1, 1, 0);

            var managerGo = new GameObject("TestEquipmentManager");
            manager = managerGo.AddComponent<EquipmentManager>();
            characterEquipmentField.SetValue(source, manager);
            manager.Initialize(source);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(resolver.gameObject);
            Object.DestroyImmediate(manager.gameObject);
            Object.DestroyImmediate(source.CurrentCell.gameObject);
            Object.DestroyImmediate(source.gameObject);
            Object.DestroyImmediate(target.CurrentCell.gameObject);
            Object.DestroyImmediate(target.gameObject);
            foreach (Object asset in assets)
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }
            assets.Clear();
        }

        private readonly List<Object> assets = new List<Object>();

        private Cards.Effects.AttackEffect MakeAttackEffect(int damage)
        {
            var effect = ScriptableObject.CreateInstance<Cards.Effects.AttackEffect>();
            effect.Damage = Cards.CardValue.MakeConstant(damage);
            assets.Add(effect);
            return effect;
        }

        [Test]
        public void PlayCard_OwnCardWithDamageEnchantment_DealsBoostedDamage()
        {
            var attackEffect = MakeAttackEffect(3);
            var attackCard = ScriptableObject.CreateInstance<Cards.CardData>();
            attackCard.CardName = "Slash";
            attackCard.Steps = new List<Cards.CardEffectStep>
            {
                new Cards.CardEffectStep { Effect = attackEffect, TargetType = Cards.TargetType.EnemyCharacter, Range = 5 }
            };
            assets.Add(attackCard);

            var weapon = ScriptableObject.CreateInstance<EquipmentData>();
            weapon.EquipmentName = "Keen Sword";
            weapon.SlotType = EquipmentSlotType.Hand;
            weapon.Cards = new List<Cards.CardData> { attackCard };
            var enchantment = ScriptableObject.CreateInstance<BonusDamageEnchantmentSO>();
            enchantment.BonusDamage = 2;
            enchantment.OnlyOwnCards = true;
            weapon.Enchantments.Add(enchantment);
            assets.AddRange(new Object[] { weapon, enchantment });

            manager.Equip(weapon, EquipmentSlotType.Hand, 0);

            source.Hand.AddCard(new Cards.CardInstance(attackCard));
            bool played = resolver.PlayCard(source, source.Hand.Cards[0], targetCharacter: target);

            Assert.IsTrue(played);
            Assert.AreEqual(10 - 5, target.Stats.CurrentHealth,
                "Base damage 3 plus the own-card enchantment bonus of 2 should deal 5 total.");
        }

        [Test]
        public void PlayCard_UnscopedEnchantment_BoostsForeignCardToo()
        {
            var attackEffect = MakeAttackEffect(3);
            var plainCard = ScriptableObject.CreateInstance<Cards.CardData>();
            plainCard.CardName = "Plain Bolt";
            plainCard.Steps = new List<Cards.CardEffectStep>
            {
                new Cards.CardEffectStep { Effect = attackEffect, TargetType = Cards.TargetType.EnemyCharacter, Range = 5 }
            };
            assets.Add(plainCard);

            var trinket = ScriptableObject.CreateInstance<EquipmentData>();
            trinket.EquipmentName = "Fury Charm";
            trinket.SlotType = EquipmentSlotType.Trinket;
            var enchantment = ScriptableObject.CreateInstance<BonusDamageEnchantmentSO>();
            enchantment.BonusDamage = 1;
            enchantment.OnlyOwnCards = false;
            trinket.Enchantments.Add(enchantment);
            assets.AddRange(new Object[] { trinket, enchantment });

            manager.Equip(trinket);

            source.Hand.AddCard(new Cards.CardInstance(plainCard));
            bool played = resolver.PlayCard(source, source.Hand.Cards[0], targetCharacter: target);

            Assert.IsTrue(played);
            Assert.AreEqual(10 - 4, target.Stats.CurrentHealth);
        }
    }
}
