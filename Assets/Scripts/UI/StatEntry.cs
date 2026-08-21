using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Characters;

namespace UI
{
    public class StatEntry : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image bg;
        public Image blockOverlay;
        public TMP_Text nameLabel;
        public TMP_Text hpLabel;
        public TMP_Text blockLabel;

        [System.NonSerialized] public List<Image> hpSegments;
        [System.NonSerialized] public List<Image> blockSegments;

        [NonSerialized] public Character linkedCharacter;

        public event Action<StatEntry> OnHoverEntered;
        public event Action<StatEntry> OnHoverExited;

        private Color originalBgColor;

        public void SetOriginalBgColor(Color color) => originalBgColor = color;

        public void RestoreBgColor()
        {
            if (bg != null) bg.color = originalBgColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnHoverEntered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnHoverExited?.Invoke(this);
        }
    }
}
