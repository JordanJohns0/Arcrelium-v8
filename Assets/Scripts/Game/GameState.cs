using System.Collections.Generic;
using Steamworks.Data;
using UnityEngine.SceneManagement;

namespace Game
{
    public static class GameState
    {
        public static int CurrentLevel { get; set; } = 1;
        public static GameMode ActiveMode { get; set; } = GameMode.SinglePlayer;
        public static int LocalPlayerId { get; set; } = 0;

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
            SceneManager.LoadScene("GameScene");
        }
    }
}
