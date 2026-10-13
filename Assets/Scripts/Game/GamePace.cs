using UnityEngine;

namespace Game
{
    /// <summary>
    /// Central pacing knob for the enemy phase. The AI plays one card at a time with
    /// deliberate delays so every action is readable, but with many enemies that serial
    /// pacing adds up; this scales every AI, popup, and turn-transition delay by a single
    /// speed factor.
    ///
    /// The factor auto-ramps with the number of living enemies (readable in a small fight,
    /// fast in a swarm) and is multiplied by a player-facing manual override. Speed 1 leaves
    /// every authored timing untouched, which keeps the base gameplay and tests unchanged.
    /// </summary>
    public static class GamePace
    {
        public const string PrefKey = "Arcrelium.GameSpeed";

        private const int FreeEnemies = 2;
        private const float PerExtraEnemy = 0.5f;
        private const float MaxAutoSpeed = 4f;
        private const float MaxSpeed = 6f;

        private static float manualMultiplier = -1f;
        private static int lastAliveEnemies;

        /// <summary>Player-facing speed multiplier (1 = normal), persisted in PlayerPrefs.</summary>
        public static float ManualMultiplier
        {
            get
            {
                if (manualMultiplier < 0f)
                    manualMultiplier = Mathf.Max(1f, PlayerPrefs.GetFloat(PrefKey, 1f));
                return manualMultiplier;
            }
            set
            {
                manualMultiplier = Mathf.Max(1f, value);
                PlayerPrefs.SetFloat(PrefKey, manualMultiplier);
                PlayerPrefs.Save();
                Recompute();
            }
        }

        /// <summary>Effective speed. 1 = the authored timings.</summary>
        public static float Speed { get; private set; } = 1f;

        /// <summary>Auto speed for a given number of living enemies, before the manual override.</summary>
        public static float AutoSpeedFor(int aliveEnemies)
        {
            return Mathf.Clamp(1f + PerExtraEnemy * Mathf.Max(0, aliveEnemies - FreeEnemies),
                1f, MaxAutoSpeed);
        }

        /// <summary>Recomputes the effective speed from the current board.</summary>
        public static void Update(int aliveEnemies)
        {
            lastAliveEnemies = aliveEnemies;
            Recompute();
        }

        private static void Recompute()
        {
            Speed = Mathf.Clamp(AutoSpeedFor(lastAliveEnemies) * ManualMultiplier, 1f, MaxSpeed);
        }

        /// <summary>Restores the base timings (between matches and in tests).</summary>
        public static void Reset()
        {
            Speed = 1f;
            lastAliveEnemies = 0;
        }

        /// <summary>Divides an authored delay by the current speed.</summary>
        public static float Scale(float baseSeconds)
        {
            return baseSeconds / Mathf.Max(1f, Speed);
        }

        /// <summary>Test seam: drops the cached manual multiplier so PlayerPrefs are re-read.</summary>
        internal static void ResetManualCache()
        {
            manualMultiplier = -1f;
        }
    }
}
