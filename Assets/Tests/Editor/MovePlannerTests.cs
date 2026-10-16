using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Cards;
using Cards.Effects;
using Characters;
using Game;
using HexGrid;
using TileEffects;

namespace Tests.TileEffects
{
    [TestFixture]
    public class MovePlannerTests
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
            foreach (Character character in Object.FindObjectsByType<Character>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(character.gameObject);
            }
            objects.Clear();
        }

        private HexCell Cell(HexCoordinates coords) => grid.GetCell(coords);

        private OilTileEffectSO Oil()
        {
            var oil = ScriptableObject.CreateInstance<OilTileEffectSO>();
            oil.EffectName = "Oil";
            oil.Duration = 0;
            oil.Slippery = true;
            return oil;
        }

        /// <summary>A hero at the origin, oil one tile east of it, and the cell beyond that oil.</summary>
        private (Character hero, HexCell oil, HexCell beyond) OilEastOfHero(int ownerId = 0)
        {
            Character hero = TestHelper.CreateCharacterOnGrid(grid, ownerId, 0, 0);
            objects.Add(hero.gameObject);

            HexCoordinates oilCoords = new HexCoordinates(0, 0).Neighbor(0);
            HexCell oil = Cell(oilCoords);
            HexCell beyond = Cell(oilCoords.Neighbor(0));
            system.PlaceEffect(oil, Oil());
            return (hero, oil, beyond);
        }

        [Test]
        public void SlideDestination_IsOnePastTheOilInTheDirectionOfTravel()
        {
            var (hero, oil, beyond) = OilEastOfHero();

            HexCell slide = MovePlanner.SlideDestination(grid, hero.CurrentCell, oil);

            Assert.AreEqual(beyond, slide);
        }

        [Test]
        public void Reachable_ThroughOil_EndsOnTheSlideDestination_NotOnTheOil()
        {
            var (hero, oil, beyond) = OilEastOfHero();

            var reachable = MovePlanner.Reachable(grid, hero.CurrentCell, 2);

            Assert.IsTrue(reachable.ContainsKey(beyond), "A character that slides onto a cell ends there.");
            Assert.IsFalse(reachable.ContainsKey(oil), "A character cannot stop on a tile it slides off of.");
            Assert.AreEqual(1, reachable[beyond].Steps, "Stepping onto the oil costs one step; the slide is free.");
        }

        [Test]
        public void FindRoute_ToTheOil_IsNull()
        {
            var (hero, oil, _) = OilEastOfHero();

            Assert.IsNull(MovePlanner.FindRoute(grid, hero.CurrentCell, oil, 3));
        }

        [Test]
        public void FindRoute_ToTheSlideDestination_CostsOneStep_AndRoutesThroughTheOil()
        {
            var (hero, oil, beyond) = OilEastOfHero();

            MoveRoute route = MovePlanner.FindRoute(grid, hero.CurrentCell, beyond, 1);

            Assert.IsNotNull(route, "One step is enough: step onto the oil and slide to the cell beyond.");
            Assert.AreEqual(1, route.Steps);
            CollectionAssert.AreEqual(new[] { oil, beyond }, route.Cells);
        }

        [Test]
        public void Validate_RouteThatStepsOntoOilThenContinuesFromWhereItLanded_IsLegal()
        {
            var (hero, oil, beyond) = OilEastOfHero();
            HexCell next = NeighborOf(beyond, exclude: oil);
            Assert.IsNotNull(next, "Precondition: the cell beyond the oil has a free neighbour.");

            MoveRoute route = MovePlanner.Validate(grid, hero.CurrentCell, new List<HexCell> { oil, beyond, next }, 2);

            Assert.IsNotNull(route);
            Assert.AreEqual(2, route.Steps, "Oil step plus one real step; the slide is free.");
            Assert.AreEqual(next, route.Resting);
        }

        [Test]
        public void Validate_StraightPathThatIgnoresTheSlide_IsRejected()
        {
            var (hero, oil, beyond) = OilEastOfHero();
            // A cell two steps from the landing spot (the old plain path would step to it from the oil).
            HexCell unreachableFromLanding = null;
            foreach (HexCell candidate in grid.GetNeighbors(oil.Coordinates))
            {
                if (candidate == null || candidate == hero.CurrentCell || candidate == beyond) continue;
                if (beyond.Coordinates.DistanceTo(candidate.Coordinates) == 2)
                {
                    unreachableFromLanding = candidate;
                    break;
                }
            }
            Assert.IsNotNull(unreachableFromLanding, "Precondition: a cell two away from the landing exists.");

            MoveRoute route = MovePlanner.Validate(grid, hero.CurrentCell,
                new List<HexCell> { oil, unreachableFromLanding }, 2);

            Assert.IsNull(route, "The character is no longer on the oil once it slides, so this path cannot be walked.");
        }

        [Test]
        public void Reachable_SlideBlockedByAnotherCharacter_StopsOnTheOil()
        {
            var (hero, oil, beyond) = OilEastOfHero();
            Character blocker = TestHelper.CreateCharacterOnGrid(grid, 1, beyond.Coordinates.Q, beyond.Coordinates.R);
            objects.Add(blocker.gameObject);

            var reachable = MovePlanner.Reachable(grid, hero.CurrentCell, 1);

            Assert.IsFalse(reachable.ContainsKey(beyond), "A slide into an occupied cell is not a landing spot.");
            Assert.IsTrue(reachable.ContainsKey(oil), "The character stops on the oil when the slide is blocked.");
        }

        [Test]
        public void MoveEffect_ExecutedWithTheRoute_ContinuesAfterTheSlide()
        {
            var (hero, oil, beyond) = OilEastOfHero();
            HexCell next = NeighborOf(beyond, exclude: oil);
            Assert.IsNotNull(next, "Precondition: the cell beyond the oil has a free neighbour.");

            var move = ScriptableObject.CreateInstance<MoveEffect>();
            var context = new CardContext
            {
                Source = hero,
                Grid = grid,
                TargetCell = next,
                MovePath = new List<HexCell> { oil, beyond, next },
                Range = 2
            };

            Assert.IsTrue(move.CanExecute(context), "The route is legal.");
            move.Execute(context);

            Assert.AreEqual(next.Coordinates, hero.CurrentCell.Coordinates,
                "After sliding on the oil the character must still walk the rest of the route.");
            Object.DestroyImmediate(move);
        }

        [Test]
        public void MoveEffect_ToTheOilTile_CannotBeTargeted()
        {
            var (hero, oil, _) = OilEastOfHero();
            var move = ScriptableObject.CreateInstance<MoveEffect>();
            var context = new CardContext
            {
                Source = hero,
                Grid = grid,
                TargetCell = oil,
                Range = 3
            };

            Assert.IsFalse(move.CanExecute(context), "A character cannot end its move on a tile it slides off of.");
            Object.DestroyImmediate(move);
        }

        [Test]
        public void MoveEffect_ForAnEnemy_PlansTheSameSlideRoute()
        {
            var (enemy, oil, beyond) = OilEastOfHero(ownerId: GameState.EnemyOwnerPlayerId);

            var move = ScriptableObject.CreateInstance<MoveEffect>();
            var context = new CardContext
            {
                Source = enemy,
                Grid = grid,
                TargetCell = beyond,
                Range = 1
            };

            Assert.IsTrue(move.CanExecute(context), "An enemy can slide onto the cell beyond the oil in one step.");
            move.Execute(context);

            Assert.AreEqual(beyond.Coordinates, enemy.CurrentCell.Coordinates,
                "An enemy's move resolves through the oil exactly like a player's.");
            Object.DestroyImmediate(move);
        }

        /// <summary>A walkable neighbour of <paramref name="of"/> that is not <paramref name="exclude"/> and not the origin hero.</summary>
        private HexCell NeighborOf(HexCell of, HexCell exclude)
        {
            foreach (HexCell candidate in grid.GetNeighbors(of.Coordinates))
            {
                if (candidate == null || candidate == exclude || !candidate.IsWalkable) continue;
                if (candidate.Coordinates.DistanceTo(new HexCoordinates(0, 0)) == 0) continue;
                return candidate;
            }
            return null;
        }
    }
}
