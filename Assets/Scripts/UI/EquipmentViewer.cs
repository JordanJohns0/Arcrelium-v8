using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Characters;
using Equipment;
using Game;

namespace UI
{
    /// <summary>
    /// Button that opens a panel showing a player's equipped items and their enchantments.
    /// The arrows step through every player at the table, starting with the local player, so
    /// anyone's gear can be checked. Built at runtime in the game scene (like the debug Kill All
    /// button), so no scene wiring is needed.
    /// </summary>
    public class EquipmentViewer : MonoBehaviour
    {
        private static readonly EquipmentSlotType[] DisplayedSlots =
        {
            EquipmentSlotType.Head, EquipmentSlotType.Chest, EquipmentSlotType.Hand,
            EquipmentSlotType.Feet, EquipmentSlotType.Trinket
        };

        private static readonly Color EnchantColor = new Color(1f, 0.85f, 0.3f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TryCreateFor(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreateFor(scene);
        }

        private static void TryCreateFor(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (FindFirstObjectByType<EquipmentViewer>() != null) return;
            if (FindFirstObjectByType<GameManager>() == null) return;
            new GameObject("Equipment Viewer", typeof(EquipmentViewer));
        }

        private readonly List<Character> players = new List<Character>();
        private int selected;
        private GameObject panel;
        private TMP_Text nameText;
        private TMP_Text bodyText;

        private void Awake()
        {
            CreateUI();
            panel.SetActive(false);
        }

        /// <summary>The text shown for one character: each equipped item, then its enchantments as "Name: Desc".</summary>
        public static string BuildEquipmentText(Character character)
        {
            if (character == null || character.Equipment == null) return "Nothing equipped.";

            var sb = new StringBuilder();
            foreach (EquipmentSlotType slot in DisplayedSlots)
            {
                foreach (EquipmentInstance instance in character.Equipment.GetEquippedInSlot(slot))
                {
                    if (instance == null || instance.Data == null) continue;

                    sb.Append("<b>").Append(SlotLabel(slot)).Append(":</b> ").Append(instance.Data.EquipmentName).Append('\n');
                    foreach (EquipmentEnchantmentSO enchantment in instance.AllEnchantments)
                    {
                        if (enchantment == null) continue;
                        sb.Append("    <color=#").Append(ColorUtility.ToHtmlStringRGB(EnchantColor)).Append('>')
                          .Append(enchantment.DisplayLine).Append("</color>\n");
                    }
                    sb.Append('\n');
                }
            }

            return sb.Length > 0 ? sb.ToString() : "Nothing equipped.";
        }

        private static string SlotLabel(EquipmentSlotType slot)
        {
            switch (slot)
            {
                case EquipmentSlotType.Head: return "Head";
                case EquipmentSlotType.Chest: return "Chest";
                case EquipmentSlotType.Hand: return "Hand";
                case EquipmentSlotType.Feet: return "Feet";
                case EquipmentSlotType.Trinket: return "Trinket";
                default: return slot.ToString();
            }
        }

        private void Open()
        {
            CollectPlayers();
            if (players.Count == 0) return;

            selected = players.FindIndex(c => c.OwnerPlayerId == GameState.LocalPlayerId);
            if (selected < 0) selected = 0;

            panel.SetActive(true);
            Refresh();
        }

        private void Close()
        {
            panel.SetActive(false);
        }

        private void Step(int direction)
        {
            if (players.Count == 0) return;
            selected = (selected + direction + players.Count) % players.Count;
            Refresh();
        }

        private void CollectPlayers()
        {
            players.Clear();
            foreach (Character c in FindObjectsByType<Character>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (c == null || c.IsStructure) continue;
                if (c.OwnerPlayerId >= GameState.EnemyOwnerPlayerId) continue;
                players.Add(c);
            }
            players.Sort((a, b) => a.OwnerPlayerId.CompareTo(b.OwnerPlayerId));
        }

        private void Refresh()
        {
            if (players.Count == 0) return;

            Character character = players[selected];
            string status = character.Stats != null && !character.Stats.IsAlive ? "  (defeated)" : "";
            nameText.text = $"{character.Stats.CharacterName}{status}   {selected + 1}/{players.Count}";
            bodyText.text = BuildEquipmentText(character);
        }

        private void CreateUI()
        {
            var canvasGo = new GameObject("EquipmentViewerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // The button that opens the panel, under the debug Kill All button in the top-left.
            MakeButton(canvasGo.transform, "Equipment", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(12f, -68f), new Vector2(210f, 48f), new Color(0.2f, 0.35f, 0.5f, 0.95f), Open);

            // The panel: header with previous / name / next / close, then the item list.
            panel = new GameObject("Equipment Panel", typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(640f, 620f);
            panel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.96f);

            MakeButton(panel.transform, "<", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -16f), new Vector2(64f, 48f), new Color(0.25f, 0.25f, 0.3f, 1f), () => Step(-1));
            MakeButton(panel.transform, ">", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-96f, -16f), new Vector2(64f, 48f), new Color(0.25f, 0.25f, 0.3f, 1f), () => Step(1));
            MakeButton(panel.transform, "X", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-16f, -16f), new Vector2(48f, 48f), new Color(0.5f, 0.15f, 0.15f, 1f), Close);

            // Header row: stretched across the top, between the arrow buttons.
            nameText = MakeText(panel.transform, 30, FontStyles.Bold, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(110f, -64f), new Vector2(-110f, -16f), new Vector2(0.5f, 1f));

            bodyText = MakeText(panel.transform, 24, FontStyles.Normal, Vector2.zero, Vector2.one,
                new Vector2(0f, -80f), new Vector2(-40f, -96f), new Vector2(0.5f, 1f));
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.rectTransform.anchorMin = Vector2.zero;
            bodyText.rectTransform.anchorMax = Vector2.one;
            bodyText.rectTransform.offsetMin = new Vector2(20f, 16f);
            bodyText.rectTransform.offsetMax = new Vector2(-20f, -80f);
        }

        private static Button MakeButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = color;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var textGo = new GameObject("Text", typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 24;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }

        private static TMP_Text MakeText(Transform parent, int fontSize, FontStyles style, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Vector2 pivot)
        {
            var go = new GameObject("Text", typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;

            var rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return text;
        }
    }
}
