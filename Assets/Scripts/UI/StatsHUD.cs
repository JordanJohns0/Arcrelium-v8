using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Characters;

namespace UI
{
    public class StatsHUD : MonoBehaviour
    {
        [SerializeField] private GameObject statEntryPrefab;
        [SerializeField] private Color allyColor = new Color(0.15f, 0.25f, 0.50f, 0.7f);
        [SerializeField] private Color enemyColor = new Color(0.50f, 0.15f, 0.15f, 0.7f);

        private Canvas canvas;
        private GameObject alliesRoot;
        private GameObject enemiesRoot;
        private bool initialized;

        private readonly List<Character> pendingCharacters = new List<Character>();
        private readonly Dictionary<Character, StatEntry> entries = new Dictionary<Character, StatEntry>();

        private void OnEnable()
        {
            Character.OnCharacterCreated += HandleCharacterCreated;
            Character.OnCharacterDestroyed += HandleCharacterDestroyed;
        }

        private void OnDisable()
        {
            Character.OnCharacterCreated -= HandleCharacterCreated;
            Character.OnCharacterDestroyed -= HandleCharacterDestroyed;
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

        private void CreateCanvas()
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                var go = new GameObject("StatsHUD_Canvas");
                go.transform.SetParent(transform, false);
                canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = -1;
                go.AddComponent<CanvasScaler>();
            }
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
            headerText.fontSize = 18;
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
            scrollImage.color = new Color(0f, 0f, 0f, 0.3f);
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
            vlg.spacing = 50f;
            vlg.padding = new RectOffset(4, 4, 120, 4);
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

            bool isAlly = character.Team == Team.Ally;
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

            entry.bg.color = isAlly ? allyColor : enemyColor;

            // Destroy all labels except name
            float nameWidth = 80f;
            if (entry.nameLabel != null)
            {
                entry.nameLabel.raycastTarget = false;
                nameWidth = Mathf.Max(60f, entry.nameLabel.preferredWidth + 8f);
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

                entry.nameLabel.fontSize = 30;
                entry.nameLabel.verticalAlignment = VerticalAlignmentOptions.Bottom;
                entry.nameLabel.alignment = isAlly ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
                entry.nameLabel.color = character.PlayerColor ?? Color.white;
                var nameRt = entry.nameLabel.rectTransform;
                if (isAlly)
                {
                    nameRt.anchorMin = new Vector2(0f, 1f);
                    nameRt.anchorMax = new Vector2(0f, 1f);
                    nameRt.pivot = new Vector2(0f, 1f);
                    nameRt.anchoredPosition = new Vector2(4f, -4f);
                }
                else
                {
                    nameRt.anchorMin = new Vector2(1f, 1f);
                    nameRt.anchorMax = new Vector2(1f, 1f);
                    nameRt.pivot = new Vector2(1f, 1f);
                    nameRt.anchoredPosition = new Vector2(-4f, -4f);
                }
                nameRt.sizeDelta = new Vector2(nameWidth, 25f);
            }

            CreateSegments(entry, character.Stats.MaxHealth, isAlly, nameWidth);

            int segmentsPerRow = Mathf.Max(1, Mathf.FloorToInt((target.GetComponent<RectTransform>().rect.width - nameWidth - 4) / 56f));
            int rows = Mathf.CeilToInt((float)character.Stats.MaxHealth / segmentsPerRow);
            float h = 6f + Mathf.Max(25f, rows * 25f + (rows - 1) * 6f);
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
                UpdateBars(kv.Value, stats, kv.Key.Team == Team.Ally);
                break;
            }
        }

        private static void UpdateEntryNameColor(StatEntry entry, Character character)
        {
            if (entry.nameLabel != null)
                entry.nameLabel.color = character.PlayerColor ?? Color.white;
        }

        private void CreateSegments(StatEntry entry, int maxHealth, bool isAlly, float nameWidth)
        {
            if (entry.bg == null) return;

            var glg = entry.bg.gameObject.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(50f, 25f);
            glg.spacing = new Vector2(6f, 6f);
            int padLeft = isAlly ? Mathf.RoundToInt(nameWidth) + 4 : 2;
            int padRight = isAlly ? 2 : Mathf.RoundToInt(nameWidth) + 4;
            glg.padding = new RectOffset(padLeft, padRight, 2, 2);
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
                blockImage.color = new Color(0.73f, 0.73f, 0.73f, 0.6f);
                blockImage.raycastTarget = false;
                var blockRt = blockSeg.GetComponent<RectTransform>();
                blockRt.anchorMin = Vector2.zero;
                blockRt.anchorMax = Vector2.one;
                blockRt.offsetMin = new Vector2(-3f, -3f);
                blockRt.offsetMax = new Vector2(3f, 3f);
                entry.blockSegments.Add(blockImage);
            }
        }

        private void UpdateBars(StatEntry entry, CharacterStats stats, bool isAlly)
        {
            int max = Mathf.Max(1, stats.MaxHealth);
            int current = stats.CurrentHealth;
            int block = stats.Block;

            Color fillColor = Color.red;
            Color emptyColor = new Color(0f, 0f, 0f, 0.3f);
            Color blockColor = new Color(0.73f, 0.73f, 0.73f, 0.6f);
            Color noBlock = Color.clear;

            for (int i = 0; i < entry.hpSegments.Count; i++)
            {
                bool hpFilled = isAlly ? i < current : i >= max - current;
                entry.hpSegments[i].color = hpFilled ? fillColor : emptyColor;

                bool hasBlock = isAlly ? i < block : i >= max - block;
                if (i < entry.blockSegments.Count)
                    entry.blockSegments[i].color = hasBlock ? blockColor : noBlock;
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
