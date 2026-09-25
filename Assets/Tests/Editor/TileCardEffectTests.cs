using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Characters;
using Cards;
using Cards.Effects;
using Equipment;
using Game;
using HexGrid;
using TileEffects;
using Tests;

namespace Tests.TileEffects
{
    /// <summary>
    /// Behavior tests for the three environment cards (Grease, Fire Wall, Mould Earth).
    /// Effects are exercised directly through CardContext so placement, wall shape and
    /// push/crush behavior are asserted precisely.
    /// </summary>
    public class TileCardEffectTests
    {
        private HexGridManager grid;
        private TileEffectSystem system;
        private readonly List<GameObject> objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            grid = TestHelper.CreateTestGrid(4);
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
            foreach (Character character in Object.FindObjectsOfType<Character>())
            {
                Object.DestroyImmediate(character.gameObject);
            }
            objects.Clear();
        }

        private HexCell Cell(int q, int r) => grid.GetCell(new HexCoordinates(q, r));

        private Character At(int ownerId, int q, int r)
        {
            Character character = TestHelper.CreateCharacterOnGrid(grid, ownerId, q, r);
            objects.Add(character.gameObject);
            return character;
        }

        private static OilTileEffectSO Oil()
        {
            var oil = ScriptableObject.CreateInstance<OilTileEffectSO>();
            oil.EffectName = "Oil";
            oil.Slippery = true;
            return oil;
        }

        private static FireTileEffectSO Fire()
        {
            var fire = ScriptableObject.CreateInstance<FireTileEffectSO>();
            fire.EffectName = "Fire";
            fire.Duration = 2;
            fire.FireDamage = 2;
            return fire;
        }

        private static RockTileEffectSO Rock()
        {
            var rock = ScriptableObject.CreateInstance<RockTileEffectSO>();
            rock.EffectName = "Rock";
            rock.BlocksMovement = true;
            return rock;
        }

        private static CardContext Ctx(HexGridManager grid, Character source, HexCell cell, int range)
        {
            return new CardContext { Source = source, Grid = grid, TargetCell = cell, Range = range };
        }

        private static bool Haseffect(HexCell cell, TileEffectSO definition)
        {
            return cell.HasEffect(definition);
        }

        // ------------------------------------------------------------ Grease

        [Test]
        public void PlaceTileEffect_PutsOilOnTarget()
        {
            Character source = At(0, 0, 0);
            HexCell target = Cell(3, 0);
            OilTileEffectSO oil = Oil();

            var place = ScriptableObject.CreateInstance<PlaceTileEffect>();
            place.Definition = oil;

            CardContext ctx = Ctx(grid, source, target, 3);
            Assert.IsTrue(place.CanExecute(ctx), "Oil can be placed on a tile in range.");
            place.Execute(ctx);

            Assert.IsTrue(Haseffect(target, oil), "Oil should be on the target tile.");
            Assert.IsFalse(Cell(2, 0).HasEffect(effect => effect.Slippery),
                "No oil should leak onto neighbouring tiles.");
        }

        [Test]
        public void PlaceTileEffect_DoesNotStackSameDefinition()
        {
            Character source = At(0, 0, 0);
            HexCell target = Cell(3, 0);
            OilTileEffectSO oil = Oil();

            var place = ScriptableObject.CreateInstance<PlaceTileEffect>();
            place.Definition = oil;

            CardContext ctx = Ctx(grid, source, target, 3);
            place.Execute(ctx);
            place.Execute(ctx);

            Assert.AreEqual(1, target.Effects.Count(effect => effect.Data == oil),
                "Casting grease twice on the same tile must not stack two oil slicks.");
        }

        [Test]
        public void PlaceTileEffect_RefusesOutOfRange()
        {
            Character source = At(0, 0, 0);
            HexCell target = Cell(4, 0); // distance 4 > range 3

            var place = ScriptableObject.CreateInstance<PlaceTileEffect>();
            place.Definition = Oil();

            Assert.IsFalse(place.CanExecute(Ctx(grid, source, target, 3)));
        }

        // ------------------------------------------------------------ Fire Wall

        [Test]
        public void FireWall_BurnsTargetAndItsTwoFlanks()
        {
            Character source = At(0, 0, 0);
            HexCell target = Cell(1, 0); // east of source
            FireTileEffectSO fire = Fire();

            var wall = ScriptableObject.CreateInstance<FireWallEffect>();
            wall.FireDefinition = fire;

            CardContext ctx = Ctx(grid, source, target, 1);
            Assert.IsTrue(wall.CanExecute(ctx), "A Fire Wall must be playable on an adjacent hex.");
            wall.Execute(ctx);

            Assert.IsTrue(Haseffect(target, fire), "Chosen hex must burn.");
            Assert.IsTrue(Haseffect(Cell(2, -1), fire), "Flank hex (NE of target) must burn.");
            Assert.IsTrue(Haseffect(Cell(1, 1), fire), "Flank hex (SE of target) must burn.");

            Assert.IsFalse(Cell(0, 0).HasEffect(effect => effect is FireTileEffectSO), "Caster tile must stay clear.");
            Assert.IsFalse(Cell(2, 0).HasEffect(effect => effect is FireTileEffectSO), "Only the wall burns.");
        }

        [Test]
        public void FireWall_RequiresAdjacentTarget()
        {
            Character source = At(0, 0, 0);
            var wall = ScriptableObject.CreateInstance<FireWallEffect>();
            wall.FireDefinition = Fire();

            Assert.IsTrue(wall.CanExecute(Ctx(grid, source, Cell(1, 0), 1)), "Adjacent pick is valid.");
            Assert.IsTrue(wall.CanExecute(Ctx(grid, source, Cell(-1, 1), 1)), "Any adjacent pick is valid.");
            Assert.IsFalse(wall.CanExecute(Ctx(grid, source, Cell(2, 0), 1)),
                "A Fire Wall only ignites hexes directly around the caster.");
        }

        [Test]
        public void FireWall_DoesNotStackOrDoubleBurn()
        {
            Character source = At(0, 0, 0);
            HexCell target = Cell(1, 0);
            FireTileEffectSO fire = Fire();

            var wall = ScriptableObject.CreateInstance<FireWallEffect>();
            wall.FireDefinition = fire;

            CardContext ctx = Ctx(grid, source, target, 1);
            wall.Execute(ctx);
            wall.Execute(ctx);

            Assert.AreEqual(1, target.Effects.Count(effect => effect.Data == fire),
                "A second wall must not double-stack fire on an already burning tile.");
        }

        // ------------------------------------------------------------ Mould Earth

        [Test]
        public void PushOccupants_HurlsEnemyToOneAdjacentTile()
        {
            Character source = At(0, 0, 0);
            Character enemy = At(1, 3, 0);

            var push = ScriptableObject.CreateInstance<PushOccupantsEffect>();
            CardContext ctx = Ctx(grid, source, Cell(3, 0), 3);
            Assert.IsTrue(push.CanExecute(ctx), "A standing enemy on the target tile is a valid mould earth play.");
            push.Execute(ctx);

            Assert.AreEqual(1, enemy.CurrentCell.Coordinates.DistanceTo(new HexCoordinates(3, 0)),
                "The enemy should land one hex away from where the rock rose.");
            Assert.IsNull(Cell(3, 0).Occupant, "The target tile must be empty for the rock.");
        }

        [Test]
        public void PushOccupants_WontTargetAlliedOrCasterTiles()
        {
            Character source = At(0, 0, 0);
            Character ally = At(0, 2, 0);

            var push = ScriptableObject.CreateInstance<PushOccupantsEffect>();
            Assert.IsFalse(push.CanExecute(Ctx(grid, source, Cell(2, 0), 3)),
                "An ally standing on the target blocks the play.");
            Assert.IsFalse(push.CanExecute(Ctx(grid, source, Cell(0, 0), 3)),
                "The caster's own tile can never be excavated.");
        }

        [Test]
        public void MouldEarth_CrushesEnemy_WhenThereIsNoExit()
        {
            Character source = At(0, 0, 0);
            Character enemy = At(1, 2, 0);

            rockOf(Cell(3, 0));
            rockOf(Cell(3, -1));
            rockOf(Cell(2, -1));
            rockOf(Cell(1, 0));
            rockOf(Cell(1, 1));
            rockOf(Cell(2, 1));

            var push = ScriptableObject.CreateInstance<PushOccupantsEffect>();
            CardContext ctx = Ctx(grid, source, Cell(2, 0), 3);
            Assert.IsTrue(push.CanExecute(ctx), "The play is valid even though the enemy has no escape.");
            push.Execute(ctx);

            Assert.IsFalse(enemy.Stats.IsAlive, "An enemy with no walkable exit must be crushed.");
            Assert.IsNull(Cell(2, 0).Occupant, "The crushed enemy must leave the tile empty for the rock.");
        }

        [Test]
        public void MouldEarth_PushesThenRocksTheTile()
        {
            Character source = At(0, 0, 0);
            Character enemy = At(1, 2, -1);
            RockTileEffectSO rock = Rock();

            var push = ScriptableObject.CreateInstance<PushOccupantsEffect>();
            var place = ScriptableObject.CreateInstance<PlaceTileEffect>();
            place.Definition = rock;

            CardContext ctx = Ctx(grid, source, Cell(2, -1), 3);
            Assert.IsTrue(push.CanExecute(ctx));
            Assert.IsTrue(place.CanExecute(ctx));

            push.Execute(ctx);
            place.Execute(ctx);

            Assert.AreEqual(1, enemy.CurrentCell.Coordinates.DistanceTo(new HexCoordinates(2, -1)),
                "Enemy should be displaced to an adjacent tile.");
            Assert.IsTrue(Haseffect(Cell(2, -1), rock), "A rock should now block the excavated tile.");
        }

        private void rockOf(HexCell cell)
        {
            if (cell == null || cell.HasBlockingEffect) return;
            Assert.IsNotNull(system.PlaceEffect(cell, Rock()),
                $"Could not place the rock on {cell.Coordinates}");
        }

        // ------------------------------------------------------------ asset wiring

        [Test]
        public void CardAssets_WireWeatherAndRegionsCorrectly()
        {
            GameDataLibrary library = GameDataLibrary.Instance;
            Assert.IsNotNull(library, "GameDataLibrary must exist in Resources.");

            AssertWiredAsPlace(library, "Grease", typeof(PlaceTileEffect), typeof(OilTileEffectSO));
            AssertWiredAsPlace(library, "Mould Earth", typeof(PlaceTileEffect), typeof(RockTileEffectSO), stepIndex: 1);

            CardData fireWall = library.GetCardByName("Fire Wall");
            Assert.IsNotNull(fireWall, "Fire Wall missing from GameDataLibrary.");
            FireWallEffect wall = fireWall.Steps[0].Effect as FireWallEffect;
            Assert.IsNotNull(wall, "Fire Wall must use FireWallEffect.");
            Assert.IsNotNull(wall.FireDefinition as FireTileEffectSO, "Fire Wall must reference the Fire tile effect.");
        }

        private static void AssertWiredAsPlace(GameDataLibrary library, string cardName,
            System.Type effectType, System.Type definitionType, int stepIndex = 0)
        {
            CardData card = library.GetCardByName(cardName);
            Assert.IsNotNull(card, $"'{cardName}' missing from GameDataLibrary.");
            Assert.IsTrue(card.Steps.Count > stepIndex, $"'{cardName}' has no step {stepIndex}.");
            CardEffectSO effect = card.Steps[stepIndex].Effect;
            Assert.IsNotNull(effect, $"'{cardName}' step has no effect.");
            Assert.IsInstanceOf(effectType, effect, $"'{cardName}' must use {effectType.Name}.");
            PlaceTileEffect place = (PlaceTileEffect)effect;
            Assert.IsNotNull(place.Definition, $"'{cardName}' references no tile effect definition.");
            Assert.IsInstanceOf(definitionType, place.Definition, $"'{cardName}' must place {definitionType.Name}.");
        }

        [Test]
        public void MouldEarth_IsComposedOfPushThenRock()
        {
            GameDataLibrary library = GameDataLibrary.Instance;
            Assert.IsNotNull(library, "GameDataLibrary must exist in Resources.");

            CardData card = library.GetCardByName("Mould Earth");
            Assert.IsNotNull(card, "Mould Earth missing from GameDataLibrary.");
            Assert.AreEqual(2, card.Steps.Count, "Mould Earth must push, then place the rock.");
            Assert.IsInstanceOf(typeof(PushOccupantsEffect), card.Steps[0].Effect);
            Assert.IsInstanceOf(typeof(PlaceTileEffect), card.Steps[1].Effect);
            Assert.IsNotNull(((PlaceTileEffect)card.Steps[1].Effect).Definition as RockTileEffectSO);
        }
    }
}