using System.Collections.Generic;
using UnityEngine;
using _Project.Scripts.MapGeneration.Core;

namespace _Project.Scripts.Pathfinding
{
    public class PathValidator
    {
        private Cell[,,] grid;
        private int mapWidth;
        private int mapFloors;
        private int mapDepth;

        /// <summary>
        /// Constructor for the PathValidator class.
        /// Initializes the validator with the grid, width, floors, and depth of the map.
        /// </summary>
        /// <param name="grid">The 3D grid of cells representing the map</param>
        /// <param name="width">Width of the map</param>
        /// <param name="floors">Number of floors in the map</param>
        /// <param name="depth">Depth of the map</param>
        public PathValidator(Cell[,,] grid, int width, int floors, int depth)
        {
            this.grid = grid;
            this.mapWidth = width;
            this.mapFloors = floors;
            this.mapDepth = depth;
        }

        /// <summary>
        /// DFS algorithm to check if a path exists between start and finish positions.
        /// It uses a stack to explore the grid and a hash set to keep track of reached positions.
        /// </summary>
        public bool IsPathPossibleDFS(Vector3Int start, Vector3Int end)
        {
            Stack<Vector3Int> frontier = new Stack<Vector3Int>();
            frontier.Push(start);
            HashSet<Vector3Int> reached = new HashSet<Vector3Int> { start };

            while (frontier.Count > 0)
            {
                Vector3Int current = frontier.Pop();
                
                if (current == end)
                {
                    return true;
                }

                foreach (Vector3Int next in GetPathNeighbors(current))
                {
                    if (reached.Add(next))
                    {
                        frontier.Push(next);
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Returns a list of positions that are adjacent to the current position and have a path socket.
        /// </summary>
        /// <param name="pos">Current position in the grid</param>
        /// <returns></returns>
        private List<Vector3Int> GetPathNeighbors(Vector3Int pos)
        {
            List<Vector3Int> neighbors = new List<Vector3Int>();
            Cell currentCell = grid[pos.x, pos.y, pos.z];

            if (currentCell.CollapsedVariant == null)
            {
                return neighbors;
            }

            // Directions of the neighbors
            Vector3Int[] directions = { 
                new Vector3Int(0, 0, 1),    // North - 0
                new Vector3Int(1, 0, 0),    // East - 1
                new Vector3Int(0, 0, -1),   // South - 2
                new Vector3Int(-1, 0, 0),   // West - 3
                new Vector3Int(0, 1, 0),    // Up - 4
                new Vector3Int(0, -1, 0)    // Down - 5
            };

            for (int i = 0; i < 6; i++)
            {
                string socket = currentCell.CollapsedVariant.Sockets[i];   
                
                if (socket == "path" || socket.StartsWith("path_vertical") || socket.StartsWith("stairs_up"))
                {
                    // Check if the neighbor is within the map bounds
                    Vector3Int nPos = pos + directions[i];
                    if (nPos.x >= 0 && nPos.x < mapWidth && nPos.y >= 0 &&
                        nPos.y < mapFloors && nPos.z >= 0 && nPos.z < mapDepth)
                    {
                        Cell neighborCell = grid[nPos.x, nPos.y, nPos.z];
                        int oppositeSide = GetOppositeSide(i);

                        if (neighborCell.CollapsedVariant != null)
                        {
                            // Check if the neighbor has a path socket on the opposite side
                            string neighborSocket = neighborCell.CollapsedVariant.Sockets[oppositeSide];
                            if (neighborSocket == "path" || neighborSocket.StartsWith("path_vertical") || neighborSocket.StartsWith("stairs_up"))
                            {
                                neighbors.Add(nPos);
                            }
                        }
                    }
                }
            }
            return neighbors;
        }

        /// <summary>
        /// Returns the opposite side of a direction index.
        /// </summary>
        /// <param name="directionIndex">Index of the direction North, South, ...</param>
        /// <returns></returns>
        private int GetOppositeSide(int directionIndex)
        {
            switch (directionIndex)
            {
                case < 4:
                    return (directionIndex + 2) % 4; // North -> South, East -> West...
                case 4:
                    return 5; // Up -> Down
                default:
                    return 4; // Down -> Up
            }
        }
    }
}