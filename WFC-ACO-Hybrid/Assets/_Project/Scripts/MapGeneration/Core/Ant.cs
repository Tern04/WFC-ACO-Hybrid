using System.Collections.Generic;
using UnityEngine;

namespace _Project.Scripts.MapGeneration.Core
{
    /// <summary>
    /// Class representing an autonomous agent (ant) for pathfinding.
    /// Independently searches the grid based on feromone levels and heuristics.
    /// </summary>
    public class Ant
    {
        // Extern data for the ant
        private readonly Cell[,,] grid;
        private readonly float [,,] pheromones;
        private readonly Vector3Int endPos;

        private readonly float alpha; // Feromone weight
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

    }
}