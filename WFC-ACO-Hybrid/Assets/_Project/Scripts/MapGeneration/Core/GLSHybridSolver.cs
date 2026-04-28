using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using _Project.Scripts.MapGeneration.Data;

namespace _Project.Scripts.MapGeneration.Core
{
    /// <summary>
    /// Class for the GLS-WFC hybrid path generation algorithm.
    /// It combines a greedy local search crawler with a WFC solver to generate a path from a start position to an end position.
    /// </summary>
    public class GLSHybridSolver
    {
        private readonly Cell[,,] grid;
        private readonly int mapWidth;
        private readonly int mapFloors;
        private readonly int mapDepth;
        private readonly WFCSolver wfc;

        /// <summary>
        /// Constructor for the GLSHybridSolver class.
        /// Initializes the solver with the grid, width, floors, and depth of the map.
        /// </summary>
        /// <param name="grid">The 3D grid of cells representing the map</param>
        /// <param name="width">Width of the map</param>
        /// <param name="floors">Number of floors in the map</param>
        /// <param name="depth">Depth of the map</param>
        /// <param name="wfcSolver">The WFC solver instance</param>
        public GLSHybridSolver(Cell[,,] grid, int width, int floors, int depth, WFCSolver wfcSolver)
        {
            this.grid = grid;
            this.mapWidth = width;
            this.mapFloors = floors;
            this.mapDepth = depth;
            wfc = wfcSolver;
        }

        /// <summary>
        /// Path generation algorithm using a greedy local search crawler, then WFC fills the remaining cells.
        /// The crawler starts at the start position and tries to reach the end position by collapsing cells along the way.
        /// </summary>
        public List<Vector3Int> RunGLSHybrid(Vector3Int startPos, Vector3Int endPos)
        {
            Cell currentCell = grid[startPos.x, startPos.y, startPos.z];
            Cell endCell = grid[endPos.x, endPos.y, endPos.z];

            // Initialize the GLS crawler with the start cell and an empty path
            bool pathCompleted = false;
            List<Vector3Int> finalPath = new List<Vector3Int> { startPos };
            HashSet<Cell> visitedPath = new HashSet<Cell> { currentCell };

            int steps = 0;
            int maxSteps = CalculateMaxSteps();

            while (!pathCompleted && steps < maxSteps)
            {
                steps++;

                // Check all 6 directions for valid path sockets and prioritize those that lead closer to the end position
                List<int> possibleDirections = new List<int>();
                for (int i = 0; i < 6; i++)
                {
                    string socket = currentCell.CollapsedVariant.Sockets[i];
                    if (socket == "path" || socket.StartsWith("stairs_up"))
                    {
                        possibleDirections.Add(i);
                    }
                }

                // Sort possible directions by distance to the end position
                possibleDirections = possibleDirections.OrderBy(dir =>
                    Vector3Int.Distance(currentCell.GridPosition + GetDirVector(dir), endPos)).ToList();

                bool moved = false;

                // Try to move in one of the possible directions
                foreach (int dir in possibleDirections)
                {
                    Vector3Int nPos = currentCell.GridPosition + GetDirVector(dir);
                    if (!IsInBounds(nPos))
                    {
                        continue;
                    }

                    Cell neighbor = grid[nPos.x, nPos.y, nPos.z];

                    // Check if we reached the end position
                    if (neighbor == endCell)
                    {
                        int oppSide = wfc.GetOppositeSide(dir);
                        string endSocket = endCell.CollapsedVariant.Sockets[oppSide];
                        string mySocket = currentCell.CollapsedVariant.Sockets[dir];

                        if (endSocket == mySocket && (mySocket == "path" || mySocket.StartsWith("stairs_up")))
                        {
                            pathCompleted = true;
                            moved = true;
                            finalPath.Add(endPos);
                            break;
                        }
                    }

                    // Check the free neighbor's socket and move if valid'
                    if (!neighbor.IsCollapsed && !visitedPath.Contains(neighbor))
                    {
                        string requiredEntrySocket = currentCell.CollapsedVariant.Sockets[dir];
                        int entrySide = wfc.GetOppositeSide(dir);

                        List<TileVariant> validPathVariants = neighbor.AvailableVariants.Where(v =>
                        {
                            // Valid socket check
                            if (v.Sockets[entrySide] != requiredEntrySocket) return false;

                            // Check if the neighbor is a stairs_top variant
                            bool isStairsTop = v.Data.name.ToLower().Contains("stairs_top");

                            // Check if the entry side is horizontal (0-3)
                            bool isHorizontalEntry = (entrySide >= 0 && entrySide <= 3);

                            // Stairs_top variants can only be entered horizontally
                            if (isStairsTop && isHorizontalEntry)
                            {
                                return false;
                            }

                            // Valid exit check
                            return HasValidExit(v, entrySide, neighbor.GridPosition, visitedPath, endPos);
                        }).ToList();

                        // Filter out variants with more than 2 path or stairs_up sockets
                        var cleanVariants = validPathVariants.Where(v =>
                            v.Sockets.Count(s => s == "path" || s.StartsWith("stairs_up")) <= 2
                        ).ToList();

                        // If there are cleaner variants, use them
                        if (cleanVariants.Count > 0)
                        {
                            validPathVariants = cleanVariants;
                        }

                        // If there are valid path variants, collapse the neighbor cell to one of them and continue the GLS crawl
                        if (validPathVariants.Count > 0)
                        {
                            wfc.ForceCollapse(neighbor, validPathVariants);

                            visitedPath.Add(neighbor);
                            finalPath.Add(nPos);

                            currentCell = neighbor;
                            moved = true;
                            break;
                        }
                    }
                }

                if (!moved)
                {
                    return null;
                }
            }

            // Attempt to fill the rest of the map using WFC - Contradictions on 2D grids
            bool wfcSuccess = wfc.RunWFC();

            if (!wfcSuccess)
            {
                // Fallback for 2D maps: If environment generation fails, we still return the path.
                if (mapFloors == 1)
                {
                    return finalPath;
                }

                // For 3D maps, we still consider environment failure as a total failure.
                return null;
            }

            return finalPath;
        }


        /// <summary>
        /// Checks if a cell has a valid exit from a given direction.
        /// A valid exit is a path or stairs_up socket that leads to the end position.
        /// </summary>
        /// <param name="variant">The tile variant being checked for valid exits</param>
        /// <param name="entrySide">The side from which we entered the cell (0-5)</param>
        /// <param name="myPos">The position of the current cell in the grid</param>
        /// <param name="visited">Set of cells already visited by the GLS crawler to avoid loops</param>
        /// <param name="endPos">The position of the end cell we are trying to reach</param>
        /// <returns>True if there is at least one valid exit, false otherwise</returns>
        private bool HasValidExit(TileVariant variant, int entrySide, Vector3Int myPos, HashSet<Cell> visited, Vector3Int endPos)
        {
            // Check all 6 directions for valid path sockets
            for (int i = 0; i < 6; i++)
            {
                if (i == entrySide)
                {
                    continue;
                }

                string socket = variant.Sockets[i];
                if (socket == "path" || socket.StartsWith("stairs_up"))
                {
                    Vector3Int exitPos = myPos + GetDirVector(i); 

                    if (exitPos == endPos)
                    {
                        return true;
                    }

                    if (IsInBounds(exitPos))
                    {
                        Cell nextCell = grid[exitPos.x, exitPos.y, exitPos.z];
                        if (!nextCell.IsCollapsed && !visited.Contains(nextCell))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the direction vector for a given direction index.
        /// </summary>
        /// <param name="dir">Direction index (0-5)</param>
        /// <returns></returns>
        private Vector3Int GetDirVector(int dir)
        {
            switch (dir)
            {
                case 0: return new Vector3Int(0, 0, 1);
                case 1: return new Vector3Int(1, 0, 0);
                case 2: return new Vector3Int(0, 0, -1);
                case 3: return new Vector3Int(-1, 0, 0);
                case 4: return new Vector3Int(0, 1, 0);
                case 5: return new Vector3Int(0, -1, 0);
                default: return Vector3Int.zero;
            }
        }

        /// <summary>
        /// Checks if a position is within the map bounds.
        /// </summary>
        /// <param name="pos">The position to check</param>
        /// <returns>True if the position is within bounds, false otherwise</returns>
        private bool IsInBounds(Vector3Int pos)
        {
            return pos.x >= 0 && pos.x < mapWidth && pos.y >= 0 && pos.y < mapFloors && pos.z >= 0 && pos.z < mapDepth;
        }

        /// <summary>
        /// Calculates the maximum number of steps (GLS crawls) allowed based on the map size.
        /// </summary>
        /// <returns>The maximum number of steps for the GLS crawler</returns>
        private int CalculateMaxSteps()
        {
            int totalCells = mapWidth * mapFloors * mapDepth;
            return totalCells * 2;
        }
        
    }
}
