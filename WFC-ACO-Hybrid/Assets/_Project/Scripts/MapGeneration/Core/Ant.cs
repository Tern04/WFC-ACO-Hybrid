using System;
using System.Collections.Generic;
using System.Linq;
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
            int mapWidth, int mapFloors, int mapDepth, float alpha, float beta)
        {
            this.grid = grid;
            this.pheromones = pheromones;
            this.endPos = endPos;
            this.alpha = alpha;
            this.beta = beta;
            
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
            ReachedTarget = false;

            int steps = 0;

            // Main loop of the ant
            while (currentPos != endPos && steps < maxSteps)
            {
                steps++;
                
                // Get valid neighbors for pathing
                List<Vector3Int> validNeighbors = GetValidNeighbors(currentPos);

                // Dead end
                if (validNeighbors.Count == 0)
                {
                    return; 
                }

                // Select the next step based on pheromone influence and heuristic
                Vector3Int nextStep = SelectNextStep(validNeighbors);
                
                // Move to the next step
                Path.Add(nextStep);
                TabuList.Add(nextStep);
                currentPos = nextStep;
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
        private Vector3Int SelectNextStep(List<Vector3Int> neighbors)
        {
            // Initialization
            List<double> probabilities = new List<double>();
            double totalProbability = 0;

            // Calculate the probability for each neighbor based on pheromone levels and heuristic information
            foreach (var n in neighbors)
            {
                float tau = pheromones[n.x, n.y, n.z]; 
                
                // Calculation of the heuristic (Eta) based on the distance to the target.
                // Adding 0.1f to avoid division by zero
                float distance = Vector3Int.Distance(n, endPos);
                float eta = 1.0f / (distance + 0.1f);  

                // Calculation of the weight for the neighbor: (Tau^Alpha) * (Eta^Beta)
                double weight = Math.Pow(tau, alpha) * Math.Pow(eta, beta);
                
                probabilities.Add(weight);
                totalProbability += weight;
            }

            // Select the next step based on the probability with randomness
            double randomValue = UnityEngine.Random.value * totalProbability;
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
        /// Checks if the neighbor is within the map bounds, not in the Tabu list, and has a path socket.
        /// </summary>
        private List<Vector3Int> GetValidNeighbors(Vector3Int pos)
        {
            // List of valid neighbors
            List<Vector3Int> valid = new List<Vector3Int>();
            
            // Directions of the neighbors
            Vector3Int[] dirs = { 
                new Vector3Int(0, 0, 1),   // North
                new Vector3Int(1, 0, 0),   // East
                new Vector3Int(0, 0, -1),  // South
                new Vector3Int(-1, 0, 0),  // West
                new Vector3Int(0, 1, 0),   // Up
                new Vector3Int(0, -1, 0)   // Down
            };

            foreach (var d in dirs)
            {
                Vector3Int n = pos + d;
                
                // Boundary and Tabu check
                if (IsInBounds(n) && !TabuList.Contains(n))
                {
                    Cell neighborCell = grid[n.x, n.y, n.z];

                    // Check for path sockets in the neighbor cell's available variants
                    bool canBePath = neighborCell.AvailableVariants.Any(variant => 
                        variant.Sockets.Any(socket => socket == "path" || socket.StartsWith("stairs_up"))
                    );

                    if (canBePath)
                    {
                        valid.Add(n);
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