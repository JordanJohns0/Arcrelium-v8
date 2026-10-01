using UnityEngine;
using Characters;
using Game;
using HexGrid;
using TileEffects;

namespace Cards.Effects
{
    /// <summary>
    /// Rises an immovable rock entity (a Character marked as a structure) on the target tile.
    /// The rock blocks movement and can be damaged/destroyed, but never moves and never takes
    /// turns. In pure-data harnesses (no live board) the effect is inert so validation never
    /// spawns board entities.
    /// </summary>
    [CreateAssetMenu(fileName = "SpawnRockEffect", menuName = "Arcrelium/Effects/Spawn Rock")]
    public class SpawnRockEffect : CardEffectSO
    {
        [Tooltip("The rock prefab that rises on the target tile (must carry a Character).")]
        public GameObject rockPrefab;

        public override bool CanExecute(CardContext context)
        {
            if (context.TargetCell == null) return false;
            if (context.Source == null || context.Source.CurrentCell == null) return false;
            return context.Source.CurrentCell.Coordinates.DistanceTo(context.TargetCell.Coordinates)
                <= context.EffectiveRange;
        }

        public override void Execute(CardContext context)
        {
            if (context.TargetCell == null) return;
            if (TileEffectSystem.Instance == null) return;
            if (!context.TargetCell.IsWalkable) return;
            if (rockPrefab == null) return;

            GameObject go = Object.Instantiate(rockPrefab);
            Character rock = go.GetComponent<Character>();
            if (rock == null)
            {
                if (Application.isPlaying) Object.Destroy(go);
                else Object.DestroyImmediate(go);
                return;
            }

            // Initialize resets Block (and build) from the prefab, so capture the desired
            // block up front and restore it after. The rock is a neutral board fixture: it is
            // owned like an enemy (never the local player, or the hand/HUD would treat the
            // rock as the player) and set to team -1 so every team sees it as a valid target.
            int block = rock.Stats != null ? rock.Stats.Block : 0;
            rock.Initialize(GameState.EnemyOwnerPlayerId, context.TargetCell, 0);
            rock.MarkAsStructure();
            rock.SetTeamId(-1); // neutral: an enemy of every team, so anyone can attack it
            if (rock.Stats != null && block > 0) rock.Stats.Block = block;

            var manager = Object.FindFirstObjectByType<GameManager>();
            if (manager != null) manager.RegisterStructure(rock);
        }
    }
}
