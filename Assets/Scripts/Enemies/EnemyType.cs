using Characters;
using UnityEngine;

namespace Enemies
{
    /// <summary>A single enemy archetype that the group generator can spawn.</summary>
    [CreateAssetMenu(fileName = "NewEnemyType", menuName = "Arcrelium/Enemy Type")]
    public class EnemyType : ScriptableObject
    {
        public string EnemyName = "New Enemy";

        /// <summary>Prefab spawned for each copy. Falls back to the GameManager's default enemy prefab when unset.</summary>
        public Character characterPrefab;

        /// <summary>How much of the encounter difficulty budget a single copy costs.</summary>
        [Min(1)] public int PointCost = 10;

        /// <summary>Minimum guaranteed copies, even when that overshoots the budget.</summary>
        [Min(0)] public int MinCount = 0;
    }
}