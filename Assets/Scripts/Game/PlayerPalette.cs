using UnityEngine;

namespace Game
{
    /// <summary>
    /// Shared player colours (blue, white, yellow, purple). Index 0 is the host.
    /// Used for both character tinting and the name shown in the HUD, so a player's
    /// character always matches their name colour.
    /// </summary>
    public static class PlayerPalette
    {
        public static readonly Color[] Colors =
        {
            new Color(0.20f, 0.45f, 0.85f),
            new Color(1.00f, 1.00f, 1.00f),
            new Color(1.00f, 0.84f, 0.15f),
            new Color(0.60f, 0.25f, 0.85f)
        };

        public static Color GetColor(int playerIndex)
        {
            if (Colors.Length == 0) return Color.white;
            int i = playerIndex % Colors.Length;
            if (i < 0) i += Colors.Length;
            return Colors[i];
        }
    }
}
