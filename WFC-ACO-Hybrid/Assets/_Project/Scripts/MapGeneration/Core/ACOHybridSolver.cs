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

        public List<Vector3Int> RunACOHybrid(Vector3Int startPos, Vector3Int endPos)
        {
            List<Vector3Int> bestValidPath = null;
            int bestLength = int.MaxValue;
            int maxAntSteps = width * floors * depth;

            for (int iter = 0; iter < MaxIterations; iter++)
            {
                List<Ant> succesfulAnts = new List<Ant>();

                for (int i = 0; i < ColonySize; i++)
                {
                    Ant ant = new Ant(grid, pheromones, endPos, width, floors, depth, Alpha, Beta);
                    
                    ant.Explore(startPos, maxAntSteps);
                    
                    if(ant.ReachedTarget)
                    {
                        succesfulAnts.Add(ant);
                    }
                }

                if (succesfulAnts.Count > 0)
                {
                    // Find the best path from the successful ants
                    Ant bestIterAnt = succesfulAnts.OrderBy(a => a.Path.Count).First();

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
                    //TODO: Penalize the path, evaporate pheromones and check for valid path
                    else
                    {
                        
                    }
                    
                }
            }
            
            return bestValidPath;
        }

        private bool SimulatePathInWFC(List<Vector3Int> path)
        {

            foreach (var p in path)
            {
                Cell cell = grid[p.x, p.y, p.z];
                
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

        
        private void DepositPheromones(List<Vector3Int> path, float amount)
        {
            foreach (var p in path)
            {
                pheromones[p.x, p.y, p.z] += amount;
            }
        }

        private void PenalizePath(List<Vector3Int> path)
        {
            foreach (var p in path)
            {
                pheromones[p.x, p.y, p.z] *= 0.1f;
            }
        }

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



    }
}