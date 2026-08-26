using System;
using System.Collections.Generic;
using UnityEngine;
using HexGrid;
using Characters;
using Cards;
using Equipment;
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
        private static readonly Color EnemyColor = Color.white;

        private static readonly HexCoordinates[] MultiplayerAllySpawns =
        {
            new HexCoordinates(0, -2),
            new HexCoordinates(1, -2),
            new HexCoordinates(-1, -1),
            new HexCoordinates(-1, -2),
            new HexCoordinates(2, -2),
            new HexCoordinates(1, -3),
            new HexCoordinates(-2, 0),
            new HexCoordinates(-2, -1)
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
        /// Steam id → player index captured at match setup. Kept so requests and
        /// member-leave handling stay correct even after someone leaves the lobby
        /// (the live member list would shift indices).
        /// </summary>
        private readonly Dictionary<ulong, int> steamIdToPlayerId = new Dictionary<ulong, int>();

        /// <summary>
        /// Test seam: maps a sender's Steam id to a lobby player index. When null,
        /// the default lobby-member lookup is used.
        /// </summary>
        internal static Func<ulong, int> PlayerIdLookup;

        private LoadoutScreen loadoutScreen;
        private bool victoryAchieved;
        private bool defeatAchieved;
        private bool matchStarted;

        /// <summary>
        /// Loadouts collected from every player during the loadout phase. The host starts
        /// the level once this matches the number of lobby members.
        /// </summary>
        private readonly Dictionary<int, EquipmentLoadout> receivedLoadouts = new Dictionary<int, EquipmentLoadout>();

        /// <summary>Number of loadouts the host must collect before starting the level.</summary>
        internal int ExpectedLoadoutCount = 1;

        private void Start()
        {
            victoryAchieved = false;
            defeatAchieved = false;
            matchStarted = false;
            GameMode currentMode = GameState.ActiveMode;

            // Make sure this machine knows the lobby's match format even if it only just
            // entered the lobby (the lobby data is authoritative; LobbyManager keeps
            // GameState.CurrentMatchMode in sync, this is a belt-and-suspenders read).
            if (currentMode != GameMode.SinglePlayer && LobbyManager.Instance.CurrentLobby.HasValue)
            {
                string modeData = LobbyManager.Instance.CurrentLobby.Value.GetData("matchmode");
                if (!string.IsNullOrEmpty(modeData)
                    && System.Enum.TryParse<MatchMode>(modeData, true, out MatchMode parsed))
                {
                    GameState.CurrentMatchMode = parsed;
                }
            }

            switch (currentMode)
            {
                case GameMode.SinglePlayer:
                    GameState.LocalPlayerId = 0;
                    SubscribeHostLogic();
                    break;

                case GameMode.Host:
                    GameState.LocalPlayerId = ComputeLocalPlayerId();
                    SubscribeHostLogic();
                    break;

                case GameMode.Client:
                    GameState.LocalPlayerId = ComputeLocalPlayerId();
                    break;
            }

            PauseMenu.GetOrCreate(inputActions);

            if (GameState.ShowLoadoutScreen)
            {
                ShowLoadoutScreen(null);
                return;
            }

            // The Equipment UI starts active in the scene. Hide it when the loadout
            // screen is not shown (e.g. resuming after a scene reload for the next level).
            var gameCanvas = GameObject.Find("Canvas");
            var eqUI = gameCanvas != null ? gameCanvas.transform.Find("Equipment UI")?.gameObject : null;
            if (eqUI != null) eqUI.SetActive(false);

            BeginMatch();
        }

        private void OnDestroy()
        {
            Networking.NetworkManager.OnGameStateReceived -= HandleGameStateReceived;
            Networking.NetworkManager.OnLoadNextLevelReceived -= HandleLoadNextLevelReceived;
            Networking.NetworkManager.OnRequestInitialStateReceived -= HandleRequestInitialState;
            Networking.NetworkManager.OnPlayCardRequestReceived -= HandlePlayCardRequest;
            Networking.NetworkManager.OnPassRequestReceived -= HandlePassRequest;
            Networking.NetworkManager.OnLoadoutSyncReceived -= HandleLoadoutSyncReceived;
            Networking.NetworkManager.OnCardPlayedDisplayReceived -= HandleCardPlayedDisplay;
            LobbyManager.OnLobbyMemberLeft -= HandleLobbyMemberLeft;
            if (turnManager != null)
            {
                turnManager.OnActionResolved -= HandleActionResolved;
                turnManager.OnTurnStarted -= HandleTurnStartedBroadcast;
                turnManager.OnCardPlayed -= HandleCardPlayed;
            }
        }

        private void HandleLoadNextLevelReceived()
        {
            Debug.Log("[GameManager] Received LoadNextLevel from host");
            if (LobbyManager.Instance.CurrentLobby.HasValue)
                Networking.NetworkManager.Instance.StartClient(LobbyManager.Instance.CurrentLobby.Value.Owner.Id);
            GameState.LoadNextLevel();
        }

        private void SubscribeHostLogic()
        {
            Networking.NetworkManager.OnRequestInitialStateReceived += HandleRequestInitialState;
            Networking.NetworkManager.OnPlayCardRequestReceived += HandlePlayCardRequest;
            Networking.NetworkManager.OnPassRequestReceived += HandlePassRequest;
            Networking.NetworkManager.OnLoadoutSyncReceived += HandleLoadoutSyncReceived;
            LobbyManager.OnLobbyMemberLeft += HandleLobbyMemberLeft;
            if (turnManager != null)
            {
                turnManager.OnActionResolved += HandleActionResolved;
                turnManager.OnTurnStarted += HandleTurnStartedBroadcast;
                turnManager.OnCardPlayed += HandleCardPlayed;
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

        /// <summary>
        /// Starts the actual match: builds the board, spawns characters (applying each
        /// player's confirmed loadout), and begins turns. Skipped while the loadout screen
        /// is up.
        /// </summary>
        private void BeginMatch()
        {
            if (matchStarted) return;
            matchStarted = true;

            GameState.ShowLoadoutScreen = false;
            switch (GameState.ActiveMode)
            {
                case GameMode.SinglePlayer:
                    if (playerCharacterPrefab != null && enemyCharacterPrefab != null)
                        StartCoroutine(SetupDynamicMatchDeferred());
                    else
                        SetupMatch();
                    break;

                case GameMode.Host:
                    if (playerCharacterPrefab != null && enemyCharacterPrefab != null)
                        StartCoroutine(SetupDynamicMatchDeferred());
                    else
                        SetupMatch();
                    break;

                case GameMode.Client:
                    BeginClientSync();
                    break;
            }
        }

        /// <summary>Client-side match entry: generate the board and start syncing.</summary>
        private void BeginClientSync()
        {
            grid.GenerateGrid();
            Networking.NetworkManager.OnGameStateReceived += HandleGameStateReceived;
            Networking.NetworkManager.OnLoadNextLevelReceived += HandleLoadNextLevelReceived;
            Networking.NetworkManager.OnCardPlayedDisplayReceived += HandleCardPlayedDisplay;
            Networking.NetworkManager.Instance.SendRequestInitialState();
        }

        private void ShowLoadoutScreen(EquipmentData reward)
        {
            var lobby = LobbyManager.Instance.CurrentLobby;
            ExpectedLoadoutCount = lobby.HasValue ? CountExpectedLoadouts(lobby.Value) : 1;

            loadoutScreen = LoadoutScreen.Create();
            loadoutScreen.Show(reward, OnLoadoutConfirmed);
        }

        private static int CountExpectedLoadouts(Lobby lobby)
        {
            int max = -1;
            foreach (var member in lobby.Members)
                max = Mathf.Max(max, GameState.GetPlayerIndex(lobby, member.Id));
            return max + 1;
        }

        private void OnLoadoutConfirmed()
        {
            var localInventory = PlayerInventoryProvider.GetLocal();

            if (GameState.ActiveMode == GameMode.SinglePlayer)
            {
                GameState.MatchLoadouts[0] = localInventory.Loadout;
                Debug.Log($"[GameManager] Single-player committed loadout={string.Join(",", localInventory.Loadout.ToNames())}");
                DestroyLoadoutScreen();
                StartLevelAfterLoadout();
                return;
            }

            if (LobbyManager.Instance.IsHost)
            {
                AddReceivedLoadout(GameState.LocalPlayerId, localInventory.Loadout);
                TryStartAfterLoadouts();
            }
            else if (!victoryAchieved)
            {
                BeginClientSync();
            }
        }

        private void HandleLoadoutSyncReceived(ulong senderSteamId, Networking.LoadoutSync sync)
        {
            if (GameState.ActiveMode != GameMode.Host) return;

            var lobby = LobbyManager.Instance.CurrentLobby;
            if (!lobby.HasValue) return;

            int playerId = GameState.GetPlayerIndex(lobby.Value, senderSteamId);
            if (playerId < 0) return;

            var library = GameDataLibrary.Instance;
            AddReceivedLoadout(playerId, EquipmentLoadout.FromNames(sync?.loadoutNames, library));
            Debug.Log($"[GameManager] Received loadout for player {playerId} ({receivedLoadouts.Count}/{ExpectedLoadoutCount})");

            TryStartAfterLoadouts();
        }

        internal void AddReceivedLoadout(int playerId, EquipmentLoadout loadout)
        {
            receivedLoadouts[playerId] = loadout;
        }

        internal bool AllLoadoutsReceived()
        {
            return receivedLoadouts.Count >= ExpectedLoadoutCount;
        }

        /// <summary>
        /// Copies the collected loadouts into the persistent match state so they survive
        /// the scene reload into the next level and are applied during match setup.
        /// </summary>
        internal void CommitLoadoutsToMatchState()
        {
            GameState.MatchLoadouts.Clear();
            foreach (var kv in receivedLoadouts)
                GameState.MatchLoadouts[kv.Key] = kv.Value;
        }

        internal void TryStartAfterLoadouts()
        {
            if (GameState.ActiveMode != GameMode.Host) return;
            if (!AllLoadoutsReceived()) return;

            CommitLoadoutsToMatchState();
            DestroyLoadoutScreen();
            StartLevelAfterLoadout();
        }

        private void StartLevelAfterLoadout()
        {
            GameState.ShowLoadoutScreen = false;

            if (victoryAchieved)
            {
                if (GameState.ActiveMode != GameMode.SinglePlayer && Networking.NetworkManager.Instance.IsHost)
                {
                    Networking.NetworkManager.Instance.SendLoadNextLevel();
                    Networking.ReadyTracker.Instance.Reset();
                }
                GameState.LoadNextLevel();
                return;
            }

            BeginMatch();
        }

        private void DestroyLoadoutScreen()
        {
            if (loadoutScreen == null) return;
            var screen = loadoutScreen;
            loadoutScreen = null;
            screen.Close();
        }

        private static EquipmentLoadout GetMatchLoadout(int playerId)
        {
            return GameState.MatchLoadouts.TryGetValue(playerId, out var loadout) ? loadout : null;
        }

        public void SetupMatch()
        {
            grid.GenerateGrid();
            DestroyActiveCharacters();

            foreach (var spawn in spawns)
            {
                var coords = new HexCoordinates(spawn.spawnQ, spawn.spawnR);
                if (!grid.TryGetCell(coords, out HexCell cell))
                {
                    Debug.LogWarning($"Spawn coordinates {coords} are not on the grid.");
                    continue;
                }

                Character character = Instantiate(spawn.characterPrefab);
                character.Initialize(spawn.ownerPlayerId, cell, loadout: GetMatchLoadout(spawn.ownerPlayerId));
                character.OnDefeated += HandleCharacterDefeated;
                activeCharacters.Add(character);
            }

            GameState.MatchLoadouts.Clear();

            turnManager.SetTurnOrder(new List<Character>(activeCharacters));
            turnManager.StartMatch();
        }

        private void DestroyActiveCharacters()
        {
            foreach (Character c in activeCharacters)
            {
                if (c == null) continue;
                c.CancelMoveAnimation();
                if (c.CurrentCell != null) c.CurrentCell.ClearOccupant();
                Destroy(c.gameObject);
            }
            activeCharacters.Clear();
        }

        private void SetupDynamicMatch()
        {
            grid.GenerateGrid();
            DestroyActiveCharacters();

            if (GameState.ActiveMode == GameMode.Host)
                SetupMultiplayerPlayers();
            else
                SetupSinglePlayerPlayer();

            // PvP is purely player-vs-player: every player is on their own team and
            // the last one standing wins, so no AI enemies spawn.
            if (GameState.IsPvP)
            {
                turnManager.SetTurnOrder(new List<Character>(activeCharacters));
                GameState.MatchLoadouts.Clear();
                turnManager.StartMatch();
                return;
            }

            int enemyCount = baseEnemyCount + (GameState.CurrentLevel - 1) * extraEnemiesPerLevel;
            var spawnCells = GetValidSpawnCells(enemyCount);
            int enemyHp = 3;

            for (int i = 0; i < enemyCount && i < spawnCells.Count; i++)
            {
                Character enemy = Instantiate(enemyCharacterPrefab);
                enemy.Stats.MaxHealth = enemyHp;
                enemy.Initialize(GameState.EnemyOwnerPlayerId + i, spawnCells[i], teamId: 1);
                enemy.SetName("Enemy");
                enemy.SetPlayerColor(EnemyColor);
                enemy.OnDefeated += HandleCharacterDefeated;
                activeCharacters.Add(enemy);
            }

            turnManager.SetTurnOrder(new List<Character>(activeCharacters));
            GameState.MatchLoadouts.Clear();
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

            steamIdToPlayerId.Clear();
            for (int i = 0; i < members.Count; i++)
                steamIdToPlayerId[members[i].Id.Value] = i;

            // PvP: place every player as far from everyone else as possible so nobody
            // starts next to an opponent. PvE keeps the fixed ally spawn table.
            var pvpSpawns = GameState.IsPvP ? GetMaxSpreadSpawnCells(members.Count) : null;

            for (int i = 0; i < members.Count; i++)
            {
                HexCell cell;
                if (pvpSpawns != null && i < pvpSpawns.Count)
                {
                    cell = pvpSpawns[i];
                }
                else
                {
                    var coords = i < MultiplayerAllySpawns.Length
                        ? MultiplayerAllySpawns[i]
                        : new HexCoordinates(allySpawnQ, allySpawnR);
                    if (!grid.TryGetCell(coords, out cell)) continue;
                }

                // PvE: every human player shares team 0 (allies). PvP: each player gets
                // a distinct team id so they are their own team and everyone else is an
                // enemy.
                int teamId = GameState.IsPvP ? i + 1 : 0;

                Character player = Instantiate(playerCharacterPrefab);
                player.Initialize(i, cell, teamId: teamId, loadout: GetMatchLoadout(i));
                player.SetName(members[i].Name);
                player.SetPlayerColor(PlayerPalette.GetColor(i));
                player.OnDefeated += HandleCharacterDefeated;
                activeCharacters.Add(player);
            }
        }

        private void SetupSinglePlayerPlayer()
        {
            var playerCoords = new HexCoordinates(allySpawnQ, allySpawnR);
            if (!grid.TryGetCell(playerCoords, out HexCell playerCell)) return;

            Character player = Instantiate(playerCharacterPrefab);
            player.Initialize(0, playerCell, teamId: 0, loadout: GetMatchLoadout(0));
            player.SetName(SteamClient.IsValid ? SteamClient.Name : null);
            player.SetPlayerColor(PlayerPalette.GetColor(0));
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

        /// <summary>
        /// Greedy farthest-point placement for 2-8 players: each next spawn is the walkable
        /// cell that maximizes its minimum hex distance to every already-chosen spawn, so the
        /// whole group spreads as wide as the board allows. Candidate order is sorted by
        /// coordinates so the layout is deterministic across machines and test runs.
        /// </summary>
        private List<HexCell> GetMaxSpreadSpawnCells(int count)
        {
            var candidates = new List<HexCell>(grid.AllCells);
            candidates.RemoveAll(cell => !cell.IsWalkable);

            candidates.Sort((a, b) =>
            {
                var ca = a.Coordinates;
                var cb = b.Coordinates;
                int q = ca.Q.CompareTo(cb.Q);
                return q != 0 ? q : ca.R.CompareTo(cb.R);
            });

            var chosen = new List<HexCell>();
            int take = Mathf.Min(count, candidates.Count);
            for (int n = 0; n < take; n++)
            {
                int bestIndex = -1;
                int bestMinDistance = -1;
                for (int i = 0; i < candidates.Count; i++)
                {
                    int minDistance = int.MaxValue;
                    foreach (var other in chosen)
                    {
                        int distance = candidates[i].Coordinates.DistanceTo(other.Coordinates);
                        if (distance < minDistance) minDistance = distance;
                    }
                    if (minDistance > bestMinDistance)
                    {
                        bestMinDistance = minDistance;
                        bestIndex = i;
                    }
                }
                chosen.Add(candidates[bestIndex]);
                candidates.RemoveAt(bestIndex);
            }
            return chosen;
        }

        private void HandleCharacterDefeated(Character character)
        {
            activeCharacters.Remove(character);
            turnManager.RemoveFromTurnOrder(character);

            if (IsMatchOver())
            {
                ShowVictory();
                return;
            }

            // PvE: once every human player is down while enemies remain, the match is a
            // loss. The host decides and broadcasts it; clients just display it.
            if (GameState.ActiveMode != GameMode.Client && AreAllPlayersDead())
                ShowGameOver();
        }

        /// <summary>True when no human-owned character is left alive.</summary>
        internal bool AreAllPlayersDead()
        {
            foreach (Character c in activeCharacters)
            {
                if (c == null || c.Stats == null || !c.Stats.IsAlive) continue;
                if (c.OwnerPlayerId < GameState.EnemyOwnerPlayerId) return false;
            }
            return true;
        }

        /// <summary>
        /// Match-over decision. PvE: won once no AI enemy is alive. PvP: last team
        /// standing — won when at most one distinct team id remains alive.
        /// </summary>
        internal bool IsMatchOver()
        {
            if (GameState.IsPvP)
            {
                var aliveTeams = new HashSet<int>();
                foreach (Character c in activeCharacters)
                {
                    if (c == null || c.Stats == null || !c.Stats.IsAlive) continue;
                    aliveTeams.Add(c.TeamId);
                }
                return aliveTeams.Count <= 1;
            }

            foreach (Character c in activeCharacters)
            {
                if (c == null || c.Stats == null) continue;
                if (c.OwnerPlayerId >= GameState.EnemyOwnerPlayerId && c.Stats.IsAlive)
                    return false;
            }
            return true;
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
                currentTurnIndex = turnManager.CurrentTurnIndex,
                victory = victoryAchieved,
                gameOver = defeatAchieved
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
                    block = c.Stats.Block,
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
            if (victoryAchieved) return;
            if (GameState.ActiveMode == GameMode.Host)
                BroadcastGameState();
        }

        private void HandleTurnStartedBroadcast(Character character)
        {
            if (victoryAchieved) return;
            if (GameState.ActiveMode == GameMode.Host)
                BroadcastGameState();
        }

        /// <summary>
        /// Host-side (and single-player): a card just resolved. Show it locally and, in a
        /// lobby, broadcast it so every client displays the same card on screen.
        /// </summary>
        private void HandleCardPlayed(CardInstance card, Character actor)
        {
            if (card == null || card.Data == null) return;

            string actorName = actor != null ? actor.Stats.CharacterName : "";
            Debug.Log($"[PlayPopup] OnCardPlayed actor='{actorName}' " +
                      $"ownerId={(actor != null ? actor.OwnerPlayerId : -1)} " +
                      $"local={GameState.LocalPlayerId} mode={GameState.ActiveMode}");

            // The acting player just watched themselves play it — no popup for them.
            // Everyone else gets the real CardData (no name lookup) so any card,
            // present or future, renders as a proper card.
            if (actor == null || actor.OwnerPlayerId != GameState.LocalPlayerId)
                UI.PlayedCardDisplay.Show(card.Data, actorName);

            if (GameState.ActiveMode != GameMode.Host) return;
            Networking.NetworkManager.Instance.BroadcastCardPlayed(new Networking.CardPlayedDisplay
            {
                cardName = card.Data.CardName,
                cost = card.Data.Cost,
                description = card.Data.Description,
                actorName = actorName,
                actorPlayerId = actor != null ? actor.OwnerPlayerId : -1
            });
        }

        /// <summary>
        /// Client-side: the host announced a card play; show it on screen so this player
        /// sees what was played even though only the host resolves card actions. The
        /// local player's own plays are not shown; everyone else's are paced.
        /// </summary>
        private void HandleCardPlayedDisplay(Networking.CardPlayedDisplay display)
        {
            if (display == null || string.IsNullOrEmpty(display.cardName)) return;
            Debug.Log($"[PlayPopup] Broadcast card='{display.cardName}' " +
                      $"actorPlayerId={display.actorPlayerId} local={GameState.LocalPlayerId}");
            if (display.actorPlayerId == GameState.LocalPlayerId) return;
            UI.PlayedCardDisplay.Show(display.cardName, display.actorName,
                display.cost, display.description);
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
            bool played = turnManager.PlayCard(card, costCards, targetCell, targetCharacter);
            if (!played)
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
            else if (steamIdToPlayerId.TryGetValue(steamId, out int mappedId))
            {
                playerId = mappedId;
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

        /// <summary>
        /// Host-only: removes the leaving player's characters from the match and
        /// broadcasts the updated state so clients rebuild without them.
        /// </summary>
        private void HandleLobbyMemberLeft(Lobby lobby, Friend friend)
        {
            if (GameState.ActiveMode != GameMode.Host) return;

            int playerId;
            if (!steamIdToPlayerId.TryGetValue(friend.Id.Value, out playerId))
                playerId = GameState.GetPlayerIndex(lobby, friend.Id.Value);
            if (playerId < 0) return;

            Debug.Log($"[GameManager] Removing characters for left member {friend.Name} (player {playerId})");

            var leaving = new List<Character>();
            foreach (var c in activeCharacters)
            {
                if (c.OwnerPlayerId == playerId) leaving.Add(c);
            }
            if (leaving.Count == 0) return;

            Character activeBefore = turnManager.ActiveCharacter;
            foreach (Character c in leaving)
            {
                activeCharacters.Remove(c);
                turnManager.RemoveFromTurnOrder(c);
                c.CancelMoveAnimation();
                if (c.CurrentCell != null) c.CurrentCell.ClearOccupant();
                Destroy(c.gameObject);
            }

            if (turnManager.ActiveCharacter != activeBefore)
                turnManager.SetTurn(turnManager.CurrentTurnIndex);
            else
                BroadcastGameState();
        }

        private void ShowVictory()
        {
            if (victoryAchieved) return;
            victoryAchieved = true;
            BroadcastGameState();
            // Each machine rolls its own reward from the shared pool, so players never
            // see each other's inventories. Unlocks persist across sessions.
            EquipmentData reward = null;
            var inventory = PlayerInventoryProvider.GetLocal();
            var pool = Equipment.RewardPool.Main;
            if (pool != null)
                reward = pool.RollUnlocked(inventory);
            if (reward != null)
            {
                inventory.Unlock(reward);
                PlayerInventoryProvider.Save(PlayerInventoryProvider.LocalPlayerKey, inventory);
            }

            ShowLoadoutScreen(reward);
        }

        /// <summary>
        /// Every player has died: show the Game Over overlay locally and, on the host,
        /// broadcast it so clients display it too.
        /// </summary>
        private void ShowGameOver()
        {
            if (defeatAchieved || victoryAchieved) return;
            defeatAchieved = true;
            BroadcastGameState();
            UI.GameOverScreen.Show();
        }

        public ActionResolver ActionResolver => actionResolver;
        public TurnManager TurnManager => turnManager;
        public HexGridManager Grid => grid;

        internal void HandleGameStateReceived(Networking.GameStateSync state)
        {
            if (state == null) return;

            if (loadoutScreen != null && state.characterStates.Count > 0 && !state.victory)
                DestroyLoadoutScreen();

            if (state.characterStates.Count > 0)
            {
                var oldPositions = new Dictionary<int, Vector3>();
                foreach (var c in activeCharacters)
                {
                    if (c == null) continue;
                    if (!oldPositions.ContainsKey(c.OwnerPlayerId))
                        oldPositions[c.OwnerPlayerId] = c.transform.position;
                }

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

                    Character prefab = cs.ownerPlayerId >= GameState.EnemyOwnerPlayerId
                        ? enemyCharacterPrefab
                        : playerCharacterPrefab;
                    if (prefab == null) continue;

                    Character character = Instantiate(prefab);
                    if (character.Stats != null)
                        character.Stats.MaxHealth = cs.maxHp;
                    character.Initialize(cs.ownerPlayerId, cell, teamId: cs.teamId,
                        loadout: cs.ownerPlayerId == GameState.LocalPlayerId
                            ? PlayerInventoryProvider.GetLocal().Loadout
                            : null);
                    character.SetName(cs.name);
                    character.SetPlayerColor(new Color(cs.colorR, cs.colorG, cs.colorB));
                    character.RebuildHand(cs.handCardNames);
                    character.OnDefeated += HandleCharacterDefeated;
                    ApplySyncedStats(character, cs);
                    activeCharacters.Add(character);

                    if (oldPositions.TryGetValue(cs.ownerPlayerId, out Vector3 oldPos)
                        && Vector3.Distance(oldPos, cell.transform.position) > 0.01f)
                    {
                        character.AnimateToCell(oldPos, cell);
                    }
                }

                turnManager.SetTurnOrder(new List<Character>(activeCharacters));
                turnManager.SetTurn(state.currentTurnIndex);
            }

            if (state.victory)
                ShowVictory();
            if (state.gameOver)
                UI.GameOverScreen.Show();
        }

        private static void ApplySyncedStats(Character character, Networking.CharacterState cs)
        {
            if (character.Stats == null) return;
            character.Stats.MaxHealth = cs.maxHp;
            character.Stats.CurrentHealth = cs.currentHp;
            character.Stats.Block = cs.block;
            character.Stats.NotifyChanged();
        }
    }
}
