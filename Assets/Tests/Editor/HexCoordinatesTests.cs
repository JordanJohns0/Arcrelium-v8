using NUnit.Framework;
using UnityEngine;
using HexGrid;

namespace Tests
{
    [TestFixture]
    public class HexCoordinatesTests
    {
        [Test]
        public void Constructor_SetsValues()
        {
            var coords = new HexCoordinates(3, -2);
            Assert.AreEqual(3, coords.Q);
            Assert.AreEqual(-2, coords.R);
        }

        [Test]
        public void S_IsDerived()
        {
            var coords = new HexCoordinates(2, 3);
            Assert.AreEqual(-5, coords.S);
        }

        [Test]
        public void DistanceTo_SameHex_ReturnsZero()
        {
            var a = new HexCoordinates(1, 2);
            Assert.AreEqual(0, a.DistanceTo(a));
        }

        [Test]
        public void DistanceTo_AdjacentHex_ReturnsOne()
        {
            var a = new HexCoordinates(0, 0);
            var b = new HexCoordinates(1, 0);
            Assert.AreEqual(1, a.DistanceTo(b));
        }

        [Test]
        public void DistanceTo_TwoStepsAway()
        {
            var a = new HexCoordinates(0, 0);
            var b = new HexCoordinates(2, 0);
            Assert.AreEqual(2, a.DistanceTo(b));
        }

        [Test]
        public void DistanceTo_DiagonalHex()
        {
            var a = new HexCoordinates(0, 0);
            var b = new HexCoordinates(1, -1);
            Assert.AreEqual(1, a.DistanceTo(b));
        }

        [Test]
        public void DistanceTo_IsSymmetric()
        {
            var a = new HexCoordinates(0, 0);
            var b = new HexCoordinates(3, -1);
            Assert.AreEqual(a.DistanceTo(b), b.DistanceTo(a));
        }

        [Test]
        public void Neighbor_ReturnsCorrectHex()
        {
            var origin = new HexCoordinates(0, 0);
            var neighbor = origin.Neighbor(0);

            Assert.AreEqual(1, neighbor.Q);
            Assert.AreEqual(0, neighbor.R);
        }

        [Test]
        public void Neighbor_AllSixDirections()
        {
            var origin = new HexCoordinates(0, 0);

            for (int i = 0; i < 6; i++)
            {
                var n = origin.Neighbor(i);
                Assert.AreEqual(1, origin.DistanceTo(n), $"Direction {i} should be distance 1");
            }
        }

        [Test]
        public void Neighbor_WrapsDirection()
        {
            var origin = new HexCoordinates(0, 0);
            var n6 = origin.Neighbor(6);
            var n0 = origin.Neighbor(0);

            Assert.AreEqual(n0.Q, n6.Q);
            Assert.AreEqual(n0.R, n6.R);
        }

        [Test]
        public void Neighbor_NegativeDirection()
        {
            var origin = new HexCoordinates(0, 0);
            var nMinus1 = origin.Neighbor(-1);
            var n5 = origin.Neighbor(5);

            Assert.AreEqual(n5.Q, nMinus1.Q);
            Assert.AreEqual(n5.R, nMinus1.R);
        }

        [Test]
        public void Equality_SameCoords_AreEqual()
        {
            var a = new HexCoordinates(2, 3);
            var b = new HexCoordinates(2, 3);
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
        }

        [Test]
        public void Equality_DifferentCoords_AreNotEqual()
        {
            var a = new HexCoordinates(2, 3);
            var b = new HexCoordinates(3, 2);
            Assert.IsFalse(a == b);
            Assert.IsTrue(a != b);
        }

        [Test]
        public void GetHashCode_SameForEqualCoords()
        {
            var a = new HexCoordinates(2, 3);
            var b = new HexCoordinates(2, 3);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void ToString_FormatsCorrectly()
        {
            var coords = new HexCoordinates(3, -5);
            Assert.AreEqual("(3, -5)", coords.ToString());
        }
    }
}
