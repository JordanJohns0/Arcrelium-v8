using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Characters;
using Game;

namespace UI
{
    public class StatsHUD : MonoBehaviour
    {
        [SerializeField] private GameObject statEntryPrefab;
        [SerializeField] private Color allyColor = new Color(0.15f, 0.25f, 0.50f, 0.7f);
        [SerializeField] private Color enemyColor = new Color(0.35f, 0.30f, 0.28f, 0.7f);

        [Header("Hover Highlight")]
        [SerializeField] private Color entryHoverHighlight = new Color(1f, 1f, 1f, 0.15f);

        [Header("Canvas")]
        [SerializeField] private int sortingOrder = -1;

        [Header("Panels")]
        [SerializeField] private int headerFontSize = 18;
        [SerializeField] private Color scrollBackgroundColor = new Color(0f, 0f, 0f, 0.3f);
        [SerializeField] private float listSpacing = 50f;
        [SerializeField] private int listPadding = 4;
        [SerializeField] private int listPaddingTop = 120;

        [Header("Entries")]
        [SerializeField] private float nameMinWidth = 60f;
        [SerializeField] private float nameMaxWidth = 80f;
        [SerializeField] private float namePadding = 8f;
        [SerializeField] private int nameFontSize = 30;
        [SerializeField] private float nameHeight = 25f;
        [SerializeField] private float nameHorizontalOffset = 4f;
        [SerializeField] private float nameVerticalOffset = -4f;

        [Header("Segments")]
        [SerializeField] private Vector2 segmentCellSize = new Vector2(50f, 25f);
        [SerializeField] private Vector2 segmentSpacing = new Vector2(6f, 6f);
        [SerializeField] private float segmentRowWidth = 56f;
        [SerializeField] private float segmentPadding = 4f;
        [SerializeField] private int segmentEdgePadding = 2;
        [SerializeField] private int segmentPaddingVertical = 2;

        [Header("Bar Colors")]
        [SerializeField] private Color hpFillColor = Color.red;
        [SerializeField] private Color hpEmptyColor = new Color(0f, 0f, 0f, 0.3f);
        [SerializeField] private Color blockColor = new Color(0.73f, 0.73f, 0.73f, 0.6f);
        [SerializeField] private float blockPadding = 3f;

        [Header("Rows")]
        [SerializeField] private float rowWidthMargin = 4f;
        [SerializeField] private float rowBaseHeight = 6f;
        [SerializeField] private float rowHeight = 25f;
        [SerializeField] private float rowSpacing = 6f;

        private Canvas canvas;
        private GameObject alliesRoot;
        private GameObject enemiesRoot;
        private bool initialized;
        private int? cachedLocalTeamId;

        private readonly List<Character> pendingCharacters = new List<Character>();
        private readonly Dictionary<Character, StatEntry> entries = new Dictionary<Character, StatEntry>();

        /// <summary>
        /// The local player's team. From their perspective only characters on this team
        /// are allies — in PvE that's every human player (team 0), and in PvP only their
        /// own character (each player has a distinct team id).
        /// </summary>
        private int GetLocalTeamId()
        {
            if (cachedLocalTeamId.HasValue) return cachedLocalTeamId.Value;

            foreach (Character c in FindObjectsByType<Character>(FindObjectsSortMode.None))
            {
                if (c != null && c.OwnerPlayerId == GameState.LocalPlayerId)
                {
                    cachedLocalTeamId = c.TeamId;
                    return c.TeamId;
                }
            }
            return 0;
        }

        private void OnEnable()
        {
            Character.OnCharacterCreated += HandleCharacterCreated;
            Character.OnCharacterDestroyed += HandleCharacterDestroyed;
            CharacterSelectable.AnyCharacterHovered += HandleCharacterHovered;
            CharacterSelectable.AnyCharacterHoverEnded += HandleCharacterHoverEnded;
        }

        private void OnDisable()
        {
            Character.OnCharacterCreated -= HandleCharacterCreated;
            Character.OnCharacterDestroyed -= HandleCharacterDestroyed;
            CharacterSelectable.AnyCharacterHovered -= HandleCharacterHovered;
            CharacterSelectable.AnyCharacterHoverEnded -= HandleCharacterHoverEnded;
        }

        private void Start()
        {
            CreateCanvas();
            CreatePanelStructure();
            initialized = true;

            foreach (var c in pendingCharacters)
                AddCharacter(c);
            pendingCharacters.Clear();

            foreach (Character c in FindObjectsByType<Character>(FindObjectsSortMode.None))
            {
                if (c.Stats != null && c.gameObject.scene.IsValid() && !entries.ContainsKey(c))
                    AddCharacter(c);
            }
        }

        private void OnDestroy()
        {
            foreach (var kv in entries)
            {
                if (kv.Key != null)
                {
                    kv.Key.OnDefeated -= HandleCharacterDefeated;
                    if (kv.Key.Stats != null)
                        kv.Key.Stats.OnStatsChanged -= UpdateEntry;
                }
            }
        }

        private void HandleCharacterCreated(Character character)
        {
            if (character.Stats == null) return;
            if (!initialized)
            {
                pendingCharacters.Add(character);
                return;
            }
            AddCharacter(character);
        }

        private void HandleCharacterDestroyed(Character character)
        {
            RemoveCharacter(character);
        }

        private void HandleCharacterDefeated(Character character)
        {
            RemoveCharacter(character);
        }

        private Character hoveredCharacter;
        private StatEntry hoveredEntry;

        /// <summary>
        /// Clicking a character's name in the HUD targets that character exactly like
        /// clicking its model: the same CharacterSelectable event fires, so all target
        /// validation (range, validity, current selection state) applies unchanged.
        /// </summary>
        private void HandleStatEntryNameClicked(StatEntry entry)
        {
            if (entry == null || entry.linkedCharacter == null) return;
            CharacterSelectable.NotifyClicked(entry.linkedCharacter);
        }

        private void HandleCharacterHovered(Character character)
        {
            hoveredCharacter = character;
            SetOutline(character, true);

            if (entries.TryGetValue(character, out StatEntry entry))
            {
                hoveredEntry = entry;
                HighlightEntry(entry, true);
            }
        }

        private void HandleCharacterHoverEnded()
        {
            if (hoveredCharacter != null)
                SetOutline(hoveredCharacter, false);

            if (hoveredEntry != null)
                HighlightEntry(hoveredEntry, false);

            hoveredCharacter = null;
            hoveredEntry = null;
        }

        private void HandleStatEntryHovered(StatEntry entry)
        {
            hoveredEntry = entry;
            HighlightEntry(entry, true);

            if (entry.linkedCharacter != null)
            {
                hoveredCharacter = entry.linkedCharacter;
                SetOutline(entry.linkedCharacter, true);
            }
        }

        private void HandleStatEntryHoverEnded(StatEntry entry)
        {
            HighlightEntry(entry, false);

            if (entry.linkedCharacter != null)
                SetOutline(entry.linkedCharacter, false);

            hoveredEntry = null;
            hoveredCharacter = null;
        }

        private void SetOutline(Character character, bool on)
        {
            OutlineEffect outline = character.GetComponentInChildren<OutlineEffect>();
            if (outline == null) return;
            outline.SetOutline(on);
        }

        private void HighlightEntry(StatEntry entry, bool highlight)
        {
            if (entry == null || entry.bg == null) return;
            if (highlight)
            {
                entry.SetOriginalBgColor(entry.bg.color);
                entry.bg.color = entry.bg.color + entryHoverHighlight;
                if (entry.nameLabel != null) entry.nameLabel.fontStyle = FontStyles.Bold;
            }
            else
            {
                entry.RestoreBgColor();
                if (entry.nameLabel != null) entry.nameLabel.fontStyle = FontStyles.Normal;
            }
        }

        private void CreateCanvas()
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                var go = new GameObject("StatsHUD_Canvas");
                go.transform.SetParent(transform, false);
                canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = sortingOrder;
                go.AddComponent<CanvasScaler>();
            }

            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        private void CreatePanelStructure()
        {
            var leftPanel = CreateSidePanel("Allies", TextAnchor.UpperLeft, 0f);
            alliesRoot = leftPanel.transform.Find("ScrollView/Viewport/Content").gameObject;

            var rightPanel = CreateSidePanel("Enemies", TextAnchor.UpperRight, 0.5f);
            enemiesRoot = rightPanel.transform.Find("ScrollView/Viewport/Content").gameObject;
        }

        private GameObject CreateSidePanel(string title, TextAnchor align, float leftRatio)
        {
            var panel = new GameObject(title, typeof(RectTransform));
            panel.transform.SetParent(canvas.transform, false);

            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(leftRatio, 0f);
            rt.anchorMax = new Vector2(leftRatio + 0.5f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var header = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI));
            header.transform.SetParent(panel.transform, false);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 0.9f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.offsetMin = Vector2.zero;
            headerRt.offsetMax = Vector2.zero;

            var headerText = header.GetComponent<TextMeshProUGUI>();
            headerText.text = title;
            headerText.fontSize = headerFontSize;
            headerText.alignment = align == TextAnchor.UpperLeft ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
            headerText.color = Color.white;
            headerText.fontStyle = FontStyles.Bold;

            var scroll = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scroll.transform.SetParent(panel.transform, false);
            var scrollRt = scroll.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = new Vector2(1f, 0.9f);
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;
            var scrollImage = scroll.GetComponent<Image>();
            scrollImage.color = scrollBackgroundColor;
            scrollImage.raycastTarget = false;
            scroll.GetComponent<ScrollRect>().horizontal = false;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image));
            viewport.transform.SetParent(scroll.transform, false);
            var vpRt = viewport.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero;
            vpRt.offsetMax = Vector2.zero;
            var vpImage = viewport.GetComponent<Image>();
            vpImage.color = Color.clear;
            vpImage.raycastTarget = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var cRt = content.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0f, 1f);
            cRt.anchorMax = new Vector2(1f, 1f);
            cRt.offsetMin = Vector2.zero;
            cRt.offsetMax = Vector2.zero;
            cRt.pivot = new Vector2(0.5f, 1f);

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = listSpacing;
            vlg.padding = new RectOffset(listPadding, listPadding, listPaddingTop, listPadding);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = content.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scroll.GetComponent<ScrollRect>().content = cRt;
            scroll.GetComponent<ScrollRect>().viewport = vpRt;

            return panel;
        }

        private void AddCharacter(Character character)
        {
            if (entries.ContainsKey(character)) return;

            bool isAlly = character.TeamId == GetLocalTeamId();
            var target = isAlly ? alliesRoot : enemiesRoot;
            if (target == null) return;

            var go = Instantiate(statEntryPrefab, target.transform);
            go.name = character.Stats.CharacterName;

            var entry = go.GetComponent<StatEntry>();
            if (entry == null)
            {
                Destroy(go);
                return;
            }

            if (entry.blockOverlay != null)
                entry.blockOverlay.gameObject.SetActive(false);

            if (entry.bg == null) return;

            // bg stays disabled (invisible, no raycast) — hover targets are the
            // name label and the health segments instead of the full panel.
            entry.bg.color = isAlly ? allyColor : enemyColor;
            entry.SetOriginalBgColor(entry.bg.color);
            entry.linkedCharacter = character;
            entry.OnHoverEntered += HandleStatEntryHovered;
            entry.OnHoverExited += HandleStatEntryHoverEnded;
            entry.OnNameClicked += HandleStatEntryNameClicked;

            // Destroy all labels except name
            float nameWidth = nameMaxWidth;
            if (entry.nameLabel != null)
            {
                entry.nameLabel.raycastTarget = true;
                nameWidth = Mathf.Max(nameMinWidth, entry.nameLabel.preferredWidth + namePadding);
                var labelsParent = entry.nameLabel.transform.parent;
                if (labelsParent != null)
                {
                    var toDestroy = new List<GameObject>();
                    foreach (Transform child in labelsParent)
                    {
                        if (child.gameObject != entry.nameLabel.gameObject)
                            toDestroy.Add(child.gameObject);
                    }
                    foreach (var d in toDestroy) Destroy(d);
                    entry.nameLabel.transform.SetParent(go.transform, false);
                    Destroy(labelsParent.gameObject);
                }

                entry.nameLabel.fontSize = nameFontSize;
                entry.nameLabel.verticalAlignment = VerticalAlignmentOptions.Bottom;
                entry.nameLabel.alignment = isAlly ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
                entry.nameLabel.color = character.NameColor ?? character.PlayerColor ?? Color.white;
                var nameRt = entry.nameLabel.rectTransform;
                if (isAlly)
                {
                    nameRt.anchorMin = new Vector2(0f, 1f);
                    nameRt.anchorMax = new Vector2(0f, 1f);
                    nameRt.pivot = new Vector2(0f, 1f);
                    nameRt.anchoredPosition = new Vector2(nameHorizontalOffset, nameVerticalOffset);
                }
                else
                {
                    nameRt.anchorMin = new Vector2(1f, 1f);
                    nameRt.anchorMax = new Vector2(1f, 1f);
                    nameRt.pivot = new Vector2(1f, 1f);
                    nameRt.anchoredPosition = new Vector2(-nameHorizontalOffset, nameVerticalOffset);
                }
                nameRt.sizeDelta = new Vector2(nameWidth, nameHeight);
            }

            CreateSegments(entry, character.Stats.MaxHealth, isAlly, nameWidth);

            int segmentsPerRow = Mathf.Max(1, Mathf.FloorToInt((target.GetComponent<RectTransform>().rect.width - nameWidth - rowWidthMargin) / segmentRowWidth));
            int rows = Mathf.CeilToInt((float)character.Stats.MaxHealth / segmentsPerRow);
            float h = rowBaseHeight + Mathf.Max(rowHeight, rows * rowHeight + (rows - 1) * rowSpacing);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, h);

            UpdateEntryLabels(entry, character.Stats);
            UpdateBars(entry, character.Stats, isAlly);

            entries[character] = entry;
            character.OnDefeated += HandleCharacterDefeated;
            character.Stats.OnStatsChanged += UpdateEntry;
        }

        private void RemoveCharacter(Character character)
        {
            if (entries.TryGetValue(character, out StatEntry entry))
            {
                character.OnDefeated -= HandleCharacterDefeated;
                character.Stats.OnStatsChanged -= UpdateEntry;
                entry.OnHoverEntered -= HandleStatEntryHovered;
                entry.OnHoverExited -= HandleStatEntryHoverEnded;
                entry.OnNameClicked -= HandleStatEntryNameClicked;
                Destroy(entry.gameObject);
                entries.Remove(character);
            }
        }

        private void UpdateEntry(CharacterStats stats)
        {
            foreach (var kv in entries)
            {
                if (kv.Key == null || kv.Key.Stats != stats) continue;
                UpdateEntryLabels(kv.Value, stats);
                UpdateEntryNameColor(kv.Value, kv.Key);
                UpdateBars(kv.Value, stats, kv.Key.TeamId == GetLocalTeamId());
                break;
            }
        }

        private static void UpdateEntryNameColor(StatEntry entry, Character character)
        {
            if (entry.nameLabel != null)
                entry.nameLabel.color = character.NameColor ?? character.PlayerColor ?? Color.white;
        }

        private void CreateSegments(StatEntry entry, int maxHealth, bool isAlly, float nameWidth)
        {
            if (entry.bg == null) return;

            var glg = entry.bg.gameObject.AddComponent<GridLayoutGroup>();
            glg.cellSize = segmentCellSize;
            glg.spacing = segmentSpacing;
            int padLeft = isAlly ? Mathf.RoundToInt(nameWidth) + Mathf.RoundToInt(segmentPadding) : segmentEdgePadding;
            int padRight = isAlly ? segmentEdgePadding : Mathf.RoundToInt(nameWidth) + Mathf.RoundToInt(segmentPadding);
            glg.padding = new RectOffset(padLeft, padRight, segmentPaddingVertical, segmentPaddingVertical);
            glg.startAxis = GridLayoutGroup.Axis.Horizontal;
            glg.childAlignment = isAlly ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
            glg.constraint = GridLayoutGroup.Constraint.Flexible;

            entry.hpSegments = new List<Image>(maxHealth);
            entry.blockSegments = new List<Image>(maxHealth);
            for (int i = 0; i < maxHealth; i++)
            {
                var seg = new GameObject($"Segment_{i}", typeof(Image));
                seg.transform.SetParent(entry.bg.transform, false);
                entry.hpSegments.Add(seg.GetComponent<Image>());

                var blockSeg = new GameObject("Block", typeof(Image));
                blockSeg.transform.SetParent(seg.transform, false);
                var blockImage = blockSeg.GetComponent<Image>();
                blockImage.color = blockColor;
                blockImage.raycastTarget = false;
                var blockRt = blockSeg.GetComponent<RectTransform>();
                blockRt.anchorMin = Vector2.zero;
                blockRt.anchorMax = Vector2.one;
                blockRt.offsetMin = new Vector2(-blockPadding, -blockPadding);
                blockRt.offsetMax = new Vector2(blockPadding, blockPadding);
                entry.blockSegments.Add(blockImage);
            }
        }

        private void UpdateBars(StatEntry entry, CharacterStats stats, bool isAlly)
        {
            int max = Mathf.Max(1, stats.MaxHealth);
            int current = stats.CurrentHealth;
            int block = stats.Block;

            for (int i = 0; i < entry.hpSegments.Count; i++)
            {
                bool hpFilled = isAlly ? i < current : i >= max - current;
                entry.hpSegments[i].color = hpFilled ? hpFillColor : hpEmptyColor;

                bool hasBlock = isAlly ? i < block : i >= max - block;
                if (i < entry.blockSegments.Count)
                    entry.blockSegments[i].color = hasBlock ? blockColor : Color.clear;
            }
        }

        private void SetLabelAlignment(StatEntry entry, bool isAlly)
        {
            if (entry.nameLabel != null)
                entry.nameLabel.alignment = isAlly ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
        }

        private void UpdateEntryLabels(StatEntry entry, CharacterStats stats)
        {
            if (entry.nameLabel != null) entry.nameLabel.text = stats.CharacterName;
        }
    }

}
