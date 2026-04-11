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
        private readonly int mapWidth;
        private readonly int mapFloors;
        private readonly int mapDepth;
        
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
            this.mapWidth = mapWidth;
            this.mapFloors = mapFloors;
            this.mapDepth = mapDepth;
            this.wfc = wfc;

            InitializePheromones();
        }

        /// <summary>
        /// Initializes the pheromone matrix with the initial value.
        /// </summary>
        private void InitializePheromones()
        {
            pheromones = new float[mapWidth, mapFloors, mapDepth];
            
            for(int x = 0; x < mapWidth; x++)
            {
                for(int y = 0; y < mapFloors; y++)
                {
                    for(int z = 0; z < mapDepth; z++)
                    {
                        pheromones[x, y, z] = InitialPheromone;
                    }
                }
            }
        }
        


    }
}