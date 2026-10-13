using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Cards;
using Cards.Effects;
using Characters;
using HexGrid;

namespace Tests.Cards
{
    [TestFixture]
    public class MoveEffectTests
    {
        private MoveEffect effect;
        private HexGridManager grid;
        private Character source;

        [SetUp]
        public void SetUp()
        {
            effect = ScriptableObject.CreateInstance<MoveEffect>();
            grid = TestHelper.CreateTestGrid(5);
            source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
        }

        [TearDown]
        public void TearDown()
        {
            if (source != null) Object.DestroyImmediate(source.gameObject);
            if (grid != null)
            {
                foreach (Transform child in grid.GetComponentsInChildren<Transform>(true))
                {
                    if (child != null && child != grid.transform)
                        Object.DestroyImmediate(child.gameObject);
                }
                if (grid != null) Object.DestroyImmediate(grid.gameObject);
            }
        }

        private CardContext ContextFor(int q, int r, int range)
        {
            return new CardContext
            {
                Source = source,
                Grid = grid,
                TargetCell = grid.GetCell(new HexCoordinates(q, r)),
                Range = range
            };
        }

        [Test]
        public void CanExecute_NullTarget_ReturnsFalse()
        {
            var ctx = new CardContext { Source = source, Grid = grid, Range = 5 };
            Assert.IsFalse(effect.CanExecute(ctx));
        }

        [Test]
        public void CanExecute_InRangeEmptyCell_ReturnsTrue()
        {
            Assert.IsTrue(effect.CanExecute(ContextFor(1, 0, 1)));
        }

        [Test]
        public void CanExecute_OutOfRange_ReturnsFalse()
        {
            Assert.IsFalse(effect.CanExecute(ContextFor(3, 0, 1)));
        }

        [Test]
        public void CanExecute_OccupiedTarget_ReturnsFalse()
        {
            var blocker = TestHelper.CreateCharacterOnGrid(grid, 1, 1, 0);
            try
            {
                Assert.IsFalse(effect.CanExecute(ContextFor(1, 0, 1)));
            }
            finally
            {
                Object.DestroyImmediate(blocker.gameObject);
            }
        }

        [Test]
        public void CanExecute_BlockedTerrain_ReturnsFalse()
        {
            grid.GetCell(new HexCoordinates(1, 0)).SetBlocked(true);
            Assert.IsFalse(effect.CanExecute(ContextFor(1, 0, 1)));
        }

        [Test]
        public void Execute_MovesSourceToTarget()
        {
            var ctx = ContextFor(2, 0, 2);
            effect.Execute(ctx);
            Assert.AreEqual(new HexCoordinates(2, 0), source.CurrentCell.Coordinates);
            Assert.AreSame(source, ctx.TargetCell.Occupant);
        }

        [Test]
        public void Execute_StepsThroughIntermediateCells()
        {
            var ctx = ContextFor(2, 0, 3);

            var visitedCells = new List<HexCoordinates>();
            source.OnMovedToCell += cell => visitedCells.Add(cell.Coordinates);

            effect.Execute(ctx);

            Assert.AreEqual(new HexCoordinates(2, 0), source.CurrentCell.Coordinates);
            Assert.IsTrue(visitedCells.Count > 1,
                "MoveEffect should step through each cell in the path, firing OnMovedToCell per step.");
        }

        [Test]
        public void CanExecute_ExplicitPath_WithinRange_ReturnsTrue()
        {
            var ctx = ContextFor(2, 0, 3);
            ctx.MovePath = new List<HexCell>
            {
                grid.GetCell(new HexCoordinates(1, 0)),
                grid.GetCell(new HexCoordinates(2, 0))
            };

            Assert.IsTrue(effect.CanExecute(ctx));
        }

        [Test]
        public void CanExecute_ExplicitPath_TooLong_ReturnsFalse()
        {
            var ctx = ContextFor(2, 0, 1);
            ctx.MovePath = new List<HexCell>
            {
                grid.GetCell(new HexCoordinates(1, 0)),
                grid.GetCell(new HexCoordinates(2, 0))
            };

            Assert.IsFalse(effect.CanExecute(ctx));
        }

        [Test]
        public void CanExecute_ExplicitPath_NotAdjacent_ReturnsFalse()
        {
            var ctx = ContextFor(3, 0, 3);
            // Skips a cell: (1,0) is adjacent to the source, but (3,0) is not adjacent to (1,0).
            ctx.MovePath = new List<HexCell>
            {
                grid.GetCell(new HexCoordinates(1, 0)),
                grid.GetCell(new HexCoordinates(3, 0))
            };

            Assert.IsFalse(effect.CanExecute(ctx));
        }

        [Test]
        public void CanExecute_ExplicitPath_DoesNotEndOnTarget_ReturnsFalse()
        {
            var ctx = ContextFor(2, 0, 3);
            // Path ends at (1,0), but the requested target is (2,0).
            ctx.MovePath = new List<HexCell>
            {
                grid.GetCell(new HexCoordinates(1, 0))
            };

            Assert.IsFalse(effect.CanExecute(ctx));
        }

        [Test]
        public void CanExecute_ExplicitPath_BlockedCell_ReturnsFalse()
        {
            var ctx = ContextFor(2, 0, 3);
            grid.GetCell(new HexCoordinates(1, 0)).SetBlocked(true);
            ctx.MovePath = new List<HexCell>
            {
                grid.GetCell(new HexCoordinates(1, 0)),
                grid.GetCell(new HexCoordinates(2, 0))
            };

            Assert.IsFalse(effect.CanExecute(ctx));
        }

        [Test]
        public void Execute_ExplicitPath_WalksExactlyTheGivenRoute()
        {
            var ctx = ContextFor(3, 0, 3);
            ctx.MovePath = new List<HexCell>
            {
                grid.GetCell(new HexCoordinates(1, 0)),
                grid.GetCell(new HexCoordinates(2, 0)),
                grid.GetCell(new HexCoordinates(3, 0))
            };

            var visitedCells = new List<HexCoordinates>();
            source.OnMovedToCell += cell => visitedCells.Add(cell.Coordinates);

            effect.Execute(ctx);

            Assert.AreEqual(3, visitedCells.Count);
            Assert.AreEqual(new HexCoordinates(1, 0), visitedCells[0]);
            Assert.AreEqual(new HexCoordinates(2, 0), visitedCells[1]);
            Assert.AreEqual(new HexCoordinates(3, 0), visitedCells[2]);
            Assert.AreEqual(new HexCoordinates(3, 0), source.CurrentCell.Coordinates);
        }
    }
}
