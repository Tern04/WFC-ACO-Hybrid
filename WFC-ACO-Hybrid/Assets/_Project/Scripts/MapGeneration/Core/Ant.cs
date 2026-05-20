using System;
using System.Collections.Generic;
using System.Linq;
using _Project.Scripts.MapGeneration.Data;
using UnityEngine;

namespace _Project.Scripts.MapGeneration.Core
{
    /// <summary>
    /// Class representing an autonomous agent (ant) for pathfinding.
    /// Independently searches the grid based on pheromone levels and heuristics.
    /// </summary>
    public class Ant
    {
        // Extern data for the ant
        private readonly Cell[,,] grid;
        private readonly float [,,] pheromones;
        private readonly Vector3Int endPos;

        private readonly float alpha; // Pheromone weight
        private readonly float beta; // Heuristic weight
        private readonly System.Random rng;
        
        // Map borders
        private readonly int mapWidth; 
        private readonly int mapFloors; 
        private readonly int mapDepth;
        
        public List<Vector3Int> Path { get; private set; } // Path found by the ant
        public HashSet<Vector3Int> TabuList { get; private set; } // List of cells visited by the ant
        public bool ReachedTarget { get; private set; } // Flag indicating if the ant has reached the target

        /// <summary>
        /// Constructor for the Ant class.
        /// </summary>
        /// <param name="grid">The 3D grid of cells representing the map</param>
        /// <param name="pheromones">The 3D array of pheromone levels at the grid</param>
        /// <param name="endPos">The target position the ant is trying to reach</param>
        /// <param name="mapWidth">Width of the map</param>
        /// <param name="mapFloors">Number of floors in the map</param>
        /// <param name="mapDepth">Depth of the map</param>
        /// <param name="alpha">Weight of the pheromone influence on the ant's decision-making</param>
        /// <param name="beta">Weight of the heuristic influence on the ant's decision-making</param>
        public Ant(Cell[,,] grid, float[,,] pheromones, Vector3Int endPos,
            int mapWidth, int mapFloors, int mapDepth, float alpha, float beta, System.Random rng)
        {
            this.grid = grid;
            this.pheromones = pheromones;
            this.endPos = endPos;
            this.alpha = alpha;
            this.beta = beta;
            this.rng = rng;

            this.mapWidth = mapWidth;
            this.mapFloors = mapFloors;
            this.mapDepth = mapDepth;
        }
        
        /// <summary>
        /// Lets the ant explore the grid, following the pheromone influence and heuristic.
        /// The ant explores the grid until it reaches the target or traps itself in a dead end.
        /// </summary>
        /// <param name="startPos">Starting position of the ant</param>
        /// <param name="maxSteps">Maximum number of steps the ant can take before giving up</param>
        public void Explore(Vector3Int startPos, int maxSteps)
        {
            // Initialization
            Path = new List<Vector3Int> { startPos };
            TabuList = new HashSet<Vector3Int> { startPos };
            Vector3Int currentPos = startPos;
            List<TileVariant> currentVariants = grid[startPos.x, startPos.y, startPos.z].AvailableVariants;
            ReachedTarget = false;

            int steps = 0;

            // Main loop of the ant
            while (currentPos != endPos && steps < maxSteps)
            {
                steps++;
                
                // Get valid neighbors for pathing
                var validNeighbors = GetValidNeighbors(currentPos, currentVariants);

                // Dead end
                if (validNeighbors.Count == 0)
                {
                    return; 
                }

                // Select the next step based on pheromone influence and heuristic
                var nextStep = SelectNextStep(validNeighbors);
                
                // Move to the next step
                Path.Add(nextStep.pos);
                TabuList.Add(nextStep.pos);
                
                currentPos = nextStep.pos;
                currentVariants = nextStep.variants;
            }

            // Check if the ant reached the target
            if (currentPos == endPos)
            {
                ReachedTarget = true;
            }
        }
        
        /// <summary>
        /// Selects the next step for the ant based on a combination of pheromone levels and heuristic information.
        /// Uses Tau (pheromone level) and Eta (heuristic) to calculate the probability of each neighbor.
        /// </summary>
        private (Vector3Int pos, List<TileVariant> variants) SelectNextStep(List<(Vector3Int pos, List<TileVariant> variants)> neighbors)
        {
            // Initialization
            List<double> probabilities = new List<double>();
            double totalProbability = 0;

            // Calculate the probability for each neighbor based on pheromone levels and heuristic information
            foreach (var n in neighbors)
            {
                float tau = pheromones[n.pos.x, n.pos.y, n.pos.z]; 
                
                // Calculation of the heuristic (Eta) based on the distance to the target.
                // Adding 0.1f to avoid division by zero
                float distance = Mathf.Abs(n.pos.x - endPos.x) + Mathf.Abs(n.pos.y - endPos.y) + Mathf.Abs(n.pos.z - endPos.z);
                float eta = 1.0f / (distance + 0.1f);  

                // Calculation of the weight for the neighbor: (Tau^Alpha) * (Eta^Beta)
                double weight = Math.Pow(tau, alpha) * Math.Pow(eta, beta);
                
                probabilities.Add(weight);
                totalProbability += weight;
            }

            // Select the next step based on the probability with randomness
            double randomValue = rng.NextDouble() * totalProbability;
            double cumulative = 0;

            for (int i = 0; i < neighbors.Count; i++)
            {
                cumulative += probabilities[i];
                if (cumulative >= randomValue)
                {
                    return neighbors[i];
                }
            }

            // Fallback 
            return neighbors.Last(); 
        }
        
        /// <summary>
        /// Finds valid neighbors for pathing based on the current position and the map boundaries.
        /// Checks if the neighbor is within the map bounds, not in the Tabu list, and has a valid path socket.
        /// </summary>
        private List<(Vector3Int pos, List<TileVariant> variants)> GetValidNeighbors(Vector3Int pos, List<TileVariant> currentVariants)
        {
            var valid = new List<(Vector3Int pos, List<TileVariant> variants)>();
            
            Vector3Int[] dirs = { 
                new Vector3Int(0, 0, 1),   // 0: North
                new Vector3Int(1, 0, 0),   // 1: East
                new Vector3Int(0, 0, -1),  // 2: South
                new Vector3Int(-1, 0, 0),  // 3: West
                new Vector3Int(0, 1, 0),   // 4: Up
                new Vector3Int(0, -1, 0)   // 5: Down
            };
            

            for (int i = 0; i < 6; i++)
            {
                Vector3Int d = dirs[i];
                Vector3Int n = pos + d;
                
                if (IsInBounds(n) && !TabuList.Contains(n))
                {
                    Cell neighborCell = grid[n.x, n.y, n.z];
                    int oppositeDir;
                    
                    if (i < 4)
                    {
                        oppositeDir = (i + 2) % 4;
                    }
                    else if (i == 4)
                    {
                        oppositeDir = 5;
                    }
                    else
                    {
                        oppositeDir = 4;
                    }

                    // Get all exit sockets for the current variants in the direction i
                    HashSet<string> possibleExitSockets = new HashSet<string>();
                    foreach (var v in currentVariants)
                    {
                        string s = v.Sockets[i];
                        if (s == "path" || s.StartsWith("stairs_up")) 
                        {
                            possibleExitSockets.Add(s);
                        }
                    }

                    // If there are no exit sockets, skip this neighbor
                    if (possibleExitSockets.Count == 0)
                    {
                        continue;
                    }

                    // Check if the neighbor has any variant that can connect to the current cell's exit socket
                    List<TileVariant> nextVariants = new List<TileVariant>();
                    foreach (var nv in neighborCell.AvailableVariants)
                    {
                        if (possibleExitSockets.Contains(nv.Sockets[oppositeDir]))
                        {
                            nextVariants.Add(nv);
                        }
                    }

                    // If there are valid variants, add the neighbor to the list of valid neighbors
                    if (nextVariants.Count > 0)
                    {
                        valid.Add((n, nextVariants));
                    }
                }
            }
            
            return valid;
        }

        /// <summary>
        /// Helper method to check if a position is within the map bounds.
        /// </summary>
        private bool IsInBounds(Vector3Int p)
        {
            return p.x >= 0 && p.x < mapWidth && 
                   p.y >= 0 && p.y < mapFloors && 
                   p.z >= 0 && p.z < mapDepth;
        }

    }
}