using System.Collections.Generic;
using UnityEngine;

namespace FantasyBrawl.HexGrid
{
    /// <summary>
    /// Builds a hex-shaped board of the given radius and provides lookup/neighbor/pathing helpers.
    /// Single source of truth for "what's where" on the board.
    /// </summary>
    public class HexGridManager : MonoBehaviour
    {
        [SerializeField] private HexCell hexCellPrefab;
        [SerializeField] private int radius = 4;
        [SerializeField] private float hexSize = 1f;

        private readonly Dictionary<HexCoordinates, HexCell> cells = new Dictionary<HexCoordinates, HexCell>();

        public int Radius => radius;
        public float HexSize => hexSize;

        public void GenerateGrid()
        {
            foreach (var cell in cells.Values)
            {
                if (cell != null) Destroy(cell.gameObject);
            }
            cells.Clear();

            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Mathf.Max(-radius, -q - radius);
                int r2 = Mathf.Min(radius, -q + radius);
                for (int r = r1; r <= r2; r++)
                {
                    CreateCell(new HexCoordinates(q, r));
                }
            }
        }

        private void CreateCell(HexCoordinates coordinates)
        {
            HexCell cell = Instantiate(hexCellPrefab, transform);
            cell.transform.localPosition = coordinates.ToWorldPosition(hexSize);
            cell.Initialize(coordinates);
            cell.name = $"Hex {coordinates}";
            cells[coordinates] = cell;
        }

        public bool TryGetCell(HexCoordinates coordinates, out HexCell cell)
        {
            return cells.TryGetValue(coordinates, out cell);
        }

        public HexCell GetCell(HexCoordinates coordinates)
        {
            cells.TryGetValue(coordinates, out HexCell cell);
            return cell;
        }

        public IEnumerable<HexCell> GetNeighbors(HexCoordinates coordinates)
        {
            for (int dir = 0; dir < 6; dir++)
            {
                HexCoordinates neighborCoords = coordinates.Neighbor(dir);
                if (cells.TryGetValue(neighborCoords, out HexCell neighbor))
                {
                    yield return neighbor;
                }
            }
        }

        /// <summary>All cells within range hexes of the origin (inclusive), useful for movement/threat ranges.</summary>
        public IEnumerable<HexCell> GetCellsInRange(HexCoordinates origin, int range)
        {
            for (int dq = -range; dq <= range; dq++)
            {
                for (int dr = Mathf.Max(-range, -dq - range); dr <= Mathf.Min(range, -dq + range); dr++)
                {
                    HexCoordinates coords = new HexCoordinates(origin.Q + dq, origin.R + dr);
                    if (cells.TryGetValue(coords, out HexCell cell))
                    {
                        yield return cell;
                    }
                }
            }
        }

        /// <summary>
        /// Simple BFS path over walkable cells. Good enough for card-driven movement ranges;
        /// swap for A* later if you add movement costs/terrain weighting.
        /// </summary>
        public List<HexCell> FindPath(HexCoordinates start, HexCoordinates goal, int maxSteps)
        {
            var frontier = new Queue<HexCoordinates>();
            var cameFrom = new Dictionary<HexCoordinates, HexCoordinates>();
            var visited = new HashSet<HexCoordinates> { start };
            frontier.Enqueue(start);

            while (frontier.Count > 0)
            {
                HexCoordinates current = frontier.Dequeue();
                if (current == goal) break;

                foreach (HexCell neighbor in GetNeighbors(current))
                {
                    if (visited.Contains(neighbor.Coordinates)) continue;
                    if (!neighbor.IsWalkable && neighbor.Coordinates != goal) continue;

                    visited.Add(neighbor.Coordinates);
                    cameFrom[neighbor.Coordinates] = current;
                    frontier.Enqueue(neighbor.Coordinates);
                }
            }

            if (!cameFrom.ContainsKey(goal) && start != goal) return null;

            var path = new List<HexCell>();
            HexCoordinates step = goal;
            while (step != start)
            {
                path.Add(GetCell(step));
                if (!cameFrom.TryGetValue(step, out step)) return null;
            }
            path.Reverse();

            return path.Count <= maxSteps ? path : null;
        }
    }
}
