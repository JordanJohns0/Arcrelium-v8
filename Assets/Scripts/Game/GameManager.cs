using System.Collections.Generic;
using UnityEngine;
using HexGrid;
using Characters;
using UI;

namespace Game
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private HexGridManager grid;
        [SerializeField] private ActionResolver actionResolver;
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private Character playerCharacterPrefab;
        [SerializeField] private Character enemyCharacterPrefab;
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

        private readonly List<Character> activeCharacters = new List<Character>();
        private VictoryScreen victoryScreen;

        private void Start()
        {
            if (playerCharacterPrefab != null && enemyCharacterPrefab != null)
                SetupDynamicMatch();
            else
                SetupMatch();
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

            Debug.Log($"[GameManager] SetupDynamicMatch. playerPrefab: {playerCharacterPrefab?.name}, enemyPrefab: {enemyCharacterPrefab?.name}");

            var playerCoords = new HexCoordinates(allySpawnQ, allySpawnR);
            if (grid.TryGetCell(playerCoords, out HexCell playerCell))
            {
                Character player = Instantiate(playerCharacterPrefab);
                Debug.Log($"[GameManager] Player spawned at {playerCell.transform.position}. Renderers: {player.GetComponentsInChildren<Renderer>().Length}");
                foreach (var r in player.GetComponentsInChildren<Renderer>())
                    Debug.Log($"[GameManager]   Renderer: {r.name}, enabled: {r.enabled}, material: {r.sharedMaterial?.name}, shader: {r.sharedMaterial?.shader?.name}");
                player.Initialize(0, playerCell);
                player.OnDefeated += HandleCharacterDefeated;
                activeCharacters.Add(player);
            }

            int enemyCount = baseEnemyCount + (GameState.CurrentLevel - 1) * extraEnemiesPerLevel;
            var spawnCells = GetValidSpawnCells(enemyCount);
            int enemyHp = 2 + GameState.CurrentLevel;
            int enemyArmor = Mathf.Max(0, GameState.CurrentLevel - 1);

            for (int i = 0; i < enemyCount && i < spawnCells.Count; i++)
            {
                Character enemy = Instantiate(enemyCharacterPrefab);
                enemy.Stats.MaxHealth = enemyHp;
                enemy.Stats.Armor = enemyArmor;
                enemy.Initialize(1, spawnCells[i]);
                enemy.OnDefeated += HandleCharacterDefeated;
                activeCharacters.Add(enemy);
            }

            turnManager.SetTurnOrder(new List<Character>(activeCharacters));
            turnManager.StartMatch();
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
                if (activeCharacters[i].OwnerPlayerId != 0)
                {
                    anyEnemyAlive = true;
                    break;
                }
            }

            if (!anyEnemyAlive)
                ShowVictory();
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
    }
}
