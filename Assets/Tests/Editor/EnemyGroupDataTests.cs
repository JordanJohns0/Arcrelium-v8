using System.Collections.Generic;
using System.Linq;
using Enemies;
using NUnit.Framework;
using UnityEditor;

namespace Tests.Enemies
{
    /// <summary>Guards user-authored EnemyGroup assets so they always produce a valid plan.</summary>
    [TestFixture]
    public class EnemyGroupDataTests
    {
        [Test]
        public void EveryGroup_HasValidStructure()
        {
            var problems = new List<string>();

            foreach (EnemyGroup group in AssetDatabase.FindAssets("t:EnemyGroup")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<EnemyGroup>(path)))
            {
                if (group == null)
                {
                    problems.Add("A null EnemyGroup asset exists.");
                    continue;
                }

                if (group.MemberTypes == null || group.MemberTypes.Count == 0)
                {
                    problems.Add($"'{group.name}': must specify at least one member type.");
                    continue;
                }

                for (int i = 0; i < group.MemberTypes.Count; i++)
                {
                    EnemyType type = group.MemberTypes[i];
                    if (type == null)
                    {
                        problems.Add($"'{group.name}': member index {i} is unbound (missing enemy type).");
                        continue;
                    }

                    if (type.PointCost < 1)
                        problems.Add($"'{group.name}' member '{type.name}': PointCost must be at least 1.");

                    if (string.IsNullOrEmpty(type.EnemyName))
                        problems.Add($"'{group.name}' member '{type.name}': EnemyName cannot be empty.");
                }
            }

            Assert.IsEmpty(problems, "Enemy groups that break generation:\n" + string.Join("\n", problems));
        }
    }
}