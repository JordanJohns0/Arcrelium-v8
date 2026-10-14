using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Equipment;
using UI;
using Game;

namespace Tests.UI
{
    [TestFixture]
    public class ShopScreenTests
    {
        private readonly List<Object> tracked = new List<Object>();
        private PlayerInventory inventory;
        private int savedCount;

        [SetUp]
        public void SetUp()
        {
            savedCount = 0;
            GameState.CurrentLevel = 1;
            GameState.ShopSeed = 1234;
            inventory = Track(ScriptableObject.CreateInstance<PlayerInventory>()) as PlayerInventory;
            ShopScreen.LocalInventory = () => inventory;
            ShopScreen.PersistInventory = _ => savedCount++;
            // Pin the offer sources to explicit (empty) pools so no real Resources asset
            // leaks into the tests; MakePool then overrides the shop pool per test.
            ShopPool.SetMain(Track(ScriptableObject.CreateInstance<ShopPool>()) as ShopPool);
            RewardPool.SetMain(Track(ScriptableObject.CreateInstance<RewardPool>()) as RewardPool);
        }

        [TearDown]
        public void TearDown()
        {
            GameState.ShopSeed = 0;
            foreach (var screen in Object.FindObjectsByType<ShopScreen>(FindObjectsSortMode.None))
            {
                if (screen != null) Object.DestroyImmediate(screen);
            }
            ShopScreen.LocalInventory = PlayerInventoryProvider.GetLocal;
            ShopScreen.PersistInventory = inv =>
                PlayerInventoryProvider.Save(PlayerInventoryProvider.LocalPlayerKey, inv);
            ShopPool.SetMain(null);
            RewardPool.SetMain(null);
            PlayerInventoryProvider.ResetForTests();
            typeof(Tooltip).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, null);
            foreach (var obj in tracked)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            tracked.Clear();
        }

        private T Track<T>(T obj) where T : Object
        {
            tracked.Add(obj);
            return obj;
        }

        private EquipmentData MakeEquipment(string name)
        {
            var data = Track(ScriptableObject.CreateInstance<EquipmentData>()) as EquipmentData;
            data.EquipmentName = name;
            return data;
        }

        private ShopPool MakePool(params EquipmentData[] stock)
        {
            var pool = Track(ScriptableObject.CreateInstance<ShopPool>()) as ShopPool;
            pool.stock.AddRange(stock);
            ShopPool.SetMain(pool);
            return pool;
        }

        /// <summary>The pane the shop fills, mirroring the scene's "Special" column.</summary>
        private RectTransform MakeOffersPanel()
        {
            var go = Track(new GameObject("Special", typeof(RectTransform))) as GameObject;
            return go.GetComponent<RectTransform>();
        }

        private TMP_Text MakeLabel(string name)
        {
            var go = Track(new GameObject(name, typeof(TextMeshProUGUI))) as GameObject;
            return go.GetComponent<TMP_Text>();
        }

        /// <summary>A tile shaped like the shared Inventory Slot prefab (icon + script).</summary>
        private GameObject MakeTilePrefab()
        {
            return Track(new GameObject("Inventory Slot", typeof(RectTransform), typeof(Image), typeof(InventorySlotUI))) as GameObject;
        }

        /// <summary>Offered items in the order the shop displayed them.</summary>
        private static List<string> TileItemNames(RectTransform panel)
        {
            var names = new List<string>();
            for (int i = 0; i < panel.childCount; i++)
            {
                var slot = panel.GetChild(i).GetComponent<InventorySlotUI>();
                names.Add(slot != null && slot.EquipmentData != null ? slot.EquipmentData.EquipmentName : "");
            }
            return names;
        }

        private static ShopScreen Bind(RectTransform panel, GameObject prefab)
            => ShopScreen.Attach(panel, prefab);

        private static Button PickButton(RectTransform panel, int index)
            => panel.GetChild(index).GetComponentInChildren<Button>();

        private static TMP_Text TileLabel(RectTransform panel, int index)
            => panel.GetChild(index).Find("Status").GetComponent<TMP_Text>();

        [Test]
        public void Show_OffersThreeItems_AsInventoryStyleTiles()
        {
            MakePool(MakeEquipment("A"), MakeEquipment("B"), MakeEquipment("C"), MakeEquipment("D"));
            var panel = MakeOffersPanel();

            Bind(panel, MakeTilePrefab()).Show();

            Assert.AreEqual(3, panel.childCount, "The shop must offer exactly three items.");
            var slot = panel.GetChild(0).GetComponent<InventorySlotUI>();
            Assert.IsNotNull(slot, "Tiles must reuse the Inventory Slot prefab.");
            Assert.IsFalse(slot.Interactable, "Shop tiles must not be draggable.");
            Assert.AreEqual("", TileLabel(panel, 0).text,
                "Tiles carry no caption: the equipment slot already shows the name.");
        }

        [Test]
        public void Show_FewerThanThreeAvailable_OffersWhatExists()
        {
            MakePool(MakeEquipment("A"), MakeEquipment("B"));
            var panel = MakeOffersPanel();

            Bind(panel, MakeTilePrefab()).Show();

            Assert.AreEqual(2, panel.childCount, "With two items available the shop offers both.");
        }

        [Test]
        public void Attach_Show_WritesTitleAndDescriptionIntoSuppliedLabels()
        {
            MakePool(MakeEquipment("Sword"));
            var panel = MakeOffersPanel();
            var title = MakeLabel("Special Title");
            var description = MakeLabel("Special Description");

            ShopScreen screen = ShopScreen.Attach(panel, MakeTilePrefab(), title, description);
            screen.Show();

            Assert.IsFalse(string.IsNullOrEmpty(title.text),
                "The shop must write its heading into the Special page's title label.");
            Assert.IsFalse(string.IsNullOrEmpty(description.text),
                "The shop must write a description into the Special page's description label.");
        }

        [Test]
        public void Pick_UnlocksChosenItemAndPersists_WithoutAnyGold()
        {
            var sword = MakeEquipment("Sword");
            MakePool(sword, MakeEquipment("B"), MakeEquipment("C"));
            var panel = MakeOffersPanel();
            Bind(panel, MakeTilePrefab()).Show();

            int swordIndex = TileItemNames(panel).IndexOf("Sword");
            PickButton(panel, swordIndex).onClick.Invoke();

            Assert.IsTrue(inventory.IsUnlocked(sword), "Picking an offer must give the player that item.");
            Assert.AreEqual(1, savedCount, "Picking must persist the inventory.");
            Assert.AreEqual("Chosen", TileLabel(panel, swordIndex).text,
                "The chosen tile must say it was chosen.");
        }

        [Test]
        public void Pick_LocksTheOtherOffers_AndDoesNotReshuffle()
        {
            MakePool(MakeEquipment("A"), MakeEquipment("B"), MakeEquipment("C"));
            var panel = MakeOffersPanel();
            Bind(panel, MakeTilePrefab()).Show();
            var before = TileItemNames(panel);

            PickButton(panel, 0).onClick.Invoke();

            CollectionAssert.AreEqual(before, TileItemNames(panel),
                "Picking must not replace or reshuffle the offers.");
            Assert.AreEqual(3, panel.childCount, "No replacement offer may slide in after a pick.");
            for (int i = 0; i < panel.childCount; i++)
            {
                Assert.IsFalse(PickButton(panel, i).interactable,
                    $"Offer {i} must be locked once one offer has been taken.");
                float expected = i == 0 ? 1f : 0.35f;
                Assert.AreEqual(expected, panel.GetChild(i).GetComponent<CanvasGroup>().alpha, 0.001f,
                    i == 0 ? "The taken offer stays opaque." : "Offers that were not taken must lose opacity.");
            }
        }

        [Test]
        public void Pick_OnlyOnce_SecondPickIsIgnored()
        {
            var first = MakeEquipment("A");
            var second = MakeEquipment("B");
            MakePool(first, second);
            var panel = MakeOffersPanel();
            Bind(panel, MakeTilePrefab()).Show();

            PickButton(panel, 0).onClick.Invoke();
            PickButton(panel, 1).onClick.Invoke();

            Assert.IsTrue(inventory.IsUnlocked(first));
            Assert.IsFalse(inventory.IsUnlocked(second), "Only one offer may be taken per shop.");
            Assert.AreEqual(1, savedCount, "The second pick must not persist anything.");
        }

        [Test]
        public void NoOffers_LeavesPanelEmpty()
        {
            var panel = MakeOffersPanel();

            Bind(panel, MakeTilePrefab()).Show();

            Assert.AreEqual(0, panel.childCount, "An empty pool must show no shop tiles.");
        }

        [Test]
        public void ShopTile_RightClick_ShowsEquipmentTooltip()
        {
            var sword = MakeEquipment("Sword");
            MakePool(sword);
            var panel = MakeOffersPanel();
            Bind(panel, MakeTilePrefab()).Show();

            Transform tile = panel.GetChild(0);
            var slot = tile.GetComponent<InventorySlotUI>();
            Assert.IsFalse(slot.Interactable, "Shop tiles stay non-draggable.");
            Assert.IsNotNull(tile.GetComponent<Button>(),
                "The pick button belongs on the tile itself so right-clicks still reach the slot.");

            var tooltipGo = Track(new GameObject("Tooltip", typeof(Tooltip))) as GameObject;
            var tooltip = tooltipGo.GetComponent<Tooltip>();
            typeof(Tooltip).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, tooltip);
            typeof(Tooltip).GetMethod("CreateUI", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(tooltip, null);

            var click = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Right
            };
            ((IPointerClickHandler)slot).OnPointerClick(click);

            bool showsName = false;
            foreach (var tmp in tooltipGo.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp.text == "Sword") showsName = true;
            }
            Assert.IsTrue(showsName, "Right-clicking a shop tile must show the offered item's tooltip.");
        }

        [Test]
        public void Show_OffersAreStablePerRunSeed_ButChangeWithNewRun()
        {
            MakePool(
                MakeEquipment("A"),
                MakeEquipment("B"),
                MakeEquipment("C"),
                MakeEquipment("D"),
                MakeEquipment("E"));
            var panel = MakeOffersPanel();
            var prefab = MakeTilePrefab();

            GameState.CurrentLevel = 1;
            GameState.ShopSeed = 111;
            ShopScreen screen = Bind(panel, prefab);
            screen.Show();
            var first = TileItemNames(panel);
            Assert.AreEqual(3, first.Count, "The shop fills its offer count.");

            screen.Show();
            CollectionAssert.AreEqual(first, TileItemNames(panel),
                "Reopening the shop in the same run+level must not reshuffle the offers.");

            GameState.ShopSeed = 222;
            screen.Show();
            CollectionAssert.AreNotEqual(first, TileItemNames(panel),
                "A new run must roll a different shop.");
        }
    }
}
