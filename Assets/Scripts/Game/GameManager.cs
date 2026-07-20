using System.Collections.Generic;
using UnityEngine;
using HexGrid;
using Characters;

namespace Game
{
    /// <summary>
    /// Top-level entry point: builds the board, spawns characters onto it, and starts the match.
    /// Wire this up in an empty scene with a HexGridManager, ActionResolver, and TurnManager
    /// on sibling/child objects and assign them here.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private HexGridManager grid;
        [SerializeField] private ActionResolver actionResolver;
        [SerializeField] private TurnManager turnManager;

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

        private void Start()
        {
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

        private void HandleCharacterDefeated(Character character)
        {
            activeCharacters.Remove(character);
            turnManager.RemoveFromTurnOrder(character);
            // Add win-condition checks here (e.g. one player's team fully defeated).
        }

        public ActionResolver ActionResolver => actionResolver;
        public TurnManager TurnManager => turnManager;
        public HexGridManager Grid => grid;
    }
}
