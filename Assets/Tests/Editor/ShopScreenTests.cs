using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cards;
using Equipment;
using UI;

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
            inventory = Track(ScriptableObject.CreateInstance<PlayerInventory>()) as PlayerInventory;
            ShopScreen.LocalInventory = () => inventory;
            ShopScreen.PersistInventory = _ => savedCount++;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var screen in Object.FindObjectsByType<ShopScreen>(FindObjectsSortMode.None))
            {
                if (screen != null) Object.DestroyImmediate(screen.gameObject);
            }
            ShopScreen.LocalInventory = PlayerInventoryProvider.GetLocal;
            ShopScreen.PersistInventory = inv =>
                PlayerInventoryProvider.Save(PlayerInventoryProvider.LocalPlayerKey, inv);
            ShopPool.SetMain(null);
            RewardPool.SetMain(null);
            PlayerInventoryProvider.ResetForTests();
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

        private ShopPool MakePool(params ShopEntry[] entries)
        {
            var pool = Track(ScriptableObject.CreateInstance<ShopPool>()) as ShopPool;
            pool.stock.AddRange(entries);
            ShopPool.SetMain(pool);
            return pool;
        }

        private static ShopEntry Entry(EquipmentData item, int price) => new ShopEntry { item = item, price = price };

        private static GameObject CanvasRoot()
        {
            var screen = Object.FindAnyObjectByType<ShopScreen>();
            if (screen == null) return null;
            Transform canvas = screen.transform.Find("ShopCanvas");
            return canvas != null && canvas.gameObject.activeSelf ? canvas.gameObject : null;
        }

        private static TMP_Text FindGoldLabel(GameObject canvas)
        {
            foreach (var text in canvas.GetComponentsInChildren<TMP_Text>())
            {
                if (text.text.StartsWith("Gold:")) return text;
            }
            return null;
        }

        private static Button FindButton(GameObject canvas, string name)
        {
            foreach (var button in canvas.GetComponentsInChildren<Button>())
            {
                if (button.gameObject.name == name) return button;
            }
            return null;
        }

        [Test]
        public void Show_CreatesShopWithGoldAndOffers()
        {
            inventory.AddGold(50);
            MakePool(Entry(MakeEquipment("Sword"), 10));

            ShopScreen.Show(() => { });

            GameObject canvas = CanvasRoot();
            Assert.IsNotNull(canvas, "Show must create the shop canvas.");
            Assert.IsNotNull(FindGoldLabel(canvas), "Shop must display the player's gold.");
            Assert.AreEqual("Gold: 50", FindGoldLabel(canvas).text);
            Assert.IsNotNull(FindButton(canvas, "BuyButton"), "Shop must offer at least one item to buy.");
            Assert.IsNotNull(FindButton(canvas, "ContinueButton"), "Shop must let the player continue.");
        }

        [Test]
        public void Buy_Affordable_UnlocksItemAndDeductsGold()
        {
            inventory.AddGold(50);
            var sword = MakeEquipment("Sword");
            MakePool(Entry(sword, 10));

            ShopScreen.Show(() => { });
            GameObject canvas = CanvasRoot();
            FindButton(canvas, "BuyButton").onClick.Invoke();

            Assert.IsTrue(inventory.IsUnlocked(sword), "Buying must unlock the item.");
            Assert.AreEqual(40, inventory.Gold, "Buying must deduct the item's price.");
            Assert.AreEqual(1, savedCount, "Buying must persist the inventory.");
        }

        [Test]
        public void Buy_TooExpensive_DoesNothing()
        {
            inventory.AddGold(5);
            var sword = MakeEquipment("Sword");
            MakePool(Entry(sword, 10));

            ShopScreen.Show(() => { });
            GameObject canvas = CanvasRoot();
            FindButton(canvas, "BuyButton").onClick.Invoke();

            Assert.IsFalse(inventory.IsUnlocked(sword), "An unaffordable item must not be unlocked.");
            Assert.AreEqual(5, inventory.Gold, "An unaffordable purchase must not change the balance.");
            Assert.AreEqual(0, savedCount, "A failed purchase must not persist.");
        }

        [Test]
        public void Continue_ClosesShopAndInvokesCallback()
        {
            bool closed = false;
            MakePool(Entry(MakeEquipment("Sword"), 10));

            ShopScreen.Show(() => closed = true);
            GameObject canvas = CanvasRoot();
            FindButton(canvas, "ContinueButton").onClick.Invoke();

            Assert.IsTrue(closed, "Continue must run the close callback.");
            Assert.IsNull(Object.FindAnyObjectByType<ShopScreen>(), "Continue must destroy the shop overlay.");
        }
    }
}
