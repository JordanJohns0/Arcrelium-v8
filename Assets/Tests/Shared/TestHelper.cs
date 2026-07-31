using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Characters;
using Cards;
using HexGrid;

namespace Tests
{
    public static class TestHelper
    {
        private static readonly FieldInfo deckField = typeof(Character).GetField("deck", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo cellsField = typeof(HexGridManager).GetField("cells", BindingFlags.NonPublic | BindingFlags.Instance);

        public static HexCell CreateCell(int q, int r)
        {
            var go = new GameObject($"Cell {q},{r}");
            var cell = go.AddComponent<HexCell>();
            cell.Initialize(new HexCoordinates(q, r));
            return cell;
        }

        public static Character CreateCharacter(int ownerId, int q, int r)
        {
            return CreateCharacter(ownerId, q, r, -1);
        }

        public static Character CreateCharacter(int ownerId, int q, int r, int teamId)
        {
            var cell = CreateCell(q, r);
            var go = new GameObject($"Character {ownerId}");
            var character = go.AddComponent<Character>();

            var deck = new Deck();
            deckField.SetValue(character, deck);

            character.Initialize(ownerId, cell, 0, teamId);
            return character;
        }

        public static HexGridManager CreateTestGrid(int radius)
        {
            var gridGo = new GameObject("TestGrid");
            var grid = gridGo.AddComponent<HexGridManager>();

            var cells = (Dictionary<HexCoordinates, HexCell>)cellsField.GetValue(grid);

            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Mathf.Max(-radius, -q - radius);
                int r2 = Mathf.Min(radius, -q + radius);
                for (int r = r1; r <= r2; r++)
                {
                    var coords = new HexCoordinates(q, r);
                    var cell = CreateCell(q, r);
                    cell.transform.SetParent(gridGo.transform);
                    cells[coords] = cell;
                }
            }

            return grid;
        }

        public static Character CreateCharacterOnGrid(HexGridManager grid, int ownerId, int q, int r)
        {
            var cells = (Dictionary<HexCoordinates, HexCell>)cellsField.GetValue(grid);
            var coords = new HexCoordinates(q, r);

            HexCell cell;
            if (!cells.TryGetValue(coords, out cell))
            {
                cell = CreateCell(q, r);
                cell.transform.SetParent(grid.transform);
                cells[coords] = cell;
            }

            var go = new GameObject($"Character {ownerId}");
            var character = go.AddComponent<Character>();

            var deck = new Deck();
            deckField.SetValue(character, deck);

            character.Initialize(ownerId, cell, 0);
            return character;
        }

        public static CardData MakeCard(string name, TargetType targetType = TargetType.Self, int cost = 0, CardEffectSO effect = null)
        {
            var data = ScriptableObject.CreateInstance<CardData>();
            data.CardName = name;
            data.TargetType = targetType;
            data.Cost = cost;
            data.Effect = effect;
            return data;
        }

        /// <summary>
        /// Yields until the condition is true or the timeout elapses. Uses
        /// Time.realtimeSinceStartup so it works in both edit- and play-mode tests.
        /// Callers should assert the condition after this returns.
        /// </summary>
        public static IEnumerator WaitUntilOrTimeout(System.Func<bool> condition, float timeout = 3f)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - start < timeout)
            {
                yield return null;
            }
        }
    }
}
