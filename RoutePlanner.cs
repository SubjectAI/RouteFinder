 using System.Collections.Generic;

namespace RouteFinder
{
    /// <summary>
    /// Provides route‐finding functionality using the prebuilt grid and A* algorithm.
    /// </summary>
    public static class RoutePlanner
    {
        /// <summary>
        /// Finds the optimal path between two grid cells.
        /// Assumes that InitializeGrid, AnnotateTerrain, and BuildAdjacency have already been called.
        /// </summary>
        /// <param name="grid">The 2D grid of GridCell? objects (null entries are impassable).</param>
        /// <param name="start">The start GridCell.</param>
        /// <param name="goal">The goal GridCell.</param>
        /// <returns>A list of GridCell representing the lowest‐cost path, including start and goal.</returns>
        public static List<GridCell> FindOptimalRoute(GridCell?[,] grid, GridCell start, GridCell goal)
        {
            // The Program class contains the AStarSearch method which uses the global grid and adjacency.
            return Program.AStarSearch(start, goal);
        }
    }
}