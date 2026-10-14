using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Equipment;
using Game;
using UI;

namespace Tests.UI
{
    [TestFixture]
    public class EnchantScreenTests
    {
        private readonly List<Object> tracked = new List<Object>();

        private PlayerInventory inventory;
        private GameDataLibrary library;
        private int savedCount;

        [SetUp]
        public void SetUp()
        {
            GameState.CurrentLevel = 1;
            GameState.ShopSeed = 4242;

            inventory = Track(ScriptableObject.CreateInstance<PlayerInventory>()) as PlayerInventory;
            library = Track(ScriptableObject.CreateInstance<GameDataLibrary>()) as GameDataLibrary;
            GameDataLibrary.SetInstance(library);

            EnchantScreen.LocalInventory = () => inventory;
            EnchantScreen.PersistInventory = _ => savedCount++;
            savedCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            GameState.ShopSeed = 0;
            foreach (var screen in Object.FindObjectsByType<EnchantScreen>(FindObjectsSortMode.None))
            {
                if (screen != null) Object.DestroyImmediate(screen);
            }
            EnchantScreen.LocalInventory = PlayerInventoryProvider.GetLocal;
            InventorySlotUI.LocalInventory = PlayerInventoryProvider.GetLocal;
            EnchantScreen.PersistInventory = inv =>
                PlayerInventoryProvider.Save(PlayerInventoryProvider.LocalPlayerKey, inv);
            GameDataLibrary.SetInstance(null);
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

        private EquipmentEnchantmentSO MakeEnchantment(string name, string description = "")
        {
            var so = Track(ScriptableObject.CreateInstance<GrantCardEnchantmentSO>()) as EquipmentEnchantmentSO;
            so.EnchantmentName = name;
            so.Description = description;
            library.allEnchantments.Add(so);
            return so;
        }

        private RectTransform MakeOffersPanel()
        {
            var go = Track(new GameObject("Special", typeof(RectTransform), typeof(GridLayoutGroup))) as GameObject;
            return go.GetComponent<RectTransform>();
        }

        private GameObject MakeTilePrefab()
        {
            return Track(new GameObject("Inventory Slot", typeof(RectTransform), typeof(InventorySlotUI))) as GameObject;
        }

        private void Own(params EquipmentData[] items)
        {
            inventory.SetUnlocked(new List<EquipmentData>(items));
            library.allEquipment.AddRange(items);
        }

        private static int TileCount(Transform panel)
        {
            int count = 0;
            for (int i = 0; i < panel.childCount; i++)
            {
                if (panel.GetChild(i).GetComponent<InventorySlotUI>() != null) count++;
            }
            return count;
        }

        private static Transform TileAt(Transform panel, int index)
        {
            int seen = 0;
            for (int i = 0; i < panel.childCount; i++)
            {
                Transform child = panel.GetChild(i);
                if (child.GetComponent<InventorySlotUI>() == null) continue;
                if (seen == index) return child;
                seen++;
            }
            return null;
        }

        private static TMP_Text StatusOf(Transform tile) => tile.Find("Status").GetComponent<TMP_Text>();

        private static TMP_Text TitleOf(Transform panel)
        {
            Transform info = panel.Find("EnchantInfo");
            return info != null ? info.Find("Title").GetComponent<TMP_Text>() : null;
        }

        private static TMP_Text DescriptionOf(Transform panel)
        {
            Transform info = panel.Find("EnchantInfo");
            return info != null ? info.Find("Description").GetComponent<TMP_Text>() : null;
        }

        [Test]
        public void Attach_Show_PopulatesEnchantmentInfoAndOptionTiles()
        {
            var sword = MakeEquipment("Sword");
            var shield = MakeEquipment("Shield");
            Own(sword, shield);
            MakeEnchantment("Sharpened", "Adds damage.");
            var panel = MakeOffersPanel();
            var prefab = MakeTilePrefab();

            EnchantScreen screen = EnchantScreen.Attach(panel, prefab);
            screen.Show();

            Assert.AreEqual(2, screen.Options.Count, "Both owned pieces are offered.");
            Assert.IsNotNull(TitleOf(panel), "Showing the station must build the info header.");
            Assert.AreEqual("Enchant", TitleOf(panel).text, "The title must say to enchant.");
            Assert.AreEqual("Sharpened: Adds damage.", DescriptionOf(panel).text, "The description must show the enchantment as Name: Desc.");
            Assert.AreEqual(2, TileCount(panel), "One tile per equipment option.");
            var slot = TileAt(panel, 0).GetComponent<InventorySlotUI>();
            Assert.IsNotNull(slot, "Option tiles reuse the Inventory Slot prefab.");
            Assert.IsFalse(slot.Interactable, "Enchantment option tiles must not be draggable.");
        }

        [Test]
        public void Attach_Show_WithExternalLabels_WritesThemAndSkipsGeneratedHeader()
        {
            var sword = MakeEquipment("Sword");
            Own(sword);
            MakeEnchantment("Sharpened", "Adds damage.");
            var panel = MakeOffersPanel();
            var prefab = MakeTilePrefab();

            var title = Track(new GameObject("Special Title", typeof(TextMeshProUGUI))) as GameObject;
            var description = Track(new GameObject("Special Description", typeof(TextMeshProUGUI))) as GameObject;

            EnchantScreen screen = EnchantScreen.Attach(panel, prefab,
                title.GetComponent<TMP_Text>(), description.GetComponent<TMP_Text>());
            screen.Show();

            Assert.AreEqual("Enchant", title.GetComponent<TMP_Text>().text,
                "The station must write the enchant heading into the scene's Special Title.");
            Assert.AreEqual("Sharpened: Adds damage.", description.GetComponent<TMP_Text>().text,
                "The station must write the enchantment name into the scene's Special Description.");
            Assert.IsNull(panel.Find("EnchantInfo"),
                "With scene labels supplied the station must not build its own info header.");
        }

        [Test]
        public void ChooseTile_AppliesEnchantmentPersistsAndLocksIn()
        {
            var sword = MakeEquipment("Sword");
            Own(sword);
            var enchantment = MakeEnchantment("Sharpened", "Adds damage.");
            var panel = MakeOffersPanel();
            var prefab = MakeTilePrefab();

            EnchantScreen screen = EnchantScreen.Attach(panel, prefab);
            screen.Show();

            TileAt(panel, 0).GetComponent<Button>().onClick.Invoke();

            CollectionAssert.Contains(
                new List<EquipmentEnchantmentSO>(inventory.GetEnchantments(sword)), enchantment,
                "Choosing a tile must apply the offered enchantment to that piece.");
            Assert.AreEqual(1, savedCount, "Applying an enchantment must persist the inventory.");
            Assert.IsTrue(screen.HasApplied);
            Assert.AreEqual("Enchanted", StatusOf(TileAt(panel, 0)).text);
            Assert.IsFalse(TileAt(panel, 0).GetComponent<Button>().interactable,
                "Once enchanted, the offer is spent for this level.");
        }

        [Test]
        public void ChooseTile_FadesTheOtherOptions_AndKeepsChosenOpaque()
        {
            var sword = MakeEquipment("Sword");
            var shield = MakeEquipment("Shield");
            Own(sword, shield);
            MakeEnchantment("Sharpened", "Adds damage.");
            var panel = MakeOffersPanel();

            EnchantScreen screen = EnchantScreen.Attach(panel, MakeTilePrefab());
            screen.Show();
            Assert.AreEqual(1f, TileAt(panel, 0).GetComponent<CanvasGroup>()?.alpha ?? 1f,
                "Before any choice every option is fully opaque.");

            TileAt(panel, 0).GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(1f, TileAt(panel, 0).GetComponent<CanvasGroup>().alpha,
                "The enchanted option stays opaque.");
            Assert.AreEqual(0.35f, TileAt(panel, 1).GetComponent<CanvasGroup>().alpha, 0.001f,
                "The other option must lose opacity once one has been enchanted.");
            Assert.AreEqual("Enchanted", StatusOf(TileAt(panel, 0)).text);
        }

        [Test]
        public void Options_ExcludeAlreadyEnchantedEquipment()
        {
            var sword = MakeEquipment("Sword");
            var shield = MakeEquipment("Shield");
            Own(sword, shield);
            inventory.AddEnchantment(sword, MakeEnchantment("Sharpened", "Adds damage."));
            MakeEnchantment("Warded", "Blocks more.");
            var panel = MakeOffersPanel();

            EnchantScreen screen = EnchantScreen.Attach(panel, MakeTilePrefab());
            screen.Show();

            CollectionAssert.DoesNotContain(new List<EquipmentData>(screen.Options), sword,
                "An equipment piece can only be enchanted once, so an enchanted one is not offered.");
            Assert.AreEqual(1, TileCount(panel));
        }

        [Test]
        public void ChooseTile_CannotEnchantAPieceTwice_EvenWhenShownBeforeEnchanted()
        {
            var sword = MakeEquipment("Sword");
            var shield = MakeEquipment("Shield");
            Own(sword, shield);
            MakeEnchantment("Sharpened", "Adds damage.");
            var panel = MakeOffersPanel();

            EnchantScreen screen = EnchantScreen.Attach(panel, MakeTilePrefab());
            screen.Show();
            int swordIndex = -1;
            for (int i = 0; i < screen.Options.Count; i++)
                if (screen.Options[i] == sword) swordIndex = i;
            Assert.AreNotEqual(-1, swordIndex, "Precondition: the sword is on screen.");

            // Enchanted elsewhere (e.g. another path) while the tile is still on screen.
            inventory.AddEnchantment(sword, screen.Enchantment);
            TileAt(panel, swordIndex).GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(1, inventory.GetEnchantments(sword).Count,
                "A piece that already carries an enchantment must refuse another one.");
            Assert.AreEqual(0, savedCount, "Refused enchantments must not persist.");
        }

        [Test]
        public void ChooseTile_Twice_AppliesOnlyOnce()
        {
            var sword = MakeEquipment("Sword");
            Own(sword);
            MakeEnchantment("Sharpened", "Adds damage.");
            var panel = MakeOffersPanel();
            var prefab = MakeTilePrefab();

            EnchantScreen screen = EnchantScreen.Attach(panel, prefab);
            screen.Show();

            Button choose = TileAt(panel, 0).GetComponent<Button>();
            choose.onClick.Invoke();
            choose.onClick.Invoke();

            Assert.AreEqual(1, inventory.GetEnchantments(sword).Count,
                "Only one enchantment may be applied per level.");
            Assert.AreEqual(1, savedCount, "A repeat click must not persist again.");
        }

        [Test]
        public void Options_IncludeEquippedAndBag_Deduped()
        {
            var equipped = MakeEquipment("Equipped Axe");
            var bagged = MakeEquipment("Bagged Ring");
            Own(equipped, bagged, equipped); // equipped also appears in the unlocked list
            inventory.Loadout.Set(EquipmentSlotType.Hand, 0, equipped);
            MakeEnchantment("Sharpened");
            var panel = MakeOffersPanel();
            var prefab = MakeTilePrefab();

            EnchantScreen screen = EnchantScreen.Attach(panel, prefab);
            screen.Show();

            Assert.AreEqual(2, screen.Options.Count, "Equipped + bagged, deduped by reference.");
            Assert.Contains(equipped, new List<EquipmentData>(screen.Options));
            Assert.Contains(bagged, new List<EquipmentData>(screen.Options));
        }

        [Test]
        public void Options_CappedAtThree()
        {
            Own(MakeEquipment("A"), MakeEquipment("B"), MakeEquipment("C"),
                MakeEquipment("D"), MakeEquipment("E"));
            MakeEnchantment("Sharpened");
            var panel = MakeOffersPanel();
            var prefab = MakeTilePrefab();

            EnchantScreen screen = EnchantScreen.Attach(panel, prefab);
            screen.Show();

            Assert.AreEqual(3, screen.Options.Count, "At most three options are offered.");
            Assert.AreEqual(3, TileCount(panel));
        }

        [Test]
        public void NoEquipment_ShowsInfoButNoTiles()
        {
            MakeEnchantment("Sharpened");
            var panel = MakeOffersPanel();
            var prefab = MakeTilePrefab();

            EnchantScreen screen = EnchantScreen.Attach(panel, prefab);
            screen.Show();

            Assert.AreEqual(0, screen.Options.Count);
            Assert.AreEqual(0, TileCount(panel), "No equipment means no option tiles.");
            Assert.AreEqual("Enchant", TitleOf(panel).text, "The enchant heading is still shown.");
        }

        [Test]
        public void EmptyPool_ShowsNoEnchantmentsAvailable()
        {
            Own(MakeEquipment("Sword"));
            var panel = MakeOffersPanel();
            var prefab = MakeTilePrefab();

            EnchantScreen screen = EnchantScreen.Attach(panel, prefab);
            screen.Show();

            Assert.IsNull(screen.Enchantment);
            Assert.AreEqual("No enchantments available", TitleOf(panel).text);
            Assert.AreEqual(0, TileCount(panel));
        }

        [Test]
        public void Show_SameSeed_ProducesSameOptions()
        {
            Own(MakeEquipment("A"), MakeEquipment("B"), MakeEquipment("C"), MakeEquipment("D"));
            MakeEnchantment("Sharpened");

            var panelA = MakeOffersPanel();
            var screenA = EnchantScreen.Attach(panelA, MakeTilePrefab());
            screenA.Show();

            var panelB = MakeOffersPanel();
            var screenB = EnchantScreen.Attach(panelB, MakeTilePrefab());
            screenB.Show();

            CollectionAssert.AreEqual(
                new List<EquipmentData>(screenA.Options),
                new List<EquipmentData>(screenB.Options),
                "The same run+level seed must offer the same options.");
        }

        [Test]
        public void Show_Reopening_DoesNotReshuffleOptions()
        {
            Own(MakeEquipment("A"), MakeEquipment("B"), MakeEquipment("C"), MakeEquipment("D"));
            MakeEnchantment("Sharpened");
            var panel = MakeOffersPanel();

            EnchantScreen screen = EnchantScreen.Attach(panel, MakeTilePrefab());
            screen.Show();
            var first = new List<EquipmentData>(screen.Options);

            screen.Show();

            CollectionAssert.AreEqual(first, new List<EquipmentData>(screen.Options),
                "Reopening the station within the same run+level must not reshuffle the offer.");
        }

        [Test]
        public void RightClick_Tooltip_ShowsEnchantmentAsNameColonDescription()
        {
            var sword = MakeEquipment("Sword");
            Own(sword);
            inventory.AddEnchantment(sword, MakeEnchantment("Sharpened", "Adds damage."));
            InventorySlotUI.LocalInventory = () => inventory;

            var slotGo = Track(new GameObject("Slot", typeof(RectTransform), typeof(Image), typeof(InventorySlotUI))) as GameObject;
            var slot = slotGo.GetComponent<InventorySlotUI>();
            slot.Setup(sword, null);

            var tooltipGo = Track(new GameObject("Tooltip", typeof(Tooltip))) as GameObject;
            var tooltip = tooltipGo.GetComponent<Tooltip>();
            typeof(Tooltip).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, tooltip);
            typeof(Tooltip).GetMethod("CreateUI", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(tooltip, null);

            slot.OnPointerClick(new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Right
            });

            bool shown = false;
            foreach (var tmp in tooltipGo.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp.text == "Sharpened: Adds damage.") shown = true;
            }
            Assert.IsTrue(shown, "Right-clicking an enchanted piece must list its enchantment as Name: Desc.");
        }

        [Test]
        public void Hide_ClearsTilesAndInfo()
        {
            Own(MakeEquipment("Sword"));
            MakeEnchantment("Sharpened");
            var panel = MakeOffersPanel();

            EnchantScreen screen = EnchantScreen.Attach(panel, MakeTilePrefab());
            screen.Show();
            Assert.AreEqual(1, TileCount(panel));

            screen.Hide();

            Assert.AreEqual(0, TileCount(panel));
            Assert.IsNull(panel.Find("EnchantInfo"));
        }

        [Test]
        public void Show_NoInventory_DoesNotThrow()
        {
            EnchantScreen.LocalInventory = () => null;
            var panel = MakeOffersPanel();

            EnchantScreen screen = EnchantScreen.Attach(panel, MakeTilePrefab());

            Assert.DoesNotThrow(() => screen.Show());
            Assert.AreEqual(0, TileCount(panel));
        }

        [Test]
        public void Attach_NullPanel_ReturnsNull()
        {
            Assert.IsNull(EnchantScreen.Attach(null, MakeTilePrefab()));
        }
    }
}
