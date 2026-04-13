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
        private const float Beta = 7.0f; // Heuristic weight
        private const float InitialPheromone = 0.1f; // Initial pheromone value
        private const float Rho = 0.05f; // Pheromone evaporation rate
        private const float Q = 100.0f; // Pheromone award constant
        private const int ColonySize = 20; // Number of agents in the colony
        private const int MaxIterations = 50; // Maximum number of colony iterations
        
        private float[,,] pheromones;

        public ACOHybridSolver(Cell[,,] grid, int mapWidth, int mapFloors, int mapDepth, WFCSolver wfc)
        {
            this.grid = grid;
            this.width = mapWidth;
            this.floors = mapFloors;
            this.depth = mapDepth;
            this.wfc = wfc;

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
            for (int iter = 0; iter < MaxIterations; iter++)
            {
                List<Ant> successfulAnts = new List<Ant>();

                // Run ColonySize ants to explore the map and find paths to the target position
                for (int i = 0; i < ColonySize; i++)
                {
                    Ant ant = new Ant(grid, pheromones, endPos, width, floors, depth, Alpha, Beta);
                    
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
                    Ant bestIterAnt = successfulAnts.OrderBy(a => a.Path.Count).First();

                    wfc.SaveSnapshot(); // Saves the current state of the WFC solver before simulating the path
                    
                    // Dry run the path in WFC to check if it's valid'
                    bool isBuildable = SimulatePathInWFC(bestIterAnt.Path);
                    
                    wfc.RestoreSnapshot(); // Restores the WFC state after simulation

                    // If the path is valid, rewarded with pheromones
                    if (isBuildable)
                    {
                        // Update path if the new path is shorter than the best found so far
                        if (bestIterAnt.Path.Count < bestLength)
                        {
                            bestLength = bestIterAnt.Path.Count;
                            bestValidPath = new List<Vector3Int>(bestIterAnt.Path);
                        }

                        // Deposit pheromones on the path based on the length of the path
                        DepositPheromones(bestIterAnt.Path, Q / bestIterAnt.Path.Count);
                    }
                    
                    else
                    {
                        // Penalize the path if it's not valid 
                        PenalizePath(bestIterAnt.Path);
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
            foreach (var p in path)
            {
                Cell cell = grid[p.x, p.y, p.z];
                
                // Get all variants that have a path socket available for this cell
                var pathVariants = cell.AvailableVariants
                    .Where(v => v.Sockets.Any(s => s == "path" || s.StartsWith("stairs_up")))
                    .ToList();

                if (pathVariants.Count == 0)
                {
                    return false;
                }
                
                // Force collapse the cell with the path variant
                wfc.ForceCollapse(cell, pathVariants);
            }

            // If contradiction is found, the path is not valid
            return !wfc.HasContradiction;
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
            foreach (var p in path)
            {
                Cell cell = grid[p.x, p.y, p.z];
                var pathVariants = cell.AvailableVariants
                    .Where(v => v.Sockets.Any(s => s == "path" || s.StartsWith("stairs_up")))
                    .ToList();
                wfc.ForceCollapse(cell, pathVariants);
            }
        }



    }
}