using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Characters;

namespace UI
{
    public class StatEntry : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
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

        /// <summary>Raised when the character's NAME label is clicked (not the HP bars).</summary>
        public event Action<StatEntry> OnNameClicked;

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

        /// <summary>
        /// Clicks bubble up from whichever raycast target was hit; only a click on the
        /// name label counts as targeting the character.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            GameObject hit = eventData.pointerCurrentRaycast.gameObject;
            if (hit == null) return;
            if (nameLabel == null || hit.transform != nameLabel.transform) return;
            OnNameClicked?.Invoke(this);
        }
    }
}
