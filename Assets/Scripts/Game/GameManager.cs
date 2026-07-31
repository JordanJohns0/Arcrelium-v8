using System;
using System.Collections.Generic;
using UnityEngine;
using HexGrid;
using Characters;
using Cards;
using UI;
using Steamworks;
using Steamworks.Data;
using Color = UnityEngine.Color;
using Random = UnityEngine.Random;

namespace Game
{
    public enum GameMode { SinglePlayer, Host, Client }

    public class GameManager : MonoBehaviour
    {
        private const int EnemyOwnerPlayerId = 100;

        private static readonly Color[] PlayerColors =
        {
            new Color(0.20f, 0.55f, 0.90f),
            new Color(0.25f, 0.80f, 0.35f),
            new Color(0.95f, 0.70f, 0.15f),
            new Color(0.70f, 0.40f, 0.90f)
        };

        private static readonly Color EnemyColor = new Color(0.85f, 0.30f, 0.25f);

        private static readonly HexCoordinates[] MultiplayerAllySpawns =
        {
            new HexCoordinates(0, -2),
            new HexCoordinates(1, -2),
            new HexCoordinates(-1, -1),
            new HexCoordinates(-1, -2)
        };

        [SerializeField] private HexGridManager grid;
        [SerializeField] private ActionResolver actionResolver;
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private Character playerCharacterPrefab;
        [SerializeField] private Character enemyCharacterPrefab;
        [SerializeField] private UnityEngine.InputSystem.InputActionAsset inputActions;
        [SerializeField] [Min(1)] private int baseEnemyCount = 2;
        [SerializeField] [Min(0)] private int extraEnemiesPerLevel = 1;
        [SerializeField] private int allySpawnQ = 0;
        [SerializeField] private int allySpawnR = -2;

        [System.Serializable]
        public class CharacterSpawn
        {
            public Character characterPrefab;
            public int ownerPlayerId;
            public int spawnQ;
            public int spawnR;
        }

        [SerializeField] private List<CharacterSpawn> spawns = new List<CharacterSpawn>();

        internal List<Character> activeCharacters = new List<Character>();

        /// <summary>
        /// Test seam: maps a sender's Steam id to a lobby player index. When null,
        /// the default lobby-member lookup is used.
        /// </summary>
        internal static Func<ulong, int> PlayerIdLookup;

        private VictoryScreen victoryScreen;

        private void Start()
        {
            GameMode currentMode = GameState.ActiveMode;
            switch (currentMode)
            {
                case GameMode.SinglePlayer:
                    GameState.LocalPlayerId = 0;
                    SubscribeHostLogic();
                    if (playerCharacterPrefab != null && enemyCharacterPrefab != null)
                        StartCoroutine(SetupDynamicMatchDeferred());
                    else
                        SetupMatch();
                    break;

                case GameMode.Host:
                    GameState.LocalPlayerId = ComputeLocalPlayerId();
                    SubscribeHostLogic();
                    if (playerCharacterPrefab != null && enemyCharacterPrefab != null)
                        StartCoroutine(SetupDynamicMatchDeferred());
                    else
                        SetupMatch();
                    break;

                case GameMode.Client:
                    GameState.LocalPlayerId = ComputeLocalPlayerId();
                    grid.GenerateGrid();
                    Networking.NetworkManager.OnGameStateReceived += HandleGameStateReceived;
                    Networking.NetworkManager.Instance.SendRequestInitialState();
                    break;
            }

            PauseMenu.GetOrCreate(inputActions);
        }

        private void OnDestroy()
        {
            Networking.NetworkManager.OnGameStateReceived -= HandleGameStateReceived;
            Networking.NetworkManager.OnRequestInitialStateReceived -= HandleRequestInitialState;
            Networking.NetworkManager.OnPlayCardRequestReceived -= HandlePlayCardRequest;
            Networking.NetworkManager.OnPassRequestReceived -= HandlePassRequest;
            if (turnManager != null)
            {
                turnManager.OnActionResolved -= HandleActionResolved;
                turnManager.OnTurnStarted -= HandleTurnStartedBroadcast;
            }
        }

        private void SubscribeHostLogic()
        {
            Networking.NetworkManager.OnRequestInitialStateReceived += HandleRequestInitialState;
            Networking.NetworkManager.OnPlayCardRequestReceived += HandlePlayCardRequest;
            Networking.NetworkManager.OnPassRequestReceived += HandlePassRequest;
            if (turnManager != null)
            {
                turnManager.OnActionResolved += HandleActionResolved;
                turnManager.OnTurnStarted += HandleTurnStartedBroadcast;
            }
        }

        private static int ComputeLocalPlayerId()
        {
            var lobby = LobbyManager.Instance.CurrentLobby;
            if (lobby.HasValue)
                return GameState.GetPlayerIndex(lobby.Value, SteamClient.SteamId);
            return 0;
        }

        private System.Collections.IEnumerator SetupDynamicMatchDeferred()
        {
            yield return null;
            SetupDynamicMatch();
            if (GameState.ActiveMode == GameMode.Host)
                BroadcastGameState();
        }

        public void SetupMatch()
        {
            grid.GenerateGrid();
            activeCharacters.Clear();

            foreach (var spawn in spawns)
            {
                var coords = new HexCoordinates(spawn.spawnQ, spawn.spawnR);
                if (!grid.TryGetCell(coords, out HexCell cell))
                {
                    Debug.LogWarning($"Spawn coordinates {coords} are not on the grid.");
                    continue;
                }

                Character character = Instantiate(spawn.characterPrefab);
                character.Initialize(spawn.ownerPlayerId, cell);
                character.OnDefeated += HandleCharacterDefeated;
                activeCharacters.Add(character);
            }

            turnManager.SetTurnOrder(new List<Character>(activeCharacters));
            turnManager.StartMatch();
        }

        private void SetupDynamicMatch()
        {
            grid.GenerateGrid();
            activeCharacters.Clear();

            if (GameState.ActiveMode == GameMode.Host)
                SetupMultiplayerPlayers();
            else
                SetupSinglePlayerPlayer();

            int enemyCount = baseEnemyCount + (GameState.CurrentLevel - 1) * extraEnemiesPerLevel;
            var spawnCells = GetValidSpawnCells(enemyCount);
            int enemyHp = 2 + GameState.CurrentLevel;
            int enemyArmor = Mathf.Max(0, GameState.CurrentLevel - 1);

            for (int i = 0; i < enemyCount && i < spawnCells.Count; i++)
            {
                Character enemy = Instantiate(enemyCharacterPrefab);
                enemy.Stats.MaxHealth = enemyHp;
                enemy.Stats.Armor = enemyArmor;
                enemy.Initialize(EnemyOwnerPlayerId + i, spawnCells[i], teamId: 1);
                enemy.SetName("Enemy");
                enemy.SetPlayerColor(EnemyColor);
                enemy.OnDefeated += HandleCharacterDefeated;
                activeCharacters.Add(enemy);
            }

            turnManager.SetTurnOrder(new List<Character>(activeCharacters));
            turnManager.StartMatch();
        }

        private void SetupMultiplayerPlayers()
        {
            var lobby = LobbyManager.Instance.CurrentLobby;
            if (!lobby.HasValue)
            {
                SetupSinglePlayerPlayer();
                return;
            }

            var members = new List<Friend>(lobby.Value.Members);
            members.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));

            for (int i = 0; i < members.Count; i++)
            {
                var coords = i < MultiplayerAllySpawns.Length
                    ? MultiplayerAllySpawns[i]
                    : new HexCoordinates(allySpawnQ, allySpawnR);
                if (!grid.TryGetCell(coords, out HexCell cell)) continue;

                Character player = Instantiate(playerCharacterPrefab);
                player.Initialize(i, cell, teamId: 0);
                player.SetName(members[i].Name);
                player.SetPlayerColor(PlayerColors[i % PlayerColors.Length]);
                player.OnDefeated += HandleCharacterDefeated;
                activeCharacters.Add(player);
            }
        }

        private void SetupSinglePlayerPlayer()
        {
            var playerCoords = new HexCoordinates(allySpawnQ, allySpawnR);
            if (!grid.TryGetCell(playerCoords, out HexCell playerCell)) return;

            Character player = Instantiate(playerCharacterPrefab);
            player.Initialize(0, playerCell, teamId: 0);
            player.SetName(SteamClient.IsValid ? SteamClient.Name : null);
            player.SetPlayerColor(PlayerColors[0]);
            player.OnDefeated += HandleCharacterDefeated;
            activeCharacters.Add(player);
        }

        private List<HexCell> GetValidSpawnCells(int count)
        {
            var candidates = new List<HexCell>();
            foreach (var cell in grid.AllCells)
            {
                if (!cell.IsWalkable) continue;
                var c = cell.Coordinates;
                if (Mathf.Abs(c.Q) <= 1 && Mathf.Abs(c.R) <= 1 && Mathf.Abs(c.Q + c.R) <= 1)
                    continue;
                candidates.Add(cell);
            }

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = temp;
            }

            int take = Mathf.Min(count, candidates.Count);
            var result = new List<HexCell>(take);
            for (int i = 0; i < take; i++)
                result.Add(candidates[i]);
            return result;
        }

        private void HandleCharacterDefeated(Character character)
        {
            activeCharacters.Remove(character);
            turnManager.RemoveFromTurnOrder(character);

            bool anyEnemyAlive = false;
            for (int i = 0; i < activeCharacters.Count; i++)
            {
                if (activeCharacters[i].Team == Team.Enemy)
                {
                    anyEnemyAlive = true;
                    break;
                }
            }

            if (!anyEnemyAlive)
                ShowVictory();
        }

        public void BroadcastGameState()
        {
            if (!Networking.NetworkManager.Instance.IsHost) return;
            Networking.NetworkManager.Instance.BroadcastGameState(BuildGameStateSync());
        }

        internal Networking.GameStateSync BuildGameStateSync()
        {
            var sync = new Networking.GameStateSync
            {
                currentTurnIndex = turnManager.CurrentTurnIndex
            };

            foreach (var c in activeCharacters)
            {
                if (c == null || c.Stats == null) continue;
                Color color = c.PlayerColor ?? Color.white;
                var handNames = new List<string>();
                if (c.Hand != null)
                {
                    foreach (CardInstance card in c.Hand.Cards)
                        handNames.Add(card != null && card.Data != null ? card.Data.CardName : "");
                }

                sync.characterStates.Add(new Networking.CharacterState
                {
                    characterId = c.GetInstanceID(),
                    ownerPlayerId = c.OwnerPlayerId,
                    teamId = c.TeamId,
                    q = c.CurrentCell.Coordinates.Q,
                    r = c.CurrentCell.Coordinates.R,
                    currentHp = c.Stats.CurrentHealth,
                    maxHp = c.Stats.MaxHealth,
                    armor = c.Stats.Armor,
                    isAlive = c.Stats.IsAlive,
                    name = c.Stats.CharacterName,
                    colorR = color.r,
                    colorG = color.g,
                    colorB = color.b,
                    handCardNames = handNames.ToArray()
                });
            }
            return sync;
        }

        private void HandleActionResolved()
        {
            if (GameState.ActiveMode == GameMode.Host)
                BroadcastGameState();
        }

        private void HandleTurnStartedBroadcast(Character character)
        {
            if (GameState.ActiveMode == GameMode.Host)
                BroadcastGameState();
        }

        internal void HandlePlayCardRequest(ulong senderSteamId, Networking.PlayCardRequest request)
        {
            Character source = FindCharacterForSteamId(senderSteamId);
            if (source == null || turnManager.ActiveCharacter != source)
            {
                BroadcastGameState();
                return;
            }

            IReadOnlyList<CardInstance> hand = source.Hand.Cards;
            if (request.cardHandIndex < 0 || request.cardHandIndex >= hand.Count)
            {
                BroadcastGameState();
                return;
            }

            CardInstance card = hand[request.cardHandIndex];
            var costCards = new List<CardInstance>();
            foreach (int idx in request.costCardHandIndices)
            {
                if (idx < 0 || idx >= hand.Count || idx == request.cardHandIndex) continue;
                costCards.Add(hand[idx]);
            }

            HexCell targetCell = request.hasBoardTarget
                ? grid.GetCell(new HexCoordinates(request.targetQ, request.targetR))
                : null;
            Character targetCharacter = request.targetOwnerPlayerId >= 0
                ? activeCharacters.Find(c => c.OwnerPlayerId == request.targetOwnerPlayerId)
                : null;

            Debug.Log($"[GameManager] Client {senderSteamId} plays '{card.Data?.CardName}'");
            turnManager.PlayCard(card, costCards, targetCell, targetCharacter);
            BroadcastGameState();
        }

        internal void HandlePassRequest(ulong senderSteamId)
        {
            Character source = FindCharacterForSteamId(senderSteamId);
            if (source == null || turnManager.ActiveCharacter != source)
            {
                BroadcastGameState();
                return;
            }

            Debug.Log($"[GameManager] Client {senderSteamId} passed");
            turnManager.Pass();
            BroadcastGameState();
        }

        private Character FindCharacterForSteamId(ulong steamId)
        {
            int playerId;
            if (PlayerIdLookup != null)
            {
                playerId = PlayerIdLookup(steamId);
            }
            else
            {
                var lobby = LobbyManager.Instance.CurrentLobby;
                if (!lobby.HasValue) return null;
                playerId = GameState.GetPlayerIndex(lobby.Value, steamId);
            }
            if (playerId < 0) return null;
            return activeCharacters.Find(c => c.OwnerPlayerId == playerId);
        }

        private void HandleRequestInitialState()
        {
            Debug.Log("[GameManager] Client requested initial state, broadcasting");
            BroadcastGameState();
        }

        private void ShowVictory()
        {
            if (victoryScreen == null)
            {
                var obj = new GameObject("VictoryScreen", typeof(VictoryScreen));
                victoryScreen = obj.GetComponent<VictoryScreen>();
            }
            victoryScreen.Show();
        }

        public ActionResolver ActionResolver => actionResolver;
        public TurnManager TurnManager => turnManager;
        public HexGridManager Grid => grid;

        internal void HandleGameStateReceived(Networking.GameStateSync state)
        {
            if (state == null || state.characterStates.Count == 0) return;

            foreach (var c in activeCharacters)
            {
                if (c == null) continue;
                c.CurrentCell?.ClearOccupant();
                Destroy(c.gameObject);
            }
            activeCharacters.Clear();

            foreach (var cs in state.characterStates)
            {
                if (!cs.isAlive) continue;
                var coords = new HexCoordinates(cs.q, cs.r);
                if (!grid.TryGetCell(coords, out HexCell cell)) continue;

                Character prefab = cs.teamId == 0 ? playerCharacterPrefab : enemyCharacterPrefab;
                if (prefab == null) continue;

                Character character = Instantiate(prefab);
                character.Initialize(cs.ownerPlayerId, cell, teamId: cs.teamId);
                character.SetName(cs.name);
                character.SetPlayerColor(new Color(cs.colorR, cs.colorG, cs.colorB));
                character.RebuildHand(cs.handCardNames);
                character.OnDefeated += HandleCharacterDefeated;
                activeCharacters.Add(character);

                character.Stats.MaxHealth = cs.maxHp;
                character.Stats.CurrentHealth = cs.currentHp;
                character.Stats.Armor = cs.armor;
            }

            turnManager.SetTurnOrder(new List<Character>(activeCharacters));
            turnManager.SetTurn(state.currentTurnIndex);
        }
    }
}
