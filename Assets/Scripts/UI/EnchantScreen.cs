using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game;
using Equipment;

namespace UI
{
    /// <summary>
    /// End-of-level enchantment station. On some levels the "Special" column of the equipment
    /// panel shows this instead of <see cref="ShopScreen"/>: one random enchantment (with its
    /// description) and up to a few of the player's own equipment pieces — equipped or in the
    /// bag — as options. Only pieces the enchantment's targets allow are offered (see
    /// <see cref="EquipmentEnchantmentSO.RestrictionFor"/>): e.g. a chest-only enchantment never
    /// shows a pair of boots. If no enchantment in the pool fits any owned piece, none is offered.
    /// Picking one applies the enchantment to that piece for the rest of the run (several
    /// enchantments may stack on the same piece).
    ///
    /// Offers are seeded per run+level so they are stable for that window and only change for a
    /// new run/level, and each machine rolls its own — the host never sees another player's
    /// inventory or offers.
    /// </summary>
    public class EnchantScreen : MonoBehaviour
    {
        [Header("Offers")]
        [SerializeField] [Min(0)] private int optionCount = 3;

        [Header("Info Panel")]
        [Tooltip("Title shown while an enchantment is on offer.")]
        [SerializeField] private string enchantTitle = "Enchant";
        [Tooltip("Title shown when no enchantment is offered (empty pool), and as the fallback " +
            "when an external title label is supplied but no enchantment exists.")]
        [SerializeField] private string defaultTitle = "No enchantments available";
        [Tooltip("Description shown when no enchantment is offered.")]
        [SerializeField] private string defaultDescription = "";
        [SerializeField] private float infoHeight = 96f;
        [SerializeField] private int titleFontSize = 22;
        [SerializeField] private int descriptionFontSize = 15;
        [SerializeField] private Color titleColor = new Color(1f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color descriptionColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        [Header("Tiles")]
        [SerializeField] private int statusFontSize = 16;
        [SerializeField] private Color enchantedColor = new Color(1f, 0.85f, 0.3f, 1f);
        [Tooltip("Opacity of the options that were not enchanted once one has been chosen.")]
        [SerializeField] [Range(0f, 1f)] private float dimmedAlpha = 0.35f;

        /// <summary>Test seam: resolves the local player's inventory.</summary>
        internal static System.Func<PlayerInventory> LocalInventory = PlayerInventoryProvider.GetLocal;

        /// <summary>Test seam: persists the local player's inventory after enchanting.</summary>
        internal static System.Action<PlayerInventory> PersistInventory = inventory =>
            PlayerInventoryProvider.Save(PlayerInventoryProvider.LocalPlayerKey, inventory);

        private Transform offersPanel;
        private GameObject tilePrefab;
        private PlayerInventory currentInventory;

        private GameObject infoRoot;
        private TMP_Text titleLabel;
        private TMP_Text descriptionLabel;
        private TMP_Text externalTitleLabel;
        private TMP_Text externalDescriptionLabel;

        private readonly List<EquipmentData> options = new List<EquipmentData>();
        private readonly List<TileEntry> tiles = new List<TileEntry>();

        private EquipmentEnchantmentSO enchantment;
        private EquipmentData appliedEquipment;

        private bool offersBuilt;
        private int offersSeed;
        private PlayerInventory offersInventory;

        private GridLayoutGroup grid;
        private RectOffset gridPadding;

        /// <summary>The enchantment offered this level (null while hidden).</summary>
        public EquipmentEnchantmentSO Enchantment => enchantment;

        /// <summary>The equipment options offered this level.</summary>
        public IReadOnlyList<EquipmentData> Options => options;

        /// <summary>True once the player has enchanted a piece this level.</summary>
        public bool HasApplied => appliedEquipment != null;

        /// <summary>A spawned option tile plus its widgets.</summary>
        private class TileEntry
        {
            public GameObject Root;
            public EquipmentData Equipment;
            public TMP_Text Status;
            public Button Choose;
        }

        /// <summary>
        /// Binds an enchantment station to an existing panel (the Equipment UI "Special"
        /// pane). Returns null when the panel is missing.
        /// </summary>
        public static EnchantScreen Attach(Transform offersPanel, GameObject tilePrefab,
            TMP_Text titleLabel = null, TMP_Text descriptionLabel = null)
        {
            if (offersPanel == null) return null;

            EnchantScreen screen = offersPanel.GetComponent<EnchantScreen>();
            if (screen == null) screen = offersPanel.gameObject.AddComponent<EnchantScreen>();
            screen.Setup(offersPanel, tilePrefab, titleLabel, descriptionLabel);
            return screen;
        }

        private void Setup(Transform offersPanel, GameObject tilePrefab,
            TMP_Text titleLabel, TMP_Text descriptionLabel)
        {
            this.offersPanel = offersPanel;
            this.tilePrefab = tilePrefab;
            externalTitleLabel = titleLabel;
            externalDescriptionLabel = descriptionLabel;
        }

        private void OnDestroy()
        {
            if (currentInventory != null)
                currentInventory.OnInventoryChanged -= Refresh;
            RestoreGridPadding();
        }

        /// <summary>Fills the panel with the offer and keeps it in sync until hidden.</summary>
        public void Show()
        {
            var inventory = LocalInventory();
            if (inventory != null && inventory != currentInventory)
            {
                if (currentInventory != null) currentInventory.OnInventoryChanged -= Refresh;
                currentInventory = inventory;
                currentInventory.OnInventoryChanged += Refresh;
            }
            Refresh();
        }

        /// <summary>Unsubscribes and empties the panel.</summary>
        public void Hide()
        {
            if (currentInventory != null)
            {
                currentInventory.OnInventoryChanged -= Refresh;
                currentInventory = null;
            }
            ClearTiles();
            if (infoRoot != null) { DestroyObject(infoRoot); infoRoot = null; }
            titleLabel = null;
            descriptionLabel = null;
            RestoreGridPadding();
        }

        private void Refresh()
        {
            if (EnsureOffer() || tiles.Count == 0)
                Populate();
            else
                UpdateTileStates();
        }

        /// <summary>
        /// Rolls the offer once per run+level (seeded so it is stable within that window and
        /// only changes for a new run/level). Returns true when the offer was (re)built.
        /// </summary>
        private bool EnsureOffer()
        {
            int seed = unchecked(GameState.ShopSeed + GameState.CurrentLevel);
            if (offersBuilt && offersSeed == seed && offersInventory == currentInventory)
                return false;

            offersBuilt = true;
            offersSeed = seed;
            offersInventory = currentInventory;

            enchantment = null;
            appliedEquipment = null;
            options.Clear();

            if (currentInventory == null) return true;

            var rng = new System.Random(seed);

            var library = GameDataLibrary.Instance;
            var pool = library != null ? library.GetAllEnchantments() : null;
            var candidates = CollectEquipment(currentInventory);

            // Offer an enchantment that has at least one piece it is allowed on. Enchantments are
            // tried in a random order, so one whose targets rule out every piece is passed over.
            if (pool != null)
            {
                var order = new List<EquipmentEnchantmentSO>();
                foreach (EquipmentEnchantmentSO candidate in pool)
                {
                    if (candidate != null) order.Add(candidate);
                }
                Shuffle(order, rng);

                foreach (EquipmentEnchantmentSO candidate in order)
                {
                    List<EquipmentData> eligible = EligibleFor(candidate, candidates, currentInventory);
                    if (eligible.Count == 0) continue;

                    enchantment = candidate;
                    Shuffle(eligible, rng);
                    for (int i = 0; i < eligible.Count && options.Count < optionCount; i++)
                        options.Add(eligible[i]);
                    break;
                }
            }

            return true;
        }

        private void Populate()
        {
            ClearTiles();

            if (offersPanel == null) return;

            EnsureInfo();

            if (tilePrefab != null)
            {
                foreach (EquipmentData equipment in options)
                    CreateTile(equipment);
            }

            UpdateTileStates();
        }

        private void EnsureInfo()
        {
            if (titleLabel != null || descriptionLabel != null) return;

            // Prefer the scene's own Special page title/description when one was supplied;
            // only build a generated header as a fallback (e.g. unit tests, legacy scenes).
            if (externalTitleLabel != null || externalDescriptionLabel != null)
            {
                titleLabel = externalTitleLabel;
                descriptionLabel = externalDescriptionLabel;
                return;
            }

            if (infoRoot != null) return;

            ApplyGridPadding();

            infoRoot = new GameObject("EnchantInfo", typeof(RectTransform), typeof(LayoutElement));
            infoRoot.transform.SetParent(offersPanel, false);

            var rect = infoRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0, infoHeight);

            // Keep the grid layout from positioning the info block as a cell.
            infoRoot.GetComponent<LayoutElement>().ignoreLayout = true;

            var titleObj = new GameObject("Title", typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(infoRoot.transform, false);
            titleLabel = titleObj.GetComponent<TextMeshProUGUI>();
            titleLabel.fontSize = titleFontSize;
            titleLabel.alignment = TextAlignmentOptions.Center;
            titleLabel.color = titleColor;
            titleLabel.rectTransform.anchorMin = new Vector2(0, 1);
            titleLabel.rectTransform.anchorMax = new Vector2(1, 1);
            titleLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleLabel.rectTransform.anchoredPosition = Vector2.zero;
            titleLabel.rectTransform.sizeDelta = new Vector2(0, titleFontSize + 8);

            var descObj = new GameObject("Description", typeof(TextMeshProUGUI));
            descObj.transform.SetParent(infoRoot.transform, false);
            descriptionLabel = descObj.GetComponent<TextMeshProUGUI>();
            descriptionLabel.fontSize = descriptionFontSize;
            descriptionLabel.alignment = TextAlignmentOptions.Top;
            descriptionLabel.color = descriptionColor;
            descriptionLabel.textWrappingMode = TextWrappingModes.Normal;
            descriptionLabel.rectTransform.anchorMin = new Vector2(0, 0);
            descriptionLabel.rectTransform.anchorMax = new Vector2(1, 1);
            descriptionLabel.rectTransform.offsetMin = new Vector2(0, 0);
            descriptionLabel.rectTransform.offsetMax = new Vector2(0, -(titleFontSize + 8));
        }

        private void ApplyGridPadding()
        {
            if (grid == null)
            {
                grid = offersPanel.GetComponent<GridLayoutGroup>();
                if (grid != null)
                    gridPadding = new RectOffset(grid.padding.left, grid.padding.right, grid.padding.top, grid.padding.bottom);
            }
            if (grid != null)
                grid.padding = new RectOffset(gridPadding.left, gridPadding.right, Mathf.RoundToInt(infoHeight), gridPadding.bottom);
        }

        private void RestoreGridPadding()
        {
            if (grid != null && gridPadding != null)
                grid.padding = new RectOffset(gridPadding.left, gridPadding.right, gridPadding.top, gridPadding.bottom);
        }

        private void CreateTile(EquipmentData equipment)
        {
            GameObject root = Object.Instantiate(tilePrefab, offersPanel);
            root.name = $"Enchant {equipment.EquipmentName}";

            var slot = root.GetComponent<InventorySlotUI>();
            if (slot != null)
            {
                slot.Interactable = false;
                slot.Setup(equipment, null);
            }

            var entry = new TileEntry
            {
                Root = root,
                Equipment = equipment,
                Status = CreateStatusLabel(root.transform),
            };
            entry.Choose = AddChooseOverlay(root.transform, entry);
            tiles.Add(entry);
        }

        private TMP_Text CreateStatusLabel(Transform tile)
        {
            var statusObj = new GameObject("Status", typeof(TextMeshProUGUI));
            statusObj.transform.SetParent(tile, false);
            var tmp = statusObj.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = statusFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = enchantedColor;
            var rt = tmp.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -2);
            rt.sizeDelta = new Vector2(0, 22);
            return tmp;
        }

        private Button AddChooseOverlay(Transform tile, TileEntry entry)
        {
            var overlay = new GameObject("Choose", typeof(Image));
            overlay.transform.SetParent(tile, false);
            var image = overlay.GetComponent<Image>();
            image.color = new Color(1, 1, 1, 0.08f);

            var rt = overlay.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => Choose(entry));
            return button;
        }

        private void Choose(TileEntry tile)
        {
            if (currentInventory == null || tile == null || tile.Equipment == null) return;
            if (enchantment == null || appliedEquipment != null) { UpdateTileStates(); return; }
            // Each piece can only be enchanted once, even across levels.
            if (currentInventory.GetEnchantments(tile.Equipment).Count > 0) { UpdateTileStates(); return; }
            // Belt-and-braces: the offer already filtered to allowed pieces, but refuse a pick the
            // targets rule out (e.g. the loadout changed after the offer was rolled).
            if (!enchantment.CanApplyTo(tile.Equipment, currentInventory.Loadout.ContainsItem(tile.Equipment)))
            {
                UpdateTileStates();
                return;
            }

            // Mark applied before AddEnchantment, which raises OnInventoryChanged → Refresh →
            // UpdateTileStates, so the tile flips to "Enchanted" without the offer rebuilding.
            appliedEquipment = tile.Equipment;
            if (!currentInventory.AddEnchantment(tile.Equipment, enchantment))
            {
                appliedEquipment = null;
                UpdateTileStates();
                return;
            }
            PersistInventory(currentInventory);
            UpdateTileStates();
        }

        /// <summary>Restates every tile from the current offer — no rebuild, no reshuffle.</summary>
        private void UpdateTileStates()
        {
            if (titleLabel != null)
                titleLabel.text = enchantment != null ? enchantTitle : defaultTitle;
            if (descriptionLabel != null)
            {
                if (enchantment == null)
                {
                    descriptionLabel.text = defaultDescription;
                }
                else
                {
                    string targets = enchantment.TargetSummary();
                    descriptionLabel.text = string.IsNullOrEmpty(targets)
                        ? enchantment.DisplayLine
                        : $"{enchantment.DisplayLine}\n{targets}";
                }
            }

            foreach (TileEntry tile in tiles)
            {
                if (tile == null || tile.Root == null) continue;

                bool chosen = appliedEquipment != null && tile.Equipment == appliedEquipment;
                if (tile.Choose != null)
                    tile.Choose.interactable = appliedEquipment == null;
                if (tile.Status != null)
                    tile.Status.text = chosen ? "Enchanted" : "";

                // Once one option is enchanted, the others fade out.
                SetTileAlpha(tile.Root, appliedEquipment == null || chosen ? 1f : dimmedAlpha);
            }
        }

        private static void SetTileAlpha(GameObject root, float alpha)
        {
            var group = root.GetComponent<CanvasGroup>();
            if (group == null) group = root.AddComponent<CanvasGroup>();
            group.alpha = alpha;
        }

        /// <summary>
        /// Union of the player's equipped items and bag, deduped by reference. Pieces that
        /// already carry an enchantment are left out: each piece can only be enchanted once.
        /// </summary>
        private static List<EquipmentData> CollectEquipment(PlayerInventory inventory)
        {
            var result = new List<EquipmentData>();
            if (inventory == null) return result;

            if (inventory.Loadout != null)
            {
                foreach (EquipmentData item in inventory.Loadout.GetAllItems())
                    AddEnchantable(inventory, result, item);
            }

            if (inventory.UnlockedEquipment != null)
            {
                foreach (EquipmentData item in inventory.UnlockedEquipment)
                    AddEnchantable(inventory, result, item);
            }

            return result;
        }

        /// <summary>The candidates an enchantment's targets allow, keeping the list order.</summary>
        private static List<EquipmentData> EligibleFor(EquipmentEnchantmentSO enchantment,
            List<EquipmentData> candidates, PlayerInventory inventory)
        {
            var result = new List<EquipmentData>();
            if (enchantment == null || candidates == null) return result;

            foreach (EquipmentData item in candidates)
            {
                bool equipped = inventory != null && inventory.Loadout != null && inventory.Loadout.ContainsItem(item);
                if (enchantment.CanApplyTo(item, equipped)) result.Add(item);
            }
            return result;
        }

        private static void AddEnchantable(PlayerInventory inventory, List<EquipmentData> list, EquipmentData item)
        {
            if (item == null) return;
            if (inventory.GetEnchantments(item).Count > 0) return;
            AddDistinct(list, item);
        }

        private static void AddDistinct(List<EquipmentData> list, EquipmentData item)
        {
            if (item == null) return;
            foreach (var existing in list)
            {
                if (existing == item) return;
            }
            list.Add(item);
        }

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng != null ? rng.Next(i + 1) : Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private void ClearTiles()
        {
            foreach (TileEntry tile in tiles)
            {
                if (tile != null && tile.Root != null) DestroyObject(tile.Root);
            }
            tiles.Clear();
        }

        private static void DestroyObject(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
