using System.Collections.Generic;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// Lobby match format. PvE is the classic co-op fight vs AI enemies; in PvP every
    /// player is on their own team (everyone else is an enemy) and the last player
    /// standing wins.
    /// </summary>
    public enum MatchMode { PvE, PvP }

    public static class GameState
    {
        /// <summary>OwnerPlayerId threshold at or above which a character is AI-controlled.</summary>
        public const int EnemyOwnerPlayerId = 100;

        public static int CurrentLevel { get; set; } = 1;
        public static GameMode ActiveMode { get; set; } = GameMode.SinglePlayer;
        public static int LocalPlayerId { get; set; } = 0;

        private static int shopSeed;

        /// <summary>
        /// Random base seed for this run's shop stock. Rolled once at run start so the shop
        /// changes every run, while the level number is mixed in so different levels still
        /// differ (see <c>ShopScreen</c>). Rolled per machine — the host never sees another
        /// player's shop — so it is not shared over the network.
        /// </summary>
        public static int ShopSeed
        {
            get
            {
                if (shopSeed == 0) shopSeed = RollShopSeed();
                return shopSeed;
            }
            set => shopSeed = value;
        }

        /// <summary>Rolls a fresh, non-zero seed for a run's shop stock.</summary>
        internal static int RollShopSeed()
        {
            return Random.Range(1, int.MaxValue);
        }

        /// <summary>
        /// Which match format the current lobby/run is using. Kept in sync with the
        /// lobby's "matchmode" data (see LobbyManager) so every machine agrees.
        /// </summary>
        public static MatchMode CurrentMatchMode { get; set; } = MatchMode.PvE;

        public static bool IsPvP => CurrentMatchMode == MatchMode.PvP;

        /// <summary>
        /// When true, the game starts on the loadout screen instead of the board: players
        /// pick gear, ready up, and the host starts the level. Set by Restart() (the start
        /// of a run) and by a victory, cleared by the host after the match begins.
        /// </summary>
        public static bool ShowLoadoutScreen { get; set; }

        /// <summary>
        /// Equipped loadout per player id for the upcoming level. Filled by the host after
        /// every player confirms on the loadout screen, survives the scene reload into the
        /// next level, and is applied to characters during match setup.
        /// </summary>
        public static readonly System.Collections.Generic.Dictionary<int, Equipment.EquipmentLoadout> MatchLoadouts =
            new System.Collections.Generic.Dictionary<int, Equipment.EquipmentLoadout>();

        /// <summary>
        /// Deterministic player index: members are sorted by Steam id so both machines
        /// agree on who is player 0, player 1, etc. Returns -1 when the id is absent.
        /// </summary>
        public static int GetPlayerIndex(IEnumerable<ulong> memberIds, ulong steamId)
        {
            if (memberIds == null) return -1;
            var ids = new List<ulong>(memberIds);
            ids.Sort();
            return ids.IndexOf(steamId);
        }

        public static int GetPlayerIndex(Lobby lobby, ulong steamId)
        {
            if (lobby.Id.Value == 0) return 0;
            var ids = new List<ulong>();
            foreach (var member in lobby.Members) ids.Add(member.Id);
            return GetPlayerIndex(ids, steamId);
        }

        public static void LoadNextLevel()
        {
            CurrentLevel++;
            SceneManager.LoadScene("GameScene");
        }

        public static void Restart()
        {
            CurrentLevel = 1;
            shopSeed = RollShopSeed();
            ShowLoadoutScreen = true;
            Equipment.PlayerInventoryProvider.ResetToTemplate(Equipment.PlayerInventoryProvider.LocalPlayerKey);
            SceneManager.LoadScene("GameScene");
        }
    }
}
