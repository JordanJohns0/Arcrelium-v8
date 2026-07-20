using UnityEngine;
using Characters;
using HexGrid;

namespace Tests
{
    public static class TestHelper
    {
        public static HexCell CreateCell(int q, int r)
        {
            var go = new GameObject($"Cell {q},{r}");
            var cell = go.AddComponent<HexCell>();
            cell.Initialize(new HexCoordinates(q, r));
            return cell;
        }

        public static Character CreateCharacter(int ownerId, int q, int r)
        {
            var cell = CreateCell(q, r);
            var go = new GameObject($"Character {ownerId}");
            var character = go.AddComponent<Character>();
            character.Initialize(ownerId, cell, 0);
            return character;
        }
    }
}
