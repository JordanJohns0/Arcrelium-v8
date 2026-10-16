using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Characters;
using Cards;
using Equipment;
using Equipment.Effects;
using HexGrid;

// Cards.Effects also defines a HealEffect, so alias the one card effect this file needs
// instead of importing the whole namespace.
using AttackEffect = Cards.Effects.AttackEffect;

namespace Tests.Equipment
{
    /// <summary>
    /// Per-asset battery over every EquipmentData in the project. Each asset must match its
    /// intended spec: the declared slot, the exact cards it should grant the owner's deck,
    /// and bound triggers whose timing/effect/parameters match the design. Trigger behaviour
    /// is then exercised end-to-end through EquipmentManager.
    /// </summary>
    [TestFixture]
    public class EquipmentAssetTests
    {
        private static readonly System.Reflection.FieldInfo managerGridField =
            typeof(EquipmentManager).GetField("grid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        private sealed class Spec
        {
            public string Name;
            public EquipmentSlotType Slot;
            public string[] Cards;
            public (TriggerTiming Timing, Type EffectType)[] Triggers;
        }

        private static readonly Spec[] Specs =
        {
            new Spec
            {
                Name = "Barbed Shoes",
                Slot = EquipmentSlotType.Feet,
                Cards = new[] { "Walk", "Walk", "Painful Sprint" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Boots of Swiftness",
                Slot = EquipmentSlotType.Feet,
                Cards = new[] { "Run", "Run", "Sprint" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Crown of Inspiration",
                Slot = EquipmentSlotType.Head,
                Cards = new[] { "Inspire" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Excalibur",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Excalibur's Justice", "Slash", "Slash" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Lucky Opal",
                Slot = EquipmentSlotType.Trinket,
                Cards = new[] { "Restore" },
                Triggers = new[] { (TriggerTiming.StartOfTurn, typeof(HealEffect)) }
            },
            new Spec
            {
                Name = "Metal Fist",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Brace", "Bonk" },
                Triggers = new[] { (TriggerTiming.OnDamageTaken, typeof(AOEDamageEffect)) }
            },
            new Spec
            {
                Name = "Snake Oil Flask",
                Slot = EquipmentSlotType.Trinket,
                Cards = Array.Empty<string>(),
                Triggers = new[] { (TriggerTiming.OnDealDamage, typeof(ShuffleCardIntoDeckEffect)) }
            },
            new Spec
            {
                Name = "Null Amulet",
                Slot = EquipmentSlotType.Trinket,
                Cards = Array.Empty<string>(),
                Triggers = new[] { (TriggerTiming.OnMovedToCell, typeof(MoveStepDamageEffect)) }
            },
            new Spec
            {
                Name = "Spiked Sword",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Slash", "Painful Slash", "Painful Slash" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Staff of Fire",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Fireball", "Wall of Flames", "Flame Bolt" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Staff of Stone",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Mould Earth" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Bottle of Grease",
                Slot = EquipmentSlotType.Trinket,
                Cards = new[] { "Grease" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Staff of Healing",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Bonk", "Restore", "Restore" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Sword of Truth",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Slash", "Slash", "Inspire" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Tower Shield",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Bulwark", "Fortify", "Fortify", "Shield Bash", "Defend" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Wanderer's Token",
                Slot = EquipmentSlotType.Trinket,
                Cards = new[] { "Walk" },
                Triggers = new[] { (TriggerTiming.EndOfTurn, typeof(AutoMoveEffect)) }
            },
            new Spec
            {
                Name = "Goblin Shoes",
                Slot = EquipmentSlotType.Feet,
                Cards = new[] { "Walk", "Walk", "Walk" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Goblin Sword",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Poke", "Poke" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Kobold Shoes",
                Slot = EquipmentSlotType.Feet,
                Cards = new[] { "Walk", "Walk", "Walk", "Walk" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Kobold Sling",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Shoot Pebble", "Shoot Pebble", "Shoot Pebble" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Kobold Stick",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Poke", "Poke" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Orc Club",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Bonk", "Bonk" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Orc Shoes",
                Slot = EquipmentSlotType.Feet,
                Cards = new[] { "Heavy Footsteps", "Heavy Footsteps", "Heavy Footsteps" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Leather Goblin Armour",
                Slot = EquipmentSlotType.Chest,
                Cards = new[] { "Bad Brace", "Bad Brace" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Leather Armour",
                Slot = EquipmentSlotType.Chest, // Starter_Chest
                Cards = new[] { "Brace", "Brace", "Reinforce" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Leather Boots",
                Slot = EquipmentSlotType.Feet,   // Starter_Feet
                Cards = new[] { "Walk", "Walk", "Jog" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Rusty Sword",
                Slot = EquipmentSlotType.Hand,     // Starter_Sword
                Cards = new[] { "Slash", "Slash", "Slash" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Iron Broadsword",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Heavy Slash", "Slash", "Parry" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Iron Cleaver",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Heavy Slash", "Heavy Slash", "Heavy Slash" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Iron Greatsword",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Heavy Slash", "Slash", "Slash" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Iron Khopesh",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Slash", "Slash", "Disarming Slash" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Iron Shortsword",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Slash" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Fingerless Glove",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Bonk" },
                Triggers = new[] { (TriggerTiming.StartOfTurn, typeof(GrantBlockEffect)) }
            },
            new Spec
            {
                Name = "Restorative Shield",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Fortify", "Reinforce", "Reinforce" },
                Triggers = new[] { (TriggerTiming.OnGainBlock, typeof(HealEffect)) }
            },
            new Spec
            {
                Name = "Terrifying Mask",
                Slot = EquipmentSlotType.Head,
                Cards = Array.Empty<string>(),
                Triggers = new[] { (TriggerTiming.OnEnemyTurnEnded, typeof(PushAwayEnemyEffect)) }
            },
            new Spec
            {
                Name = "Backpack",
                Slot = EquipmentSlotType.Trinket,
                Cards = new[] { "Prepare", "Sleight of Hand" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Cursed Ring",
                Slot = EquipmentSlotType.Trinket,
                Cards = new[] { "Transmute", "Transmute" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Mystic Scroll",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Mystic Bolt" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Soul-Sight Magnifying Glass",
                Slot = EquipmentSlotType.Trinket,
                Cards = new[] { "Sense Evil" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Tome of Knowledge",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Papercut" },
                Triggers = new[] { (TriggerTiming.StartOfTurn, typeof(DrawCardsEffect)) }
            },
            new Spec
            {
                Name = "Steel Net",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Entrap" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Thieve's Tools",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Pickpocket", "Prepare" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Boots of Flame",
                Slot = EquipmentSlotType.Feet,
                Cards = new[] { "Walk", "Walk", "Walk" },
                Triggers = new[] { (TriggerTiming.OnLeftCell, typeof(PlaceFireTileEffect)) }
            },
            new Spec
            {
                Name = "Ring of Clarity",
                Slot = EquipmentSlotType.Trinket,
                Cards = new[] { "Clarity" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Ring of Foresight",
                Slot = EquipmentSlotType.Trinket,
                Cards = new[] { "Foretell" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Snake Oil Vial",
                Slot = EquipmentSlotType.Trinket,
                Cards = new[] { "Snake Oil" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Golem Skin",
                Slot = EquipmentSlotType.Chest,
                Cards = new[] { "Fortify", "Fortify" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Golem Head",
                Slot = EquipmentSlotType.Head,
                Cards = new[] { "Wall of Flames" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Golem Fist",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Crush" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Golem Feet",
                Slot = EquipmentSlotType.Feet,
                Cards = new[] { "Heavy Footsteps" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Fire Elemental Hand",
                Slot = EquipmentSlotType.Hand,
                Cards = new[] { "Throw Flames", "Throw Flames" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Fire Elemental Feet",
                Slot = EquipmentSlotType.Feet,
                Cards = new[] { "Walk", "Walk" },
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
            new Spec
            {
                Name = "Flame Elemental Essence",
                Slot = EquipmentSlotType.Trinket,
                Cards = Array.Empty<string>(),
                Triggers = Array.Empty<(TriggerTiming, Type)>()
            },
        };

        // Timings EquipmentManager actually fires during gameplay (see EquipmentManager.Initialize
        // and its handlers: StartOfTurn, EndOfTurn, OnBeforeCardPlay, OnMovedToCell, OnDamageTaken,
        // OnDealDamage, OnGainBlock, OnEnemyTurnEnded).
        private static readonly HashSet<TriggerTiming> WiredTimings = new HashSet<TriggerTiming>
        {
            TriggerTiming.StartOfTurn,
            TriggerTiming.EndOfTurn,
            TriggerTiming.OnBeforeCardPlay,
            TriggerTiming.OnMovedToCell,
            TriggerTiming.OnDamageTaken,
            TriggerTiming.OnDealDamage,
            TriggerTiming.OnGainBlock,
            TriggerTiming.OnEnemyTurnEnded,
            TriggerTiming.OnLeftCell,
            TriggerTiming.OnShuffle,
            TriggerTiming.StartOfCombat
        };

        private static IEnumerable<EquipmentData> AllEquipmentAssets()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:EquipmentData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var equipment = AssetDatabase.LoadAssetAtPath<EquipmentData>(path);
                if (equipment != null) yield return equipment;
            }
        }

        private static EquipmentData LoadAsset(string equipmentName)
        {
            foreach (var equipment in AllEquipmentAssets())
            {
                if (equipment.EquipmentName == equipmentName) return equipment;
            }
            Assert.Fail($"Equipment '{equipmentName}' not found in the project.");
            return null;
        }

        private static Spec FindSpec(string equipmentName)
        {
            foreach (var spec in Specs)
            {
                if (spec.Name == equipmentName) return spec;
            }
            return null;
        }

        private static bool SameCardNames(List<CardData> cards, string[] expected)
        {
            if (cards == null || cards.Count != expected.Length) return false;
            for (int i = 0; i < expected.Length; i++)
            {
                if (cards[i] == null || cards[i].CardName != expected[i]) return false;
            }
            return true;
        }

        /// <summary>
        /// Every equipment asset in the project must match its intended spec exactly: slot type,
        /// deck cards (name + order), and trigger timings/effect types. Any drift from the design
        /// (wrong slot, missing/extra/misordered cards, re-bound trigger) is flagged here.
        /// </summary>
        [Test]
        public void EveryEquipmentAsset_MatchesItsSpec()
        {
            var problems = new List<string>();
            foreach (var equipment in AllEquipmentAssets())
            {
                if (equipment == null)
                {
                    problems.Add("A null (broken) EquipmentData asset exists.");
                    continue;
                }
                Spec spec = FindSpec(equipment.EquipmentName);
                if (spec == null)
                {
                    problems.Add($"'{equipment.EquipmentName}' has no Spec row — add the intended design to Specs.");
                    continue;
                }
                if (equipment.SlotType != spec.Slot)
                {
                    problems.Add($"'{equipment.EquipmentName}': slot is {equipment.SlotType}, spec says {spec.Slot}.");
                }
                if (!SameCardNames(equipment.Cards, spec.Cards))
                {
                    string actual = equipment.Cards == null
                        ? "<null>"
                        : string.Join(", ", equipment.Cards.Select(c => c?.CardName ?? "<null>"));
                    problems.Add($"'{equipment.EquipmentName}': deck cards are [{actual}], spec expects [{string.Join(", ", spec.Cards)}].");
                }
                var actualTriggers = equipment.Triggers == null
                    ? new List<(TriggerTiming, Type)>()
                    : equipment.Triggers
                        .Where(t => t != null)
                        .Select(t => (t.Timing, t.Effect == null ? typeof(object) : t.Effect.GetType()))
                        .ToList();
                if (actualTriggers.Count != spec.Triggers.Length)
                {
                    problems.Add($"'{equipment.EquipmentName}': has {actualTriggers.Count} bound triggers, spec expects {spec.Triggers.Length}.");
                }
                else
                {
                    for (int i = 0; i < spec.Triggers.Length; i++)
                    {
                        if (actualTriggers[i] != spec.Triggers[i])
                        {
                            problems.Add($"'{equipment.EquipmentName}': trigger {i} is {actualTriggers[i]}, spec expects {spec.Triggers[i]}.");
                        }
                    }
                }
            }
            Assert.IsEmpty(problems, "Equipment assets that drift from their intended spec:\n" + string.Join("\n", problems));
        }

        /// <summary>
        /// Mirrors EquipmentManager.GetAllCards (Character.cs builds the deck from it): every
        /// card the player can end up holding SHOULD be reachable through at least one
        /// equipment's granted-cards list (the Cards field) OR through one of its enchantments'
        /// CollectGrantedCards. This is informational only — it is not an error for a card to
        /// have no granting equipment (it may be enemy-only, a future reward, etc.) — so any
        /// uncovered cards are logged for design review instead of failing the suite. Cards are
        /// matched by asset identity, not display name, so coverage only counts when the exact
        /// CardData an equipment grants is the same object.
        /// </summary>
        [Test]
        public void EveryCard_GrantableBySomeEquipment()
        {
            var granted = new HashSet<CardData>();
            foreach (var equipment in AllEquipmentAssets())
            {
                if (equipment.Cards != null)
                {
                    foreach (CardData card in equipment.Cards)
                    {
                        if (card != null) granted.Add(card);
                    }
                }

                if (equipment.Enchantments == null) continue;
                foreach (EquipmentEnchantmentSO enchantment in equipment.Enchantments)
                {
                    if (enchantment == null) continue;
                    var grantedByEnchantment = new List<CardData>();
                    enchantment.CollectGrantedCards(grantedByEnchantment);
                    foreach (CardData card in grantedByEnchantment)
                    {
                        if (card != null) granted.Add(card);
                    }
                }
            }

            var unobtainable = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:CardData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardData>(path);
                if (card == null)
                {
                    unobtainable.Add($"<asset fails to load at {path}>");
                    continue;
                }
                if (!granted.Contains(card)) unobtainable.Add(card.CardName);
            }

            if (unobtainable.Count > 0)
            {
                Debug.Log("No equipment grants these cards (informational — not a failure): "
                    + string.Join(", ", unobtainable));
            }
        }

        /// <summary>
        /// Every trigger effect asset used by equipment must be present and use the designed
        /// numeric parameters. Guards against, e.g., Heal_1 silently healing 5.
        /// </summary>
        [Test]
        public void TriggerEffects_UseDesignedParameters()
        {
            AssertTriggerEffect<HealEffect>("Lucky Opal", e => e.Amount == 1, "should heal exactly 1");
            AssertTriggerEffect<AOEDamageEffect>("Metal Fist", e => e.Damage == 1 && e.Range == 1, "should deal 1 damage in range 1");
            AssertTriggerEffect<MoveStepDamageEffect>("Null Amulet", e => e.Damage == 1, "should deal exactly 1 damage");
            AssertTriggerEffect<AutoMoveEffect>("Wanderer's Token", e => e.Range == 1, "should move exactly 1 cell");
            AssertTriggerEffect<PlaceFireTileEffect>("Boots of Flame", e => e.FireDefinition != null, "should reference a fire tile effect asset");
        }

        private static void AssertTriggerEffect<T>(string equipmentName, Func<T, bool> predicate, string message) where T : EquipmentEffect
        {
            var equipment = LoadAsset(equipmentName);
            Assert.IsNotNull(equipment.Triggers, $"{equipmentName} should have a trigger list.");
            Assert.AreEqual(1, equipment.Triggers.Count, $"{equipmentName} should have exactly one trigger.");
            var effect = equipment.Triggers[0].Effect;
            Assert.IsNotNull(effect, $"{equipmentName} trigger effect is unbound (missing script?).");
            Assert.IsInstanceOf<T>(effect, $"{equipmentName} trigger should be {typeof(T).Name}, was {effect.GetType().Name}.");
            Assert.IsTrue(predicate((T)effect), $"{equipmentName} trigger {message}.");
        }

        /// <summary>
        /// No equipment may exist purely as an empty shell: every item must grant the owner
        /// cards, a trigger effect, or at least one built-in enchantment, otherwise equipping
        /// it does nothing.
        /// </summary>
        [Test]
        public void NoEquipment_IsAnEmptyShell()
        {
            var empties = new List<string>();
            foreach (var equipment in AllEquipmentAssets())
            {
                bool hasCards = equipment.Cards != null && equipment.Cards.Count > 0;
                bool hasTriggers = equipment.Triggers != null && equipment.Triggers.Count > 0;
                bool hasEnchantments = equipment.Enchantments != null && equipment.Enchantments.Count > 0;
                if (!hasCards && !hasTriggers && !hasEnchantments)
                {
                    empties.Add($"'{equipment.EquipmentName}'");
                }
            }
            Assert.IsEmpty(empties, "Equipment that grant nothing when equipped: " + string.Join(", ", empties));
        }

        /// <summary>
        /// Every trigger timing referenced by an equipment asset must actually have a firing
        /// path in EquipmentManager. A configured trigger that the game never fires is dead.
        /// </summary>
        [Test]
        public void EveryUsedTriggerTiming_HasAWiredFiringPath()
        {
            var used = new HashSet<TriggerTiming>();
            foreach (var equipment in AllEquipmentAssets())
            {
                if (equipment.Triggers == null) continue;
                foreach (var trigger in equipment.Triggers)
                {
                    if (trigger != null) used.Add(trigger.Timing);
                }
            }
            var unwired = new List<string>();
            foreach (var timing in used)
            {
                if (!WiredTimings.Contains(timing))
                    unwired.Add($"{timing} (used by equipment in the project)");
            }
            Assert.IsEmpty(unwired, "Trigger timings the game never fires: " + string.Join(", ", unwired));
        }

        // ---------- Trigger behaviour, end-to-end through EquipmentManager ----------

        [Test]
        public void LuckyOpal_StartOfTurn_HealsOne()
        {
            var grid = TestHelper.CreateTestGrid(3);
            var source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            var manager = BuildManager(grid, source);
            try
            {
                Assert.IsTrue(manager.Equip(LoadAsset("Lucky Opal")));
                source.Stats.ApplyDamage(3); // 10 -> 7
                manager.FireTriggers(TriggerTiming.StartOfTurn);
                Assert.AreEqual(8, source.Stats.CurrentHealth, "Lucky Opal should heal 1 at the start of the owner's turn.");
                manager.FireTriggers(TriggerTiming.StartOfTurn); // 8 -> 9
                manager.FireTriggers(TriggerTiming.StartOfTurn); // 9 -> 10 (capped)
                Assert.AreEqual(10, source.Stats.CurrentHealth, "Heal must not exceed max health.");
            }
            finally
            {
                DestroyAll(grid, source, manager);
            }
        }

        [Test]
        public void WanderersToken_EndOfTurn_MovesToAdjacentCell()
        {
            var grid = TestHelper.CreateTestGrid(3);
            var source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            var manager = BuildManager(grid, source);
            try
            {
                Assert.IsTrue(manager.Equip(LoadAsset("Wanderer's Token")));
                HexCoordinates origin = source.CurrentCell.Coordinates;
                manager.FireTriggers(TriggerTiming.EndOfTurn);
                HexCoordinates moved = source.CurrentCell.Coordinates;
                Assert.AreNotEqual(origin, moved, "Wanderer's Token should move the owner at end of turn.");
                Assert.AreEqual(1, HexDistance(origin, moved), "Wanderer's Token should move to an adjacent cell.");
            }
            finally
            {
                DestroyAll(grid, source, manager);
            }
        }

        [Test]
        public void NullAmulet_OnMovedToCell_DealsOneToAdjacentEnemiesOnly()
        {
            var grid = TestHelper.CreateTestGrid(3);
            var source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            var manager = BuildManager(grid, source);
            try
            {
                Assert.IsTrue(manager.Equip(LoadAsset("Null Amulet")));
                Character adjacentEnemy = TestHelper.CreateCharacterOnGrid(grid, 1, 1, 0); // neighbour of (1,-1)
                Character farEnemy = TestHelper.CreateCharacterOnGrid(grid, 1, 2, 2);
                Character adjacentAlly = TestHelper.CreateCharacterOnGrid(grid, 0, 2, -1); // neighbour of (1,-1)
                HexCell destination = grid.GetCell(new HexCoordinates(1, -1));
                source.SetCellOnly(destination);

                Assert.AreEqual(9, adjacentEnemy.Stats.CurrentHealth, "Adjacent enemy should take 1 damage on move.");
                Assert.AreEqual(10, farEnemy.Stats.CurrentHealth, "Distant enemy must be untouched.");
                Assert.AreEqual(10, adjacentAlly.Stats.CurrentHealth, "Allies must not be damaged.");
                Assert.AreEqual(10, source.Stats.CurrentHealth, "Owner must not damage itself.");

                UnityEngine.Object.DestroyImmediate(adjacentEnemy.gameObject);
                UnityEngine.Object.DestroyImmediate(farEnemy.gameObject);
                UnityEngine.Object.DestroyImmediate(adjacentAlly.gameObject);
            }
            finally
            {
                DestroyAll(grid, source, manager);
            }
        }

        [Test]
        public void NullAmulet_MultiStepMove_DealsDamagePerStep()
        {
            var grid = TestHelper.CreateTestGrid(3);
            var source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            var manager = BuildManager(grid, source);
            try
            {
                Assert.IsTrue(manager.Equip(LoadAsset("Null Amulet")));

                // (2,-1) is adjacent to both (1,0) and (1,-1)
                Character enemy = TestHelper.CreateCharacterOnGrid(grid, 1, 2, -1);
                Character farEnemy = TestHelper.CreateCharacterOnGrid(grid, 1, 2, 2);

                // Step through two cells; enemy is adjacent to both, so it should take 2 damage
                source.SetCellOnly(grid.GetCell(new HexCoordinates(1, 0)));
                source.SetCellOnly(grid.GetCell(new HexCoordinates(1, -1)));

                Assert.AreEqual(8, enemy.Stats.CurrentHealth,
                    "Enemy adjacent to both intermediate steps should take 1 damage per step.");
                Assert.AreEqual(10, farEnemy.Stats.CurrentHealth, "Distant enemy must be untouched.");

                UnityEngine.Object.DestroyImmediate(enemy.gameObject);
                UnityEngine.Object.DestroyImmediate(farEnemy.gameObject);
            }
            finally
            {
                DestroyAll(grid, source, manager);
            }
        }

        /// <summary>
        /// Metal Fist's trigger is OnDamageTaken -> deal 1 to adjacent enemies. This test drives
        /// the real damage path (Character.TakeDamage) and expects the AOE to fire on each hit.
        /// </summary>
        [Test]
        public void MetalFist_OnDamageTaken_DealsOneToAdjacentEnemies()
        {
            var grid = TestHelper.CreateTestGrid(3);
            var source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            var manager = BuildManager(grid, source);
            try
            {
                Assert.IsTrue(manager.Equip(LoadAsset("Metal Fist")));

                Character enemy = TestHelper.CreateCharacterOnGrid(grid, 1, 1, 0); // adjacent to source
                source.TakeDamage(1);

                Assert.AreEqual(9, enemy.Stats.CurrentHealth,
                    "Metal Fist should deal 1 damage to adjacent enemies when the owner takes damage.");

                source.TakeDamage(1); // triggers again per hit
                Assert.AreEqual(8, enemy.Stats.CurrentHealth,
                    "Metal Fist should deal 1 damage again for each subsequent hit.");

                UnityEngine.Object.DestroyImmediate(enemy.gameObject);
            }
            finally
            {
                DestroyAll(grid, source, manager);
            }
        }

        /// <summary>
        /// Metal Fist's trigger is flagged OnlyOnWearersTurn: damage taken during someone
        /// else's turn must NOT trigger the AOE; damage taken on the wearer's own turn still does.
        /// </summary>
        [Test]
        public void MetalFist_OnlyOnWearersTurn_SuppressedOnOtherTurns()
        {
            var grid = TestHelper.CreateTestGrid(3);
            var source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            var turnGo = new GameObject("TestTurnManager");
            var turnManager = turnGo.AddComponent<global::Game.TurnManager>();
            var manager = BuildManager(grid, source); // Initialize finds the TurnManager
            try
            {
                EquipmentData fist = LoadAsset("Metal Fist");
                Assert.IsTrue(fist.Triggers[0].OnlyOnWearersTurn,
                    "Metal Fist asset must flag its OnDamageTaken trigger as wearer-turn-only.");
                Assert.IsTrue(manager.Equip(fist));

                Character enemy = TestHelper.CreateCharacterOnGrid(grid, 1, 1, 0); // adjacent to source
                turnManager.SetTurnOrder(new List<Character> { enemy, source });

                // Enemy's turn: taking damage must not retaliate.
                turnManager.SetTurn(0);
                source.TakeDamage(1);
                Assert.AreEqual(10, enemy.Stats.CurrentHealth,
                    "Metal Fist must not fire when damage is taken on another character's turn.");

                // Wearer's own turn: the AOE fires again.
                turnManager.SetTurn(1);
                source.TakeDamage(1);
                Assert.AreEqual(9, enemy.Stats.CurrentHealth,
                    "Metal Fist should fire when damage is taken during the wearer's own turn.");

                UnityEngine.Object.DestroyImmediate(enemy.gameObject);
            }
            finally
            {
                DestroyAll(grid, source, manager);
                UnityEngine.Object.DestroyImmediate(turnGo);
            }
        }

        /// <summary>
        /// GrantBlockEffect grants block to the owner and routes through Character.GainBlock,
        /// so an OnGainBlock trigger (e.g. Restorative Shield's heal-on-gain-block) reacts to
        /// equipment-sourced block the same way it reacts to block from a card.
        /// </summary>
        [Test]
        public void GrantBlockEffect_GrantsBlock_AndFiresOnGainBlock()
        {
            var grid = TestHelper.CreateTestGrid(3);
            var source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            var manager = BuildManager(grid, source);
            try
            {
                var heal = ScriptableObject.CreateInstance<HealEffect>();
                heal.Amount = 1;

                var grantBlock = ScriptableObject.CreateInstance<GrantBlockEffect>();
                grantBlock.Amount = 2;

                var grantedData = ScriptableObject.CreateInstance<EquipmentData>();
                grantedData.SlotType = EquipmentSlotType.Trinket;
                grantedData.Triggers.Add(new EquipmentTrigger { Timing = TriggerTiming.OnGainBlock, Effect = heal });
                Assert.IsTrue(manager.Equip(grantedData, EquipmentSlotType.Trinket, 0));

                // Damage the owner so the heal is observable.
                source.Stats.ApplyDamage(2); // 10 -> 8

                // Equipment-sourced block grants block and fires OnGainBlock -> heal 1.
                grantBlock.Execute(new EquipmentContext
                {
                    Source = source,
                    Grid = grid,
                    Timing = TriggerTiming.OnGainBlock
                });

                Assert.AreEqual(2, source.Stats.Block, "GrantBlockEffect should grant its Amount as block.");
                Assert.AreEqual(9, source.Stats.CurrentHealth, "OnGainBlock trigger should fire when block is gained from equipment, healing the owner.");
            }
            finally
            {
                DestroyAll(grid, source, manager);
            }
        }

        /// <summary>
        /// Snake Oil Flask's trigger is OnDealDamage -> shuffle a Snake Oil into the damaged
        /// enemy's deck. The owner and the victim must be distinct, and only the victim's
        /// deck (not the owner's) gains the card.
        /// </summary>
        [Test]
        public void SnakeOilFlask_OnDealDamage_ShufflesSnakeOilIntoVictimsDeck()
        {
            var grid = TestHelper.CreateTestGrid(3);
            var source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            var manager = BuildManager(grid, source);
            try
            {
                Assert.IsTrue(manager.Equip(LoadAsset("Snake Oil Flask")));

                Character enemy = TestHelper.CreateCharacterOnGrid(grid, 1, 1, 0);

                Assert.AreEqual(10, enemy.Stats.CurrentHealth, "Enemy should start at full health.");
                Assert.AreEqual(0, enemy.Deck.DrawPile.Count, "Victim's deck should start empty.");
                Assert.AreEqual(0, source.Deck.DrawPile.Count, "Owner's deck should start empty.");

                enemy.TakeDamage(2, source);

                Assert.AreEqual(8, enemy.Stats.CurrentHealth, "Enemy should have taken the damage.");
                Assert.AreEqual(1, enemy.Deck.DrawPile.Count,
                    "Dealing damage should shuffle exactly one Snake Oil into the victim's deck.");
                Assert.AreEqual(0, source.Deck.DrawPile.Count,
                    "The card must go into the victim's deck, not the owner's.");

                CardInstance shuffled = enemy.Deck.DrawPile[0];
                Assert.AreEqual("Snake Oil", shuffled.Data.CardName,
                    "The shuffled card should be Snake Oil.");

                UnityEngine.Object.DestroyImmediate(enemy.gameObject);
            }
            finally
            {
                DestroyAll(grid, source, manager);
            }
        }

        /// <summary>
        /// The trigger fires once per hit, so repeated damage keeps bloating the victim's deck.
        /// </summary>
        [Test]
        public void SnakeOilFlask_FiresOncePerHit()
        {
            var grid = TestHelper.CreateTestGrid(3);
            var source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            var manager = BuildManager(grid, source);
            try
            {
                Assert.IsTrue(manager.Equip(LoadAsset("Snake Oil Flask")));

                Character enemy = TestHelper.CreateCharacterOnGrid(grid, 1, 1, 0);
                enemy.Stats.Heal(10); // survive three hits

                enemy.TakeDamage(1, source);
                Assert.AreEqual(1, enemy.Deck.DrawPile.Count, "First hit should add one Snake Oil.");

                enemy.TakeDamage(1, source);
                Assert.AreEqual(2, enemy.Deck.DrawPile.Count, "Second hit should add another.");

                enemy.TakeDamage(1, source);
                Assert.AreEqual(3, enemy.Deck.DrawPile.Count, "Third hit should add another.");

                UnityEngine.Object.DestroyImmediate(enemy.gameObject);
            }
            finally
            {
                DestroyAll(grid, source, manager);
            }
        }

        /// <summary>
        /// Only the wearer that dealt the damage reacts: an ally dealing damage (or the
        /// victim dealing it) must not shuffle cards into anyone's deck.
        /// </summary>
        [Test]
        public void SnakeOilFlask_OnlyFiresForItsOwnWearer()
        {
            var grid = TestHelper.CreateTestGrid(3);
            var source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            var manager = BuildManager(grid, source);
            Character ally = null;
            Character enemy = null;
            try
            {
                Assert.IsTrue(manager.Equip(LoadAsset("Snake Oil Flask")));

                ally = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 2);
                enemy = TestHelper.CreateCharacterOnGrid(grid, 1, 1, 0);

                // The wearer hits an ally: damage still counts, but the ally is not an enemy,
                // so the effect's enemy check must keep its deck clean.
                ally.TakeDamage(1, source);
                Assert.AreEqual(0, ally.Deck.DrawPile.Count,
                    "Damaging an ally must not shuffle Snake Oil into the ally's deck.");

                // The enemy damages the wearer: the wearer's own trigger must not fire for it.
                source.TakeDamage(1, enemy);
                Assert.AreEqual(0, enemy.Deck.DrawPile.Count,
                    "Taking damage must not trigger OnDealDamage.");
            }
            finally
            {
                if (ally != null) UnityEngine.Object.DestroyImmediate(ally.gameObject);
                if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy.gameObject);
                DestroyAll(grid, source, manager);
            }
        }

        /// <summary>
        /// Damage from an attack card is routed through Character.TakeDamage with the card's
        /// source as the dealer, so an OnDealDamage trigger reacts to real card attacks too —
        /// not just to direct TakeDamage calls.
        /// </summary>
        [Test]
        public void SnakeOilFlask_ReactsToDamageFromAnAttackCard()
        {
            var grid = TestHelper.CreateTestGrid(3);
            var source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            var manager = BuildManager(grid, source);
            Character enemy = null;
            try
            {
                Assert.IsTrue(manager.Equip(LoadAsset("Snake Oil Flask")));
                enemy = TestHelper.CreateCharacterOnGrid(grid, 1, 1, 0);

                var attack = ScriptableObject.CreateInstance<AttackEffect>();
                attack.Damage = CardValue.MakeConstant(1);
                attack.Execute(new CardContext { Source = source, TargetCharacter = enemy });

                Assert.AreEqual(9, enemy.Stats.CurrentHealth, "The attack card should deal its damage.");
                Assert.AreEqual(1, enemy.Deck.DrawPile.Count,
                    "Damage dealt by an attack card should shuffle a Snake Oil into the target's deck.");

                UnityEngine.Object.DestroyImmediate(attack);
            }
            finally
            {
                if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy.gameObject);
                DestroyAll(grid, source, manager);
            }
        }

        private static EquipmentManager BuildManager(HexGridManager grid, Character character)
        {
            var go = new GameObject("TestEquipmentManager");
            var manager = go.AddComponent<EquipmentManager>();
            manager.Initialize(character);
            managerGridField.SetValue(manager, grid);
            return manager;
        }

        private static void DestroyAll(HexGridManager grid, Character character, EquipmentManager manager)
        {
            if (character != null) UnityEngine.Object.DestroyImmediate(character.gameObject);
            if (manager != null) UnityEngine.Object.DestroyImmediate(manager.gameObject);
            if (grid != null) UnityEngine.Object.DestroyImmediate(grid.gameObject);
        }

        private static int HexDistance(HexCoordinates a, HexCoordinates b)
        {
            int dq = Mathf.Abs(a.Q - b.Q);
            int dr = Mathf.Abs(a.R - b.R);
            return Mathf.Max(dq, Mathf.Max(dr, Mathf.Abs((a.Q + a.R) - (b.Q + b.R))));
        }
    }
}
