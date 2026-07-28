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
            vlg.spacing = 4f;
            vlg.padding = new RectOffset(4, 4, 4, 4);
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

            if (entry.bg != null) entry.bg.color = isAlly ? allyColor : enemyColor;
            UpdateEntryLabels(entry, character.Stats);
            UpdateBars(entry, character.Stats);

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
                UpdateBars(kv.Value, stats);
                break;
            }
        }

        private void UpdateBars(StatEntry entry, CharacterStats stats)
        {
            float max = Mathf.Max(1, stats.MaxHealth);

            if (entry.hpFill != null)
            {
                float hpRatio = (float)stats.CurrentHealth / max;
                entry.hpFill.rectTransform.anchorMax = new Vector2(hpRatio, 1f);
                entry.hpFill.color = Color.Lerp(Color.red, Color.green, hpRatio);
            }

            if (entry.blockOverlay != null)
            {
                float blockRatio = (float)stats.Block / max;
                entry.blockOverlay.rectTransform.anchorMax = new Vector2(blockRatio, 1f);
            }
        }

        private void UpdateEntryLabels(StatEntry entry, CharacterStats stats)
        {
            if (entry.nameLabel != null) entry.nameLabel.text = stats.CharacterName;
            if (entry.hpLabel != null) entry.hpLabel.text = $"HP: {stats.CurrentHealth}/{stats.MaxHealth}";
            if (entry.armorLabel != null) entry.armorLabel.text = $"Armor: {stats.Armor}";
            if (entry.blockLabel != null) entry.blockLabel.text = $"Block: {stats.Block}";
        }
    }

}
