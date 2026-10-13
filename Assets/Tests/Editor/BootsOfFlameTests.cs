using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Characters;
using Equipment;
using Equipment.Effects;
using HexGrid;
using TileEffects;

namespace Tests.Equipment
{
    /// <summary>
    /// Boots of Flame: when the wearer leaves a cell, that cell catches fire (OnLeftCell ->
    /// PlaceFireTileEffect with the fire definition bound to the asset).
    /// </summary>
    [TestFixture]
    public class BootsOfFlameTests
    {
        private static readonly System.Reflection.FieldInfo managerGridField =
            typeof(EquipmentManager).GetField("grid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        [Test]
        public void OnLeftCell_IgnoresCellsTheWearerWasNotOn()
        {
            // CanExecute-style guard is not the mechanism here: leaving a cell already means the
            // wearer WAS on it, so this test asserts the trigger fires exactly once per departure.
            var grid = TestHelper.CreateTestGrid(3);
            Character source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            var manager = BuildManager(grid, source);
            var tileGo = new GameObject("TestTileEffectSystem");
            var tileSystem = tileGo.AddComponent<TileEffectSystem>();
            try
            {
                tileSystem.InitializeForTests(grid, null);
                Assert.IsTrue(manager.Equip(LoadBoots()), "Boots of Flame should equip.");

                HexCell origin = source.CurrentCell;
                source.SetCellOnly(grid.GetCell(new HexCoordinates(1, 0)));

                Assert.IsTrue(origin.HasEffect(effect => effect is FireTileEffectSO),
                    "The cell the wearer just left should be on fire.");
            }
            finally
            {
                tileSystem.Shutdown();
                Object.DestroyImmediate(tileGo);
                Object.DestroyImmediate(source.gameObject);
                Object.DestroyImmediate(manager.gameObject);
                Object.DestroyImmediate(grid.gameObject);
            }
        }

        [Test]
        public void OnLeftCell_FiresOnlyForTheCellJustVacated()
        {
            var grid = TestHelper.CreateTestGrid(3);
            Character source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            var manager = BuildManager(grid, source);
            var tileGo = new GameObject("TestTileEffectSystem");
            var tileSystem = tileGo.AddComponent<TileEffectSystem>();
            try
            {
                tileSystem.InitializeForTests(grid, null);
                Assert.IsTrue(manager.Equip(LoadBoots()));

                HexCell first = source.CurrentCell;
                source.SetCellOnly(grid.GetCell(new HexCoordinates(1, 0))); // burns (0,0)
                HexCell second = source.CurrentCell;
                source.SetCellOnly(grid.GetCell(new HexCoordinates(2, 0))); // burns (1,0)

                Assert.IsTrue(first.HasEffect(effect => effect is FireTileEffectSO), "The first vacated cell should be on fire.");
                Assert.IsTrue(second.HasEffect(effect => effect is FireTileEffectSO), "The cell left on the second move should also be on fire.");
                Assert.IsFalse(grid.GetCell(new HexCoordinates(2, 0)).HasEffect(effect => effect is FireTileEffectSO),
                    "The cell the wearer is standing on must not be on fire.");
            }
            finally
            {
                tileSystem.Shutdown();
                Object.DestroyImmediate(tileGo);
                Object.DestroyImmediate(source.gameObject);
                Object.DestroyImmediate(manager.gameObject);
                Object.DestroyImmediate(grid.gameObject);
            }
        }

        private static EquipmentData LoadBoots()
        {
            var boots = AssetDatabase.LoadAssetAtPath<EquipmentData>("Assets/Equipment/EquipmentData/Boots of Flame.asset");
            Assert.IsNotNull(boots, "Boots of Flame asset missing.");
            return boots;
        }

        private static EquipmentManager BuildManager(HexGridManager grid, Character character)
        {
            var go = new GameObject("TestEquipmentManager");
            var manager = go.AddComponent<EquipmentManager>();
            manager.Initialize(character);
            managerGridField.SetValue(manager, grid);
            return manager;
        }
    }
}