using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI
{
    public class StatEntry : MonoBehaviour
    {
        public Image bg;
        public Image blockOverlay;
        public TMP_Text nameLabel;
        public TMP_Text hpLabel;
        public TMP_Text blockLabel;

        [System.NonSerialized] public List<Image> hpSegments;
        [System.NonSerialized] public List<Image> blockSegments;
    }
}
