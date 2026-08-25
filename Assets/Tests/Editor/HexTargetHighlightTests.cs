using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Cards;
using Cards.Effects;
using Characters;
using HexGrid;
using UI;

namespace Tests.UI
{
    /// <summary>
    /// IsValidTarget drives board highlighting when a card awaits a target. Hex-cell cards
    /// with Constraint.Any (e.g. Fireball) must treat OCCUPIED cells as valid — only blocked
    /// terrain and (for Empty-constrained cards) occupants are invalid. Clicking already
    /// allowed these cells; the highlight must agree or players see dead hexes.
    /// </summary>
    public class HexTargetHighlightTests
    {
        private static readonly MethodInfo isValidTarget =
            typeof(TurnFlowUIController).GetMethod("IsValidTarget",
                BindingFlags.NonPublic | BindingFlags.Static);

        private readonly List<Object> owned = new List<Object>();
        private HexGridManager grid;

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in owned)
                if (obj != null) Object.DestroyImmediate(obj);
            owned.Clear();
            if (grid != null) Object.DestroyImmediate(grid.gameObject);
        }

        private Character Place(int ownerId, int q, int r)
        {
            Character character = TestHelper.CreateCharacterOnGrid(grid, ownerId, q, r);
            owned.Add(character.gameObject);
            return character;
        }

        private static CardData MakeCardWith(CellConstraint constraint)
        {
            var step = TestHelper.Step(
                ScriptableObject.CreateInstance<MoveEffect>(), TargetType.HexCell, 2);
            step.CellConstraint = constraint;
            return TestHelper.MakeCard(constraint == CellConstraint.Empty ? "EmptyC" : "AnyC", 0, step);
        }

        private static bool Invoke(CardData card, Character source, HexCell cell)
        {
            return (bool)isValidTarget.Invoke(null, new object[] { card, source, cell });
        }

        [SetUp]
        public void SetUp()
        {
            grid = TestHelper.CreateTestGrid(3);
        }

        [Test]
        public void OccupiedByEnemy_AnyConstraint_IsValid()
        {
            Character source = Place(0, 0, 0);
            Place(1, 1, 0); // enemy occupies the neighbouring cell

            HexCell cell = grid.GetCell(new HexCoordinates(1, 0));
            Assert.IsTrue(cell.IsOccupied, "Setup: the target cell must have an occupant.");
            Assert.IsTrue(Invoke(MakeCardWith(CellConstraint.Any), source, cell),
                "AoE-style cell cards must highlight (and allow) occupied cells.");
        }

        [Test]
        public void EmptyCell_AnyConstraint_IsValid()
        {
            Character source = Place(0, 0, 0);

            HexCell cell = grid.GetCell(new HexCoordinates(1, 0));
            Assert.IsTrue(Invoke(MakeCardWith(CellConstraint.Any), source, cell));
        }

        [Test]
        public void Occupied_EmptyConstraint_IsInvalid()
        {
            Character source = Place(0, 0, 0);
            Place(1, 1, 0);

            HexCell cell = grid.GetCell(new HexCoordinates(1, 0));
            Assert.IsFalse(Invoke(MakeCardWith(CellConstraint.Empty), source, cell),
                "Movement-style cards must still refuse occupied cells.");
        }

        [Test]
        public void BlockedCell_AlwaysInvalid()
        {
            Character source = Place(0, 0, 0);

            HexCell cell = grid.GetCell(new HexCoordinates(1, 0));
            cell.SetBlocked(true);
            Assert.IsFalse(Invoke(MakeCardWith(CellConstraint.Any), source, cell),
                "Blocked terrain is never targetable, regardless of constraint.");
        }
    }
}
