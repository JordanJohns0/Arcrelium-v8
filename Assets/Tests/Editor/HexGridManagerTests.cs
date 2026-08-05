using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Characters;
using HexGrid;

namespace Tests.Grid
{
    [TestFixture]
    public class HexGridManagerTests
    {
        private HexGridManager grid;

        [SetUp]
        public void SetUp()
        {
            grid = TestHelper.CreateTestGrid(3);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(grid.gameObject);
        }

        [Test]
        public void FindPath_SameStartAndGoal_ReturnsEmptyPath()
        {
            var path = grid.FindPath(new HexCoordinates(0, 0), new HexCoordinates(0, 0), 5);
            Assert.IsNotNull(path);
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void FindPath_AdjacentCell_ReturnsSingleStep()
        {
            var path = grid.FindPath(new HexCoordinates(0, 0), new HexCoordinates(1, 0), 5);
            Assert.IsNotNull(path);
            Assert.AreEqual(1, path.Count);
            Assert.AreEqual(new HexCoordinates(1, 0), path[0].Coordinates);
        }

        [Test]
        public void FindPath_TwoSteps_ReturnsPath()
        {
            var path = grid.FindPath(new HexCoordinates(0, 0), new HexCoordinates(2, 0), 5);
            Assert.IsNotNull(path);
            Assert.AreEqual(2, path.Count);
        }

        [Test]
        public void FindPath_OutOfRange_ReturnsNull()
        {
            var path = grid.FindPath(new HexCoordinates(0, 0), new HexCoordinates(3, 0), 2);
            Assert.IsNull(path);
        }

        [Test]
        public void FindPath_BlockedDestination_ReturnsNull()
        {
            grid.GetCell(new HexCoordinates(1, 0)).SetBlocked(true);
            var path = grid.FindPath(new HexCoordinates(0, 0), new HexCoordinates(1, 0), 5);
            Assert.IsNull(path);
        }

        [Test]
        public void FindPath_BlockedIntermediate_Reroutes()
        {
            grid.GetCell(new HexCoordinates(1, 0)).SetBlocked(true);

            var path = grid.FindPath(new HexCoordinates(0, 0), new HexCoordinates(2, 0), 5);
            Assert.IsNotNull(path);
            Assert.IsTrue(path.Count >= 2);

            foreach (var cell in path)
            {
                Assert.IsFalse(cell.IsBlocked, $"Path goes through blocked cell {cell.Coordinates}");
            }
        }

        [Test]
        public void FindPath_CompletelyBlocked_ReturnsNull()
        {
            grid.GetCell(new HexCoordinates(1, 0)).SetBlocked(true);
            grid.GetCell(new HexCoordinates(0, 1)).SetBlocked(true);
            grid.GetCell(new HexCoordinates(-1, 1)).SetBlocked(true);
            grid.GetCell(new HexCoordinates(-1, 0)).SetBlocked(true);
            grid.GetCell(new HexCoordinates(0, -1)).SetBlocked(true);
            grid.GetCell(new HexCoordinates(1, -1)).SetBlocked(true);

            var path = grid.FindPath(new HexCoordinates(0, 0), new HexCoordinates(2, 0), 5);
            Assert.IsNull(path);
        }

        [Test]
        public void FindPath_OccupiedDestination_ReturnsNull()
        {
            var blocker = TestHelper.CreateCharacterOnGrid(grid, 1, 1, 0);
            var path = grid.FindPath(new HexCoordinates(0, 0), new HexCoordinates(1, 0), 5);
            Assert.IsNull(path);
        }

        [Test]
        public void FindPath_OccupiedIntermediate_Reroutes()
        {
            TestHelper.CreateCharacterOnGrid(grid, 1, 1, 0);

            var path = grid.FindPath(new HexCoordinates(0, 0), new HexCoordinates(2, 0), 5);
            Assert.IsNotNull(path);

            foreach (var cell in path)
            {
                Assert.IsFalse(cell.IsOccupied, $"Path goes through occupied cell {cell.Coordinates}");
            }
        }

        [Test]
        public void FindPath_RespectsMaxSteps()
        {
            var path = grid.FindPath(new HexCoordinates(0, 0), new HexCoordinates(2, 0), 1);
            Assert.IsNull(path);
        }

        [Test]
        public void GetNeighbors_ReturnsSixForCenter()
        {
            var neighbors = new List<HexCell>(grid.GetNeighbors(new HexCoordinates(0, 0)));
            Assert.AreEqual(6, neighbors.Count);
        }

        [Test]
        public void GetCellsInRange_ReturnsCorrectCount()
        {
            var cells = new List<HexCell>(grid.GetCellsInRange(new HexCoordinates(0, 0), 1));
            Assert.AreEqual(7, cells.Count);
        }

        [Test]
        public void Obstacles_CanBeSetViaSerializedArray()
        {
            var cell = grid.GetCell(new HexCoordinates(1, 0));
            Assert.IsFalse(cell.IsBlocked);

            cell.SetBlocked(true);
            Assert.IsTrue(cell.IsBlocked);
            Assert.IsFalse(cell.IsWalkable);
        }

        [Test]
        public void Obstacle_BlocksPathButAdjacentCellsReachable()
        {
            grid.GetCell(new HexCoordinates(1, 0)).SetBlocked(true);

            var pathToObstacle = grid.FindPath(new HexCoordinates(0, 0), new HexCoordinates(1, 0), 5);
            Assert.IsNull(pathToObstacle);

            var pathAroundObstacle = grid.FindPath(new HexCoordinates(0, 0), new HexCoordinates(2, 0), 5);
            Assert.IsNotNull(pathAroundObstacle);
        }
    }
}
