using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Characters;
using HexGrid;
using TileEffects;

namespace Tests.TileEffects
{
    public class TileEffectSystemTests
    {
        private HexGridManager grid;
        private TileEffectSystem system;
        private readonly List<GameObject> objects = new List<GameObject>();

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
            foreach (Character character in Object.FindObjectsOfType<Character>())
            {
                Object.DestroyImmediate(character.gameObject);
            }
            objects.Clear();
        }

        private Character At(int q, int r)
        {
            Character character = TestHelper.CreateCharacterOnGrid(grid, 0, q, r);
            objects.Add(character.gameObject);
            return character;
        }

        private HexCell Cell(int q, int r) => grid.GetCell(new HexCoordinates(q, r));

        private FireTileEffectSO Fire(int fireDamage = 2, int explosionDamage = 3)
        {
            var fire = ScriptableObject.CreateInstance<FireTileEffectSO>();
            fire.EffectName = "Fire";
            fire.Duration = 2;
            fire.FireDamage = fireDamage;
            fire.ExplosionDamage = explosionDamage;
            return fire;
        }

        private OilTileEffectSO Oil()
        {
            var oil = ScriptableObject.CreateInstance<OilTileEffectSO>();
            oil.EffectName = "Oil";
            oil.Duration = 0;
            oil.Slippery = true;
            return oil;
        }

        private RockTileEffectSO Rock()
        {
            var rock = ScriptableObject.CreateInstance<RockTileEffectSO>();
            rock.EffectName = "Rock";
            rock.Duration = 0;
            rock.BlocksMovement = true;
            return rock;
        }

        // ------------------------------------------------------- movement / entry

        [Test]
        public void Oil_Slides_CharacterOneTileForward_InMovementDirection()
        {
            Character hero = At(0, 0);
            var oilCoords = new HexCoordinates(0, 0).Neighbor(0);
            HexCell oilCell = Cell(oilCoords.Q, oilCoords.R);
            HexCell beyond = Cell(oilCoords.Neighbor(0).Q, oilCoords.Neighbor(0).R);

            system.PlaceEffect(oilCell, Oil());

            system.WalkInto(hero, oilCell);

            Assert.AreEqual(beyond.Coordinates, hero.CurrentCell.Coordinates,
                "Character should slide past the oil.");
            Assert.AreEqual(oilCoords, hero.LastCell.Coordinates);
        }

        [Test]
        public void Oil_SlideBlocked_CharacterStopsOnOil()
        {
            Character hero = At(0, 0);
            var oilCoords = new HexCoordinates(0, 0).Neighbor(0);
            HexCell oilCell = Cell(oilCoords.Q, oilCoords.R);
            At(oilCoords.Neighbor(0).Q, oilCoords.Neighbor(0).R); // blocker occupies the slide target

            system.PlaceEffect(oilCell, Oil());

            system.WalkInto(hero, oilCell);

            Assert.AreEqual(oilCell.Coordinates, hero.CurrentCell.Coordinates);
        }

        [Test]
        public void Fire_DamagesCharacter_OnEntry()
        {
            Character hero = At(0, 0);
            HexCell fireCell = Cell(1, 0);
            system.PlaceEffect(fireCell, Fire());

            system.WalkInto(hero, fireCell);

            Assert.AreEqual(10 - 2, hero.Stats.CurrentHealth);
        }

        [Test]
        public void MoveEffect_Routes_ThroughSlippingOil()
        {
            Character hero = At(0, 0);
            var oilCoords = new HexCoordinates(0, 0).Neighbor(0);
            HexCell oilCell = Cell(oilCoords.Q, oilCoords.R);
            HexCell beyond = Cell(oilCoords.Neighbor(0).Q, oilCoords.Neighbor(0).R);
            system.PlaceEffect(oilCell, Oil());

            var move = ScriptableObject.CreateInstance<global::Cards.Effects.MoveEffect>();
            move.Execute(new global::Cards.CardContext
            {
                Source = hero,
                Grid = grid,
                TargetCell = oilCell,
                MovePath = new List<HexCell> { oilCell }
            });

            Assert.AreEqual(beyond.Coordinates, hero.CurrentCell.Coordinates);
        }

        // ------------------------------------------------------- turn start / ticking

        [Test]
        public void Fire_DamagesCharacter_AtStartOfTurn()
        {
            Character hero = At(1, 0);
            system.PlaceEffect(hero.CurrentCell, Fire());

            system.HandleTurnStarted(hero, isRoundStart: false);

            Assert.AreEqual(10 - 2, hero.Stats.CurrentHealth);
        }

        [Test]
        public void Fire_TicksDown_OncePerRound_AndExpires()
        {
            Character hero = At(0, 0);
            HexCell fireCell = Cell(2, 1);
            TileEffectInstance placed = system.PlaceEffect(fireCell, Fire());
            Assert.AreEqual(2, placed.TurnsRemaining);

            system.HandleTurnStarted(hero, isRoundStart: true);
            Assert.AreEqual(1, placed.TurnsRemaining, "Round start should tick one round.");

            system.HandleTurnStarted(hero, isRoundStart: false);
            Assert.AreEqual(1, placed.TurnsRemaining, "Mid-round turns should not tick.");

            system.HandleTurnStarted(hero, isRoundStart: true);
            Assert.AreEqual(0, placed.TurnsRemaining);
            Assert.IsFalse(fireCell.Effects.Contains(placed), "Fire should be removed after its last round.");
        }

        [Test]
        public void PermanentEffects_NeverExpire()
        {
            Character hero = At(0, 0);
            HexCell oilCell = Cell(2, 1);
            TileEffectInstance placed = system.PlaceEffect(oilCell, Oil());

            system.HandleTurnStarted(hero, isRoundStart: true);
            system.HandleTurnStarted(hero, isRoundStart: true);

            Assert.IsTrue(oilCell.Effects.Contains(placed), "Permanent oil should survive rounds.");
        }

        // ------------------------------------------------------- ignition & rock

        [Test]
        public void FireOnOil_Explodes_DamagingTileAndAdjacentCharacters()
        {
            Character victim = At(0, 0);
            Character neighbor = At(1, 0);

            system.PlaceEffect(victim.CurrentCell, Oil());
            system.PlaceEffect(victim.CurrentCell, Fire());

            Assert.AreEqual(10 - 3, victim.Stats.CurrentHealth);
            Assert.AreEqual(10 - 3, neighbor.Stats.CurrentHealth);
            Assert.IsFalse(victim.CurrentCell.HasEffect(effect => effect.Slippery), "Oil should be consumed by the blast.");
            Assert.IsTrue(victim.CurrentCell.HasEffect(effect => effect is FireTileEffectSO), "Fire should stay lit.");
        }

        [Test]
        public void Rock_BlocksPathing_MoveStopsBeforeIt()
        {
            Character hero = At(0, 0);
            HexCell rockCell = Cell(1, 0);
            system.PlaceEffect(rockCell, Rock());

            List<HexCell> moved = system.MoveCharacter(hero, new List<HexCell> { rockCell });

            Assert.IsEmpty(moved);
            Assert.AreEqual(new HexCoordinates(0, 0), hero.CurrentCell.Coordinates);
            Assert.IsFalse(rockCell.IsWalkable);
        }

        [Test]
        public void Rock_OnOccupiedTile_IsRejected()
        {
            Character hero = At(1, 0);

            TileEffectInstance placed = system.PlaceEffect(hero.CurrentCell, Rock());

            Assert.IsNull(placed);
            Assert.IsFalse(hero.CurrentCell.HasBlockingEffect);
        }

        [Test]
        public void Rock_BlocksPathfinding_ShortestRouteCannotCrossIt()
        {
            Character hero = At(0, 0);
            Assert.IsNotNull(
                grid.FindPath(hero.CurrentCell.Coordinates, new HexCoordinates(2, 0), 2),
                "Baseline: (0,0)->(2,0) is two steps with no obstacles.");

            system.PlaceEffect(Cell(1, 0), Rock());

            Assert.IsNull(
                grid.FindPath(hero.CurrentCell.Coordinates, new HexCoordinates(2, 0), 2),
                "The shortest two-step route straight through the rock must be blocked.");
            Assert.IsNull(
                grid.FindPath(hero.CurrentCell.Coordinates, new HexCoordinates(1, 0), 5),
                "The rock cell itself is never walkable.");
        }
    }
}