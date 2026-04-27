using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace _Project.Scripts.MapGeneration.Core
{
    /// <summary>
    /// Class representing the ACO-WFC hybrid path generation algorithm.
    /// 
    /// </summary>
    public class ACOHybridSolver
    {
        private readonly Cell[,,] grid; // Grid representation of the map
        private readonly WFCSolver wfc; // WFC solver instance
        
        // Map dimensions
        private readonly int width;
        private readonly int floors;
        private readonly int depth;
        
        // ACO parameters
        private const float Alpha = 1.0f; // Pheromone influence
        private readonly float beta; // Heuristic weight
        private const float InitialPheromone = 0.1f; // Initial pheromone value
        private const float Rho = 0.15f; // Pheromone evaporation rate
        private const float Q = 100.0f; // Pheromone award constant
        private readonly int colonySize; // Number of agents in the colony
        private readonly int maxIterations; // Maximum number of colony iterations
        
        private float[,,] pheromones;

        /// <summary>
        /// Constructor for the ACOHybridSolver class.
        /// Initializes the solver with the grid, map dimensions, WFC solver, and ACO parameters.
        /// </summary>
        public ACOHybridSolver(Cell[,,] grid, int mapWidth, int mapFloors, int mapDepth,
            WFCSolver wfc, float beta = -1, int colonySize= -1, int iterations = -1)
        {
            this.grid = grid;
            this.width = mapWidth;
            this.floors = mapFloors;
            this.depth = mapDepth;
            this.wfc = wfc;
            this.beta = beta > 0 ? beta : 7.9f;
            
            // Check for manual overrides of the parameters - benchmark
            if (colonySize > 0 && iterations > 0)
            {
                this.colonySize = colonySize;
                this.maxIterations = iterations;
            }
            else
            {
                // Calculate dynamic parameters based on the number of cells in the map
                int cells = mapWidth * mapFloors * mapDepth;
                float sqrt = Mathf.Sqrt(cells);
                this.colonySize = Mathf.Clamp(Mathf.RoundToInt(sqrt * 0.5f), 10, 300);
                this.maxIterations = Mathf.Clamp(Mathf.RoundToInt(sqrt * 0.2f), 8, 100);
            }
            
            InitializePheromones();
        }

        /// <summary>
        /// Initializes the pheromone matrix with the initial value.
        /// </summary>
        private void InitializePheromones()
        {
            pheromones = new float[width, floors, depth];
            
            for(int x = 0; x < width; x++)
            {
                for(int y = 0; y < floors; y++)
                {
                    for(int z = 0; z < depth; z++)
                    {
                        pheromones[x, y, z] = InitialPheromone;
                    }
                }
            }
        }

        /// <summary>
        /// Path generation algorithm using an ant colony optimization approach.
        /// The algorithm explores the map using multiple ants, each with its own pheromone influence.
        /// The ants explore the map until they reach the target position or find a dead end.
        /// After each iteration, the pheromone levels are updated based on the successful paths found by the ants.
        /// The resulting path is simulated in the WFC solver to check if it's valid.
        /// If the path is valid, the pheromones are awarded and the WFC solver is run with the path.
        /// If the path is not valid, the pheromones are penalized and the simulation is repeated.
        /// </summary>
        /// <param name="startPos">Starting position of the ants</param>
        /// <param name="endPos">Target position the ants are trying to reach</param>
        /// <returns></returns>
        public List<Vector3Int> RunACOHybrid(Vector3Int startPos, Vector3Int endPos)
        {
            // Initialization
            List<Vector3Int> bestValidPath = null;
            int bestLength = int.MaxValue;
            int maxAntSteps = width * floors * depth;

            // Main loop of the algorithm
            for (int iter = 0; iter < maxIterations; iter++)
            {
                List<Ant> successfulAnts = new List<Ant>();

                // Run ColonySize ants to explore the map and find paths to the target position
                for (int i = 0; i < colonySize; i++)
                {
                    Ant ant = new Ant(grid, pheromones, endPos, width, floors, depth, Alpha, beta);
                    
                    ant.Explore(startPos, maxAntSteps);
                    
                    if(ant.ReachedTarget)
                    {
                        successfulAnts.Add(ant);
                    }
                }

                // If any ants reached the target, update the pheromones based on the successful paths
                if (successfulAnts.Count > 0)
                {
                    // Find the best path from the successful ants
                    List<List<Vector3Int>> cleanedPaths = successfulAnts.Select(a => RemoveLoops(a.Path)).ToList();

                    List<Vector3Int> bestIterAntPath = cleanedPaths.OrderBy(p => p.Count).First();
                        
                    wfc.SaveSnapshot(); // Saves the current state of the WFC solver before simulating the path
                    
                    // Dry run the path in WFC to check if it's valid'
                    bool isBuildable = SimulatePathInWFC(bestIterAntPath);
                    
                    wfc.RestoreSnapshot(); // Restores the WFC state after simulation

                    // If the path is valid, rewarded with pheromones
                    if (isBuildable)
                    {
                        // Update path if the new path is shorter than the best found so far
                        if (bestIterAntPath.Count < bestLength)
                        {
                            bestLength = bestIterAntPath.Count;
                            bestValidPath = new List<Vector3Int>(bestIterAntPath);
                        }

                        // Deposit pheromones on the path based on the length of the path
                        DepositPheromones(bestIterAntPath, Q / bestIterAntPath.Count);
                    }
                    
                    else
                    {
                        // Penalize the path if it's not valid 
                        PenalizePath(bestIterAntPath);
                    }
                }
                // Evaporate pheromones after each iteration
                EvaporatePheromones();
            }

            // If a valid path was found, run the WFC solver with the best path and finish the generation
            if (bestValidPath != null)
            {
                ApplyConstraintsToWFC(bestValidPath);
                if (wfc.RunWFC())
                {
                    return bestValidPath;
                }
            }
            
            return null; // No valid path found
        }

        /// <summary>
        /// Simulates the path in the WFC solver.
        /// Goes through each cell in the path and forces the collapse of the cell to a variant that has a path socket.
        /// If any cell in the path cannot be collapsed to a path variant, the simulation fails and returns false.
        /// </summary>
        /// <param name="path">List of grid positions representing the path to be simulated in the WFC solver</param>
        /// <returns></returns>
        private bool SimulatePathInWFC(List<Vector3Int> path)
        {
            {
                for (int i = 0; i < path.Count; i++)
                {
                    Cell cell = grid[path[i].x, path[i].y, path[i].z];
                    
                    // Skip collapsed cells (start and finish)
                    if (cell.IsCollapsed)
                    {
                        continue;
                    }

                    // Get the required directions for the current cell based on the path
                    List<int> requiredDirections = new List<int>();
                    if (i > 0) requiredDirections.Add(GetDirectionIndex(path[i], path[i - 1]));
                    if (i < path.Count - 1) requiredDirections.Add(GetDirectionIndex(path[i], path[i + 1]));

                    var pathVariants = cell.AvailableVariants.Where(v =>
                    {
                        // Check for path sockets in all required directions
                        foreach (int dirIndex in requiredDirections)
                        {
                            string socket = v.Sockets[dirIndex];
                            if (socket != "path" && !socket.StartsWith("stairs_up"))
                            {
                                return false; 
                            }
                        }

                        return true;
                    }).ToList();

                    if (pathVariants.Count == 0) return false;

                    wfc.ForceCollapse(cell, pathVariants);
                }

                return !wfc.HasContradiction;
            }
        }
        
        /// <summary>
        /// Removes loops from the path.
        /// </summary>
        /// <param name="rawPath">List of grid positions representing the raw path found by the ant.
        /// The path may contain loops where the ant crosses its own trail.</param>
        /// <returns>List of grid positions representing the cleaned path with loops removed.</returns>
        public List<Vector3Int> RemoveLoops(List<Vector3Int> rawPath)
        {
            List<Vector3Int> cleanPath = new List<Vector3Int>();

            foreach (Vector3Int pos in rawPath)
            {
                int index = cleanPath.IndexOf(pos);
        
                if (index != -1)
                {
                    // Loop detected
                    cleanPath.RemoveRange(index + 1, cleanPath.Count - index - 1);
                }
                else
                {
                    // Add position to the cleaned path if it's not already present
                    cleanPath.Add(pos);
                }
            }

            return cleanPath;
        }


        /// <summary>
        /// Deposits pheromones on the path based on the length of the path.
        /// The amount of pheromones awarded is proportional to the length of the path.
        /// </summary>
        /// <param name="path">List of grid positions representing the path on which to deposit pheromones</param>
        /// <param name="amount">The amount of pheromones to be deposited on each cell in the path</param>
        private void DepositPheromones(List<Vector3Int> path, float amount)
        {
            foreach (var p in path)
            {
                pheromones[p.x, p.y, p.z] += amount;
            }
        }

        /// <summary>
        /// Penalizes the path by reducing the pheromone levels on each cell in the path.
        /// Used when no valid path is found during the simulation in the WFC solver
        /// to discourage ants from exploring similar paths in future iterations.
        /// </summary>
        /// <param name="path">List of grid positions representing the path to be penalized</param>
        private void PenalizePath(List<Vector3Int> path)
        {
            foreach (var p in path)
            {
                pheromones[p.x, p.y, p.z] *= 0.1f;
            }
        }

        /// <summary>
        /// Evaporates the pheromones in the map.
        /// The evaporation rate is set to 0.05, meaning that 5% of the pheromones are lost each iteration.
        /// </summary>
        private void EvaporatePheromones()
        {
            for (int x = 0; x < width; x++)
            {
                for(int y = 0; y < floors; y++)                
                {
                    for(int z = 0; z < depth; z++)
                    {
                        pheromones[x, y, z] *= (1 - Rho);
                    }
                }
            }
        }

        /// <summary>
        /// Applies constraints to the WFC solver based on the path found by the ACO algorithm.
        /// The constraints ensure that the path is valid and that the cells in the path have path sockets available.
        /// </summary>
        /// <param name="path"></param>
        private void ApplyConstraintsToWFC(List<Vector3Int> path)
        {
            for (int i = 0; i < path.Count; i++)
            {
                Cell cell = grid[path[i].x, path[i].y, path[i].z];
                
                // Skip collapsed start and finish
                if (cell.IsCollapsed)
                {
                    continue;
                }
        
                List<int> requiredDirections = new List<int>();
                if (i > 0) requiredDirections.Add(GetDirectionIndex(path[i], path[i - 1]));
                if (i < path.Count - 1) requiredDirections.Add(GetDirectionIndex(path[i], path[i + 1]));

                var pathVariants = cell.AvailableVariants.Where(v => 
                {
                    foreach (int dirIndex in requiredDirections)
                    {
                        string socket = v.Sockets[dirIndex];
                        if (socket != "path" && !socket.StartsWith("stairs_up")) return false;
                    }
                    return true;
                }).ToList();

                wfc.ForceCollapse(cell, pathVariants);
            }
        }
        
        /// <summary>
        /// Gets the index of the direction that leads from one position to another.
        /// </summary>
        private int GetDirectionIndex(Vector3Int from, Vector3Int to)
        {
            Vector3Int dir = to - from;
            if (dir == new Vector3Int(0, 0, 1)) return 0;  // North
            if (dir == new Vector3Int(1, 0, 0)) return 1;  // East
            if (dir == new Vector3Int(0, 0, -1)) return 2; // South
            if (dir == new Vector3Int(-1, 0, 0)) return 3; // West
            if (dir == new Vector3Int(0, 1, 0)) return 4;  // Up
            if (dir == new Vector3Int(0, -1, 0)) return 5; // Down
            return -1; // Invalid direction
        }

        /// <summary>
        /// Returns the current state of the pheromone matrix.
        /// </summary>
        /// <returns>3D array of pheromones</returns>
        public float[,,] GetPheromones()
        {
            return pheromones;
        }



    }
}