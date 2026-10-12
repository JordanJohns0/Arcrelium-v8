using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Cards;
using Cards.Effects;
using Characters;
using HexGrid;
using Game;
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

    /// <summary>
    /// A card that awaits a HexCell target must treat a click on a character as a click on the
    /// cell that character stands on, rather than reporting "this card targets a cell, not a
    /// character" and doing nothing.
    /// </summary>
    [TestFixture]
    public class CellTargetByCharacterTests
    {
        private static readonly MethodInfo handleCharacterClicked =
            typeof(TurnFlowUIController).GetMethod("HandleCharacterClicked",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly MethodInfo handleCellClicked =
            typeof(TurnFlowUIController).GetMethod("HandleCellClicked",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly List<Object> owned = new List<Object>();
        private readonly List<ScriptableObject> tracked = new List<ScriptableObject>();
        private HexGridManager grid;
        private TurnManager turnManager;
        private TurnFlowUIController controller;
        private Character player;
        private Character enemy;

        [SetUp]
        public void SetUp()
        {
            GameState.LocalPlayerId = 0;

            grid = TestHelper.CreateTestGrid(3);
            owned.Add(grid.gameObject);

            player = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            owned.Add(player.gameObject);
            enemy = TestHelper.CreateCharacterOnGrid(grid, 1, 1, 0);
            owned.Add(enemy.gameObject);

            var tmGo = new GameObject("TurnManager");
            owned.Add(tmGo);
            turnManager = tmGo.AddComponent<TurnManager>();

            var arGo = new GameObject("ActionResolver");
            owned.Add(arGo);
            var actionResolver = arGo.AddComponent<ActionResolver>();
            typeof(ActionResolver).GetField("grid", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(actionResolver, grid);
            typeof(TurnManager).GetField("actionResolver", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(turnManager, actionResolver);

            turnManager.SetTurnOrder(new List<Character> { player, enemy });
            turnManager.StartMatch();

            // Built inactive so OnEnable (which wires scene references) never runs in the test.
            var handGo = new GameObject("HandView");
            handGo.SetActive(false);
            owned.Add(handGo);
            var handView = handGo.AddComponent<HandView>();

            var uiGo = new GameObject("TurnFlowUI");
            uiGo.SetActive(false);
            owned.Add(uiGo);
            controller = uiGo.AddComponent<TurnFlowUIController>();
            SetPrivate("turnManager", turnManager);
            SetPrivate("handView", handView);
            SetPrivate("grid", grid);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (ScriptableObject so in tracked)
                if (so != null) Object.DestroyImmediate(so);
            tracked.Clear();
            foreach (Object obj in owned)
                if (obj != null) Object.DestroyImmediate(obj);
            owned.Clear();
        }

        private void SetPrivate(string field, object value)
        {
            typeof(TurnFlowUIController).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(controller, value);
        }

        private CardInstance SetPendingCard(CardData data)
        {
            var card = new CardInstance(data);
            player.Hand.AddCard(card);

            SetPrivate("pendingCard", card);
            FieldInfo stateField = typeof(TurnFlowUIController).GetField("state",
                BindingFlags.NonPublic | BindingFlags.Instance);
            stateField.SetValue(controller, System.Enum.Parse(stateField.FieldType, "SelectingTarget"));
            return card;
        }

        private void PreparePendingCellCard()
        {
            var effect = ScriptableObject.CreateInstance<AreaDamageEffect>();
            tracked.Add(effect);
            effect.Damage = CardValue.MakeConstant(3);
            effect.Radius = 0;
            effect.Faction = AreaFaction.Enemies;

            var data = TestHelper.MakeCard("Test Blast", 0,
                TestHelper.Step(effect, TargetType.HexCell, 3));
            tracked.Add(data);

            SetPendingCard(data);
        }

        private void PreparePendingCharacterCard(TargetType targetType)
        {
            var effect = ScriptableObject.CreateInstance<AttackEffect>();
            tracked.Add(effect);
            effect.Damage = CardValue.MakeConstant(3);

            var data = TestHelper.MakeCard("Test Strike", 0,
                TestHelper.Step(effect, targetType, 3));
            tracked.Add(data);

            SetPendingCard(data);
        }

        [Test]
        public void ClickingCharacter_WithCellTargetCard_TargetsTheCellTheyStandOn()
        {
            PreparePendingCellCard();
            int before = enemy.Stats.CurrentHealth;

            handleCharacterClicked.Invoke(controller, new object[] { enemy });

            Assert.AreEqual(before - 3, enemy.Stats.CurrentHealth,
                "Clicking the enemy should select the cell it stands on and resolve the blast there.");
        }

        [Test]
        public void ClickingCellWithCharacter_WithCharacterTargetCard_TargetsThatCharacter()
        {
            PreparePendingCharacterCard(TargetType.EnemyCharacter);
            int before = enemy.Stats.CurrentHealth;

            handleCellClicked.Invoke(controller, new object[] { enemy.CurrentCell });

            Assert.AreEqual(before - 3, enemy.Stats.CurrentHealth,
                "Clicking the enemy's tile should target the enemy standing on it.");
        }

        [Test]
        public void ClickingEmptyCell_WithCharacterTargetCard_DoesNotResolve()
        {
            PreparePendingCharacterCard(TargetType.EnemyCharacter);
            int before = enemy.Stats.CurrentHealth;

            handleCellClicked.Invoke(controller, new object[] { grid.GetCell(new HexCoordinates(2, 0)) });

            Assert.AreEqual(before, enemy.Stats.CurrentHealth,
                "An empty tile is not a character target; nothing should resolve.");
        }
    }
}
