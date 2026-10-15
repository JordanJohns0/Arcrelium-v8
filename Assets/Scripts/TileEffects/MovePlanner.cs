using System.Collections.Generic;
using HexGrid;

namespace TileEffects
{
    /// <summary>
    /// A planned move: every cell the mover enters (slide destinations included, in order), the
    /// number of deliberate steps it costs, and the cell it finally rests on.
    /// </summary>
    public sealed class MoveRoute
    {
        public List<HexCell> Cells { get; } = new List<HexCell>();
        public int Steps { get; set; }
        public HexCell Resting { get; set; }

        public MoveRoute Copy()
        {
            var copy = new MoveRoute { Steps = Steps, Resting = Resting };
            copy.Cells.AddRange(Cells);
            return copy;
        }
    }

    /// <summary>
    /// The single rule for where a move ends. Stepping onto a slippery tile (oil) costs one step
    /// like any other, and the character then slides one tile further on in the direction it was
    /// moving; the slide itself is free. Chained slides are capped by
    /// <see cref="TileEffectSystem.MaxSlipChain"/>. A slide only happens when the landing cell is
    /// walkable; otherwise the character stops on the oil.
    ///
    /// Route planning (targeting highlights, the hover preview, AI candidates) and route
    /// resolution (<see cref="TileEffectSystem.MoveCharacter"/>) both use these rules, so the path
    /// a player or enemy sees is the path the character actually walks. Plain logic, no
    /// MonoBehaviour state, so it can be tested directly.
    /// </summary>
    public static class MovePlanner
    {
        /// <summary>
        /// The cell a character entering <paramref name="onTile"/> from <paramref name="from"/> slides
        /// to, or null when the tile is not slippery, the cells are not adjacent, or the slide runs
        /// off the board. Does not check whether the landing cell is walkable.
        /// </summary>
        public static HexCell SlideDestination(HexGridManager grid, HexCell from, HexCell onTile)
        {
            if (grid == null || from == null || onTile == null) return null;
            if (!onTile.HasEffect(effect => effect.Slippery)) return null;

            for (int dir = 0; dir < 6; dir++)
            {
                if (onTile.Coordinates.Neighbor(dir) == from.Coordinates)
                    return grid.GetCell(onTile.Coordinates.Neighbor((dir + 3) % 6));
            }
            return null;
        }

        /// <summary>
        /// Where a mover ends after entering <paramref name="entered"/> from <paramref name="from"/>,
        /// following any oil slides. Each slide destination is appended to <paramref name="appended"/>.
        /// </summary>
        public static HexCell RestingAfterEntering(HexGridManager grid, HexCell from, HexCell entered,
            List<HexCell> appended, HexCell vacated)
        {
            HexCell previous = from;
            HexCell current = entered;
            int budget = TileEffectSystem.MaxSlipChain;

            while (current != null && budget-- > 0)
            {
                HexCell slide = SlideDestination(grid, previous, current);
                if (slide == null || slide == current || !CanEnter(slide, vacated)) break;

                appended?.Add(slide);
                previous = current;
                current = slide;
            }

            return current;
        }

        /// <summary>
        /// Whether a mover can step onto <paramref name="cell"/>. The planner works on the board as it is
        /// before the move, so the cell the mover is leaving (<paramref name="vacated"/>) still looks occupied;
        /// it is free once the mover has left, so it counts as enterable here.
        /// </summary>
        private static bool CanEnter(HexCell cell, HexCell vacated)
        {
            return cell != null && (cell.IsWalkable || cell == vacated);
        }

        /// <summary>
        /// Every cell the mover can end a move on with at most <paramref name="maxSteps"/> steps,
        /// each with the shortest route to it. Built breadth-first by steps, so the first route found
        /// to a cell is a shortest one.
        /// </summary>
        public static Dictionary<HexCell, MoveRoute> Reachable(HexGridManager grid, HexCell start, int maxSteps)
        {
            var routes = new Dictionary<HexCell, MoveRoute>();
            if (grid == null || start == null) return routes;

            routes[start] = new MoveRoute { Resting = start };
            var frontier = new Queue<HexCell>();
            frontier.Enqueue(start);

            while (frontier.Count > 0)
            {
                HexCell here = frontier.Dequeue();
                MoveRoute hereRoute = routes[here];
                if (hereRoute.Steps >= maxSteps) continue;

                foreach (HexCell neighbor in grid.GetNeighbors(here.Coordinates))
                {
                    if (neighbor == null || !CanEnter(neighbor, start)) continue;

                    var appended = new List<HexCell> { neighbor };
                    HexCell resting = RestingAfterEntering(grid, here, neighbor, appended, start);
                    if (resting == null || routes.ContainsKey(resting)) continue;

                    MoveRoute route = hereRoute.Copy();
                    route.Cells.AddRange(appended);
                    route.Steps = hereRoute.Steps + 1;
                    route.Resting = resting;

                    routes[resting] = route;
                    frontier.Enqueue(resting);
                }
            }

            return routes;
        }

        /// <summary>
        /// The shortest route that ends on <paramref name="goal"/> within <paramref name="maxSteps"/>,
        /// or null when the goal cannot be a resting cell (unreachable, or it is an oil tile the
        /// character slides off of). Staying on the start cell is a zero-step route.
        /// </summary>
        public static MoveRoute FindRoute(HexGridManager grid, HexCell start, HexCell goal, int maxSteps)
        {
            if (grid == null || start == null || goal == null) return null;
            if (goal == start) return new MoveRoute { Resting = start };

            return Reachable(grid, start, maxSteps).TryGetValue(goal, out MoveRoute route) ? route : null;
        }

        /// <summary>
        /// Checks an explicit route, cell by cell, the way it will be walked. Each entry is either
        /// the character's current cell (already reached by a slide, so free) or a walkable neighbour of
        /// where the character is at that point (one step). Returns the simulated route, or null when
        /// the route is illegal or costs more than <paramref name="maxSteps"/>.
        /// </summary>
        public static MoveRoute Validate(HexGridManager grid, HexCell start, IReadOnlyList<HexCell> path, int maxSteps)
        {
            if (grid == null || start == null || path == null || path.Count == 0) return null;

            var route = new MoveRoute { Resting = start };
            HexCell position = start;

            foreach (HexCell cell in path)
            {
                if (cell == null) return null;
                if (cell == position) continue;

                if (position.Coordinates.DistanceTo(cell.Coordinates) != 1) return null;
                if (!CanEnter(cell, start)) return null;

                route.Cells.Add(cell);
                route.Steps++;
                if (route.Steps > maxSteps) return null;

                position = RestingAfterEntering(grid, position, cell, route.Cells, start);
            }

            route.Resting = position;
            return route;
        }
    }
}
