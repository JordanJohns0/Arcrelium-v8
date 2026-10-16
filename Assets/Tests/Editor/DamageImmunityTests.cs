using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Characters;
using Equipment;
using HexGrid;
using TileEffects;

namespace Tests.Damage
{
    /// <summary>
    /// Covers the DamageImmunity enchantment at the Character.TakeDamage seam: typed damage
    /// is negated before block or health are touched, and the immunity mask is aggregated
    /// across every equipped slot.
    /// </summary>
    [TestFixture]
    public class DamageImmunityEnchantmentTests
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

        private DamageImmunityEnchantmentSO MakeImmunity(DamageType mask)
        {
            var enchantment = ScriptableObject.CreateInstance<DamageImmunityEnchantmentSO>();
            enchantment.ImmuneTo = mask;
            trackedAssets.Add(enchantment);
            return enchantment;
        }

        private void EquipImmunity(DamageType mask)
        {
            var item = MakeEquip(EquipmentSlotType.Trinket, "Asbestos Charm");
            item.Enchantments.Add(MakeImmunity(mask));
            Assert.IsTrue(manager.Equip(item), "Immunity trinket should equip.");
        }

        // ------------------------------------------------------------- mask logic

        [Test]
        public void DefaultMask_CoversFireAndExplosion()
        {
            var enchantment = MakeImmunity(DamageType.Fire | DamageType.Explosion);
            Assert.IsTrue(enchantment.IsImmuneTo(DamageType.Fire));
            Assert.IsTrue(enchantment.IsImmuneTo(DamageType.Explosion));
        }

        [Test]
        public void Mask_OnlyCoversConfiguredTypes()
        {
            var enchantment = MakeImmunity(DamageType.Fire);
            Assert.IsTrue(enchantment.IsImmuneTo(DamageType.Fire));
            Assert.IsFalse(enchantment.IsImmuneTo(DamageType.Explosion));
            Assert.IsFalse(enchantment.IsImmuneTo(DamageType.Physical));
        }

        [Test]
        public void NoneIsNeverBlocked_EvenWithBroadMask()
        {
            var enchantment = MakeImmunity(DamageType.Fire | DamageType.Explosion | DamageType.Physical);
            Assert.IsFalse(enchantment.IsImmuneTo(DamageType.None));
        }

        // ------------------------------------------------------------- manager query

        [Test]
        public void IsImmuneTo_AggregatesAcrossSlots()
        {
            var item = MakeEquip(EquipmentSlotType.Hand, "Fire Ward");
            item.Enchantments.Add(MakeImmunity(DamageType.Fire));
            Assert.IsTrue(manager.Equip(item, EquipmentSlotType.Hand, 0));

            Assert.IsTrue(manager.IsImmuneTo(DamageType.Fire));
            Assert.IsFalse(manager.IsImmuneTo(DamageType.Explosion));
        }

        [Test]
        public void IsImmuneTo_DropsWhenEnchantmentUnequipped()
        {
            EquipImmunity(DamageType.Fire);
            Assert.IsTrue(manager.IsImmuneTo(DamageType.Fire));

            manager.Unequip(EquipmentSlotType.Trinket, 0);
            Assert.IsFalse(manager.IsImmuneTo(DamageType.Fire));
        }

        // ------------------------------------------------------------- TakeDamage

        [Test]
        public void ImmuneCharacter_TakesNoFireDamage()
        {
            EquipImmunity(DamageType.Fire | DamageType.Explosion);
            int before = character.Stats.CurrentHealth;

            character.TakeDamage(5, type: DamageType.Fire);

            Assert.AreEqual(before, character.Stats.CurrentHealth, "Fire damage should be fully negated.");
        }

        [Test]
        public void ImmuneCharacter_TakesNoExplosionDamage()
        {
            EquipImmunity(DamageType.Fire | DamageType.Explosion);
            int before = character.Stats.CurrentHealth;

            character.TakeDamage(5, type: DamageType.Explosion);

            Assert.AreEqual(before, character.Stats.CurrentHealth, "Explosion damage should be fully negated.");
        }

        [Test]
        public void NonImmuneCharacter_TakesFireDamage()
        {
            EquipmentData item = MakeEquip(EquipmentSlotType.Trinket, "Plain Charm");
            item.Enchantments.Add(MakeImmunity(DamageType.Physical));
            Assert.IsTrue(manager.Equip(item));

            int before = character.Stats.CurrentHealth;
            character.TakeDamage(5, type: DamageType.Fire);

            Assert.AreEqual(before - 5, character.Stats.CurrentHealth, "Uncovered damage should still land.");
        }

        [Test]
        public void UnspecifiedDamage_IsNotBlockedByImmunityMask()
        {
            EquipImmunity(DamageType.Fire | DamageType.Explosion | DamageType.Physical);
            int before = character.Stats.CurrentHealth;

            character.TakeDamage(5);

            Assert.AreEqual(before - 5, character.Stats.CurrentHealth, "Untyped damage should land.");
        }

        [Test]
        public void ImmuneCharacter_DoesNotSpendBlock()
        {
            EquipImmunity(DamageType.Fire | DamageType.Explosion);
            character.Stats.GainBlock(4);

            character.TakeDamage(3, type: DamageType.Fire);

            Assert.AreEqual(4, character.Stats.Block, "Immune damage must not consume block.");
        }

        [Test]
        public void ImmuneCharacter_DoesNotFireOnDamageTaken()
        {
            EquipImmunity(DamageType.Fire | DamageType.Explosion);
            bool fired = false;
            character.OnDamageTaken += _ => fired = true;

            character.TakeDamage(3, type: DamageType.Fire);

            Assert.IsFalse(fired, "Negated damage should not raise OnDamageTaken.");
        }
    }

    /// <summary>
    /// End-to-end: fire tiles and the fire-ignites-oil explosion both route their damage
    /// through the typed seam, so an immune wearer is untouched by either.
    /// </summary>
    [TestFixture]
    public class FireExplosionTileImmunityTests
    {
        private static readonly FieldInfo characterEquipmentField =
            typeof(Character).GetField("equipmentManager", BindingFlags.NonPublic | BindingFlags.Instance);

        private HexGridManager grid;
        private TileEffectSystem system;
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<Object> trackedAssets = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            grid = TestHelper.CreateTestGrid(3);
            objects.Add(grid.gameObject);

            var systemGo = new GameObject("TileEffectSystem");
            objects.Add(systemGo);
            system = systemGo.AddComponent<TileEffectSystem>();
            system.InitializeForTests(grid, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (system != null) system.Shutdown();
            foreach (GameObject go in objects)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            foreach (Character character in Object.FindObjectsByType<Character>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(character.gameObject);
            }
            foreach (Object asset in trackedAssets)
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }
            objects.Clear();
            trackedAssets.Clear();
        }

        private HexCell Cell(int q, int r) => grid.GetCell(new HexCoordinates(q, r));

        private FireTileEffectSO Fire(int fireDamage = 2, int explosionDamage = 5)
        {
            var fire = ScriptableObject.CreateInstance<FireTileEffectSO>();
            fire.EffectName = "Fire";
            fire.Duration = 2;
            fire.FireDamage = fireDamage;
            fire.ExplosionDamage = explosionDamage;
            fire.ExplosionRadius = 1;
            trackedAssets.Add(fire);
            return fire;
        }

        private OilTileEffectSO Oil()
        {
            var oil = ScriptableObject.CreateInstance<OilTileEffectSO>();
            oil.EffectName = "Oil";
            oil.Duration = 0;
            oil.Slippery = true;
            trackedAssets.Add(oil);
            return oil;
        }

        /// <summary>Places a character on the grid and gives them a working equipment manager.</summary>
        private Character At(int q, int r, DamageType? immunity)
        {
            Character character = TestHelper.CreateCharacterOnGrid(grid, 0, q, r);
            objects.Add(character.gameObject);

            if (immunity.HasValue)
            {
                var go = new GameObject("TestEquipmentManager");
                objects.Add(go);
                var manager = go.AddComponent<EquipmentManager>();
                characterEquipmentField.SetValue(character, manager);
                manager.Initialize(character);

                var item = ScriptableObject.CreateInstance<EquipmentData>();
                item.EquipmentName = "Asbestos Charm";
                item.SlotType = EquipmentSlotType.Trinket;
                trackedAssets.Add(item);

                var enchantment = ScriptableObject.CreateInstance<DamageImmunityEnchantmentSO>();
                enchantment.ImmuneTo = immunity.Value;
                trackedAssets.Add(enchantment);

                item.Enchantments.Add(enchantment);
                Assert.IsTrue(manager.Equip(item), "Immunity trinket should equip.");
            }

            return character;
        }

        [Test]
        public void ImmuneCharacter_SteppingIntoFire_TakesNoDamage()
        {
            Character hero = At(1, 0, DamageType.Fire | DamageType.Explosion);
            int before = hero.Stats.CurrentHealth;

            system.PlaceEffect(Cell(2, 0), Fire());
            system.WalkInto(hero, Cell(2, 0));

            Assert.AreEqual(before, hero.Stats.CurrentHealth, "Immune character should walk through fire unharmed.");
        }

        [Test]
        public void NonImmuneCharacter_SteppingIntoFire_TakesDamage()
        {
            Character hero = At(1, 0, null);
            int before = hero.Stats.CurrentHealth;

            system.PlaceEffect(Cell(2, 0), Fire(fireDamage: 2));
            system.WalkInto(hero, Cell(2, 0));

            Assert.AreEqual(before - 2, hero.Stats.CurrentHealth, "Control character should burn on entry.");
        }

        [Test]
        public void ImmuneCharacter_FireAppears_AndTurnStarts_TakesNoDamage()
        {
            Character hero = At(1, 0, DamageType.Fire | DamageType.Explosion);
            int before = hero.Stats.CurrentHealth;

            system.PlaceEffect(hero.CurrentCell, Fire(fireDamage: 2));
            Assert.AreEqual(before, hero.Stats.CurrentHealth, "Immune to fire appearing on the tile.");

            system.HandleTurnStarted(hero, false);
            Assert.AreEqual(before, hero.Stats.CurrentHealth, "Immune to fire at the start of a turn.");
        }

        [Test]
        public void ImmuneCharacter_FireOnOilExplosion_TakesNoDamage()
        {
            Character victim = At(1, 0, DamageType.Fire | DamageType.Explosion);
            int before = victim.Stats.CurrentHealth;

            system.PlaceEffect(victim.CurrentCell, Oil());
            system.PlaceEffect(victim.CurrentCell, Fire(fireDamage: 2, explosionDamage: 5));

            Assert.AreEqual(before, victim.Stats.CurrentHealth, "Immune character should survive the explosion.");
        }

        [Test]
        public void NonImmuneCharacter_FireOnOilExplosion_TakesDamageAndConsumesOil()
        {
            Character victim = At(1, 0, null);
            int before = victim.Stats.CurrentHealth;

            system.PlaceEffect(victim.CurrentCell, Oil());
            system.PlaceEffect(victim.CurrentCell, Fire(fireDamage: 2, explosionDamage: 5));

            Assert.AreEqual(before - 5, victim.Stats.CurrentHealth, "Control character should take explosion damage.");
            Assert.IsFalse(victim.CurrentCell.HasEffect(effect => effect.Slippery), "Oil is consumed by the blast.");
        }

        [Test]
        public void FireImmunityAlone_DoesNotStopExplosion()
        {
            Character victim = At(1, 0, DamageType.Fire);
            int before = victim.Stats.CurrentHealth;

            system.PlaceEffect(victim.CurrentCell, Oil());
            system.PlaceEffect(victim.CurrentCell, Fire(fireDamage: 2, explosionDamage: 5));

            Assert.AreEqual(before - 5, victim.Stats.CurrentHealth,
                "Fire immunity must not cover the separate explosion type.");
        }
    }
}
