using System.Collections.Generic;
using UnityEngine;

namespace Enemies
{
    /// <summary>A named pool of enemy archetypes used to build a single encounter.</summary>
    [CreateAssetMenu(fileName = "NewEnemyGroup", menuName = "Arcrelium/Enemy Group")]
    public class EnemyGroup : ScriptableObject
    {
        public string GroupName = "New Group";

        public List<EnemyType> MemberTypes = new List<EnemyType>();
    }
}