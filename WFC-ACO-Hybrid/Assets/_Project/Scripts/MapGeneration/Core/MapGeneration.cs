using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using _Project.Scripts.MapGeneration.Data;
using _Project.Scripts.Utils;
using Debug = UnityEngine.Debug;

namespace _Project.Scripts.MapGeneration.Core
{
    /// <summary>
    /// Main class responsible for generating the map using WFC and hybrid algorithms.
    /// Handles the initialization, generation, and visualization of the map.
    /// </summary>
    public class MapGenerator : MonoBehaviour
    {
        [Header("Map Settings")] 
        public int mapWidth = 10;
        public int mapFloors = 3;
        public int mapDepth = 10;
        public float tileSize = 20f; 

        [Header("WFC Data")] 
        public List<TileData> allAvailableTiles; // List of all available tiles

        [Header("Special Tiles")]
        public TileData startTileData;
        public TileData finishTileData;

        [Header("Visualization")]
        public Material mainPathMaterial;

        private Cell[,,] grid; // 2D array representing the map
        private List<TileVariant> standardVariants; // List of all possible tile variants
        private List<TileVariant> startVariants; // List of possible start tile variants
        private List<TileVariant> finishVariants; // List of possible finish tile variants

        /// <summary>
        /// Main entry point for the map generation. Initializes the grid and starts the WFC algorithm.
        /// </summary>
        void Start()
        {
            InitializeGrid();
        }
        
        /// <summary>
        /// Method for generating the map from UI.
        /// 0 = GLS
        /// 1 = ACO
        /// 2 = Pure WFC
        /// </summary>
        public void GenerateMapFromUI(int width, int floors, int depth, int algoIndex, MapGeneratorUI uiCallback)
        {
            mapWidth = width;
            mapFloors = floors;
            mapDepth = depth;

            float startTime = Time.realtimeSinceStartup;
            List<Vector3Int> builtPath = null;
            int attempts = 0;
            int maxRetries = algoIndex == 0 || algoIndex == 2
                ? CalculateMaxAttempts()
                : 3; // Allow more retries for GLS crawler and pure WFC, fewer for ACO due to better performance
            
            ACOHybridSolver aco = null; // Declare ACO solver variable for failure case visibility
            
            bool isEnvironmentFinished = false;

            // Allow more attempts
            while (attempts < maxRetries && builtPath == null)
            {
                attempts++;
                ClearScene();
                ResetGrid();
                
                WFCSolver wfc = new WFCSolver(grid, mapWidth, mapFloors, mapDepth);
                wfc.ApplyBoundaryConstraints();
                SetStartAndFinish(wfc);

                Vector3Int startPos = new Vector3Int(0, 0, 0);
                Vector3Int endPos = new Vector3Int(mapWidth - 1, mapFloors - 1, mapDepth - 1);
                
                if (algoIndex == 0) // 0 = GLS
                {
                    GLSHybridSolver gls = new GLSHybridSolver(grid, mapWidth, mapFloors, mapDepth, wfc);
                    builtPath = gls.RunGLSHybrid(startPos, endPos);
                }
                else if (algoIndex == 1) // 1 = ACO
                {
                    aco = new ACOHybridSolver(grid, mapWidth, mapFloors, mapDepth, wfc);
                    builtPath = aco.RunACOHybrid(startPos, endPos);
                }
                else if (algoIndex == 2) // 2 = Pure WFC
                {
                    bool wfcSuccess = wfc.RunWFC();
                    
                    if (wfcSuccess)
                    {
                        // Success, create an empty list for validation
                        builtPath = new List<Vector3Int>(); 
                    }
                    else
                    {
                        // Failure, set it to null to indicate failure
                        builtPath = null; 
                    }
                    
                }
                isEnvironmentFinished = wfc.IsFullyCollapsed();
            }

            float duration = (Time.realtimeSinceStartup - startTime) * 1000f;
            
            bool hasPath = builtPath != null;
            bool fullSuccess = hasPath && isEnvironmentFinished;
            int finalPathLength = (hasPath && builtPath.Count > 0) ? builtPath.Count : 0;

            if (builtPath != null)
            {
                Vector3Int startPos = new Vector3Int(0, 0, 0);
                Vector3Int endPos = new Vector3Int(mapWidth - 1, mapFloors - 1, mapDepth - 1);

                // Path visualization
                foreach (Vector3Int pos in builtPath)
                {
                    // Skip the start and end positions
                    if (pos == startPos || pos == endPos)
                    {
                        continue;
                    }
                    
                    grid[pos.x, pos.y, pos.z].isMainPath = true;
                }
                
                InstantiateTiles();
                uiCallback.UpdateResult(fullSuccess, duration, attempts, finalPathLength);
            }
            else
            {
                // If ACO failed, show pheromone levels
                if (algoIndex == 1 && aco != null)
                {
                    VisualizePheromones(aco.GetPheromones());
                }
                uiCallback.UpdateResult(false, duration, attempts, 0);
            }
        }

        /// <summary>
        /// Initializes the grid with all possible tile variants.
        /// </summary>
        void InitializeGrid()
        {
            standardVariants = new List<TileVariant>();
            startVariants = new List<TileVariant>();
            finishVariants = new List<TileVariant>();

            foreach (var tile in allAvailableTiles)
            {
                for (int r = 0; r < 4; r++)
                {
                    TileVariant variant = new TileVariant(tile, r);

                    // Check for the start tile
                    if (tile == startTileData) startVariants.Add(variant);

                    // Check for the finish tile
                    if (tile == finishTileData) finishVariants.Add(variant);

                    // Other tiles
                    if (tile != startTileData && tile != finishTileData) standardVariants.Add(variant);
                }
            }

            // Initialize the grid
            grid = new Cell[mapWidth, mapFloors, mapDepth];
            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 0; y < mapFloors; y++)
                {
                    for (int z = 0; z < mapDepth; z++)
                    {
                        grid[x, y, z] = new Cell(new Vector3Int(x, y, z), standardVariants);
                    }
                }
            }
        }

        /// <summary>
        /// Resets the grid to its initial state.
        /// </summary>
        void ResetGrid()
        {
            grid = new Cell[mapWidth, mapFloors, mapDepth];
            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 0; y < mapFloors; y++)
                {
                    for (int z = 0; z < mapDepth; z++)
                    {
                        grid[x, y, z] = new Cell(new Vector3Int(x, y, z), standardVariants);
                    }
                }
            }
        }

        /// <summary>
        /// Sets the start and finish tiles on the map.
        /// </summary>
        void SetStartAndFinish(WFCSolver solver, bool visualize = false)
        {
            // Start in the first floor, first corner
            Cell startCell = grid[0, 0, 0];
            var filteredStart = startVariants.Where(v => v.Sockets[0] == "path" ||
                                                         v.Sockets[1] == "path").ToList();

            if (filteredStart.Count > 0)
            {
                // Set the initial start with the filtered start variants to ensure it has a valid path connection
                solver.SetInitialCell(startCell, filteredStart);
                if (visualize) VisualizeCell(startCell);
            }

            // Finish in the opposite corner at the last floor 
            Cell endCell = grid[mapWidth - 1, mapFloors - 1, mapDepth - 1];
            var filteredEnd = finishVariants.Where(v => v.Sockets[2] == "path" ||
                                                        v.Sockets[3] == "path").ToList();

            if (filteredEnd.Count > 0)
            {
                // Set the initial finish with the filtered end variants to ensure it has a valid path connection
                solver.SetInitialCell(endCell, filteredEnd);
                if (visualize) VisualizeCell(endCell);
            }
        }

        /// <summary>
        /// Generates a valid map by repeatedly running the hybrid GLS algorithm until a valid map is found.
        /// Iterates up to a maximum number of attempts to prevent infinite loops.
        /// Uses GLS to guide the WFC process and visualizes the generation after the generation.
        /// Time and path length are measured for comparison.
        /// </summary>
        private void GenerateValidGLSMap()
        {
            int attempts = 0;
            bool success = false;
            int maxAttempts = CalculateMaxAttempts();

            // Start runtime measurement for generation performance evaluation.
            float startTime = Time.realtimeSinceStartup;

            while (!success && attempts < maxAttempts)
            {
                attempts++;
                ClearScene();
                ResetGrid();

                WFCSolver solver = new WFCSolver(grid, mapWidth, mapFloors, mapDepth);
                solver.ApplyBoundaryConstraints();
                SetStartAndFinish(solver);

                GLSHybridSolver glsSolver = new GLSHybridSolver(grid, mapWidth, mapFloors, mapDepth, solver);

                Vector3Int startPos = new Vector3Int(0, 0, 0);
                Vector3Int endPos = new Vector3Int(mapWidth - 1, mapFloors - 1, mapDepth - 1);

                // Retrieve the explicit GLS backbone path as an ordered sequence of grid coordinates.
                List<Vector3Int> builtPath = glsSolver.RunGLSHybrid(startPos, endPos);

                if (builtPath != null)
                {
                    // Stop measuring time after the successful computation
                    float duration = (Time.realtimeSinceStartup - startTime) * 1000f;
                    success = true;

                    // Output key benchmarking metrics for reproducible evaluation.
                    Debug.Log("Valid map generated after " + attempts + " attempts.");
                    Debug.Log("Map generation in " + duration.ToString("0.00") + " ms.");
                    Debug.Log("Path length: " + builtPath.Count);

                    // Mark the computed path for post-process visual highlighting.
                    foreach (Vector3Int pos in builtPath)
                    {
                        // Skip the start and end positions
                        if (pos == startPos || pos == endPos)
                        {
                            continue;
                        }
                        
                        grid[pos.x, pos.y, pos.z].isMainPath = true;
                    }

                    // Instantiate the final map tiles.
                    InstantiateTiles();
                }
            }

            if (!success)
            {
                Debug.LogError("Failed to generate a valid map after " + attempts + " attempts.");
            }
        }

        /// <summary>
        /// Generates a valid map by running the ACO-WFC hybrid algorithm.
        /// Uses ACO to guide the WFC process and visualizes the generation in real-time.
        /// </summary>
        private void GenerateValidACOMap()
        {
            // Clear the scene and reset the grid
            ClearScene();
            ResetGrid();

            // Time measurement for performance evaluation
            float startTime = Time.realtimeSinceStartup;

            // Initialize WFC and set start and finish positions
            WFCSolver wfcSolver = new WFCSolver(grid, mapWidth, mapFloors, mapDepth);
            wfcSolver.ApplyBoundaryConstraints();

            SetStartAndFinish(wfcSolver);

            // Initialize the ACO solver with the grid and WFC solver reference
            ACOHybridSolver acoSolver = new ACOHybridSolver(grid, mapWidth, mapFloors, mapDepth, wfcSolver);

            Vector3Int startPos = new Vector3Int(0, 0, 0);
            Vector3Int endPos = new Vector3Int(mapWidth - 1, mapFloors - 1, mapDepth - 1);

            // Start the ACO algorithm to build the path and guide the WFC process
            List<Vector3Int> builtPath = acoSolver.RunACOHybrid(startPos, endPos);

            // Evaluation and visualization
            if (builtPath != null)
            {
                float duration = (Time.realtimeSinceStartup - startTime) * 1000f;

                Debug.Log("Valid map generated on the first attempt");
                Debug.Log($"Total Generation Time: {duration:0.00} ms.");
                Debug.Log($"Optimal Path Length: {builtPath.Count} steps.");

                // Set the main path flag for visualization
                foreach (Vector3Int pos in builtPath)
                {
                    // Skip the start and end positions
                    if (pos == startPos || pos == endPos)
                    {
                        continue;
                    }
                    
                    grid[pos.x, pos.y, pos.z].isMainPath = true;
                }

                // Instantiate the final map tiles with the main path highlighted
                InstantiateTiles();
            }
            else
            {
                // Failure
                Debug.LogError("Failed to generate a map. Ants could not find any buildable path.");
            }
        }
        
        /// <summary>
        /// Visualizes the pheromone levels on the map by instantiating spheres with dynamic sizes and colors.
        /// </summary>
        /// <param name="pheromones">3D array of pheromone levels</param>
        private void VisualizePheromones(float[,,] pheromones)
        {
            float minPheromone = float.MaxValue;
            float maxPheromone = 0f;
            
            // Get the min and max pheromone values
            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 0; y < mapFloors; y++)
                {
                    for (int z = 0; z < mapDepth; z++)
                    {
                        float p = pheromones[x, y, z];
                        if (p > 0.0001f)
                        {
                            if (p < minPheromone)
                            {
                                minPheromone = p;
                            }
                            
                            if (p > maxPheromone)
                            {
                                maxPheromone = p;
                            }
                        }
                    }
                }
            }

            // Visualization of pheromone levels
            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 0; y < mapFloors; y++)
                {
                    for (int z = 0; z < mapDepth; z++)
                    {
                        float p = pheromones[x, y, z];
                        
                        if (p > 0.0001f) 
                        {
                            GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            node.transform.position = new Vector3(x * tileSize, y * tileSize, z * tileSize); 
                            Destroy(node.GetComponent<Collider>());

                            float intensity = 0f;
                            if (maxPheromone > minPheromone)
                            {
                                // Normalize intensity based on the range of pheromones
                                intensity = (p - minPheromone) / (maxPheromone - minPheromone);
                            }

                            // Dynamic size based on intensity
                            float dynamicSize = tileSize * Mathf.Lerp(0.05f, 0.15f, intensity);
                            node.transform.localScale = Vector3.one * dynamicSize; 

                            // Red - high value, Blue - low value
                            Color heatmapColor = Color.Lerp(Color.blue, Color.red, intensity);

                            Renderer rend = node.GetComponent<Renderer>();
                            rend.material.color = heatmapColor;

                            node.transform.parent = this.transform;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the maximum number of generation attempts based on the map size.
        /// </summary>
        /// <returns>Maximum number of attempts</returns>
        private int CalculateMaxAttempts()
        {
            int totalCells = mapWidth * mapFloors * mapDepth;

            int maxAttempts = 20 + (totalCells / 2);

            return Math.Clamp(maxAttempts, 20, 500); // Clamp between 20 and 500 attempts
        }

        /// <summary>
        /// Clears all objects in the scene.
        /// Helper function for generating the valid map.
        /// </summary>
        void ClearScene()
        {
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
        }

        /// <summary>
        /// Instantiates the tiles in the Unity scene based on the collapsed grid.
        /// Each tile is placed according to its grid position and rotation.
        /// </summary>
        void InstantiateTiles()
        {
            foreach (var cell in grid)
            {
                if (cell.CollapsedVariant != null)
                {
                    InstantiateCell(cell);
                }
            }
        }

        /// <summary>
        /// Instantiates a single collapsed cell and applies optional path visualization.
        /// </summary>
        void InstantiateCell(Cell cell)
        {
            Vector3 pos = new Vector3(cell.GridPosition.x * tileSize,
                cell.GridPosition.y * tileSize,
                cell.GridPosition.z * tileSize);

            Quaternion rot = Quaternion.Euler(0, cell.CollapsedVariant.Rotation * 90, 0);
            GameObject tileObj = Instantiate(cell.CollapsedVariant.Data.prefab, pos, rot, transform);

            if (cell.isMainPath)
            {
                ApplyMainPathMaterial(tileObj);
            }
        }

        /// <summary>
        /// Applies the main path material to the floor and step objects of a tile.
        /// </summary>
        /// <param name="tileObj">The tile GameObject to which the material should be applied</param>
        void ApplyMainPathMaterial(GameObject tileObj)
        {
            if (mainPathMaterial == null)
            {
                return;
            }

            MeshRenderer[] renderers = tileObj.GetComponentsInChildren<MeshRenderer>();

            foreach (MeshRenderer rnd in renderers)
            {
                string objName = rnd.gameObject.name.ToLower();

                if (objName.Contains("floor") || objName.Contains("step"))
                {
                    rnd.material = mainPathMaterial;
                }
            }
        }

        /// <summary>
        /// Visualizes the collapsed cell in the scene.
        /// </summary>
        /// <param name="cell">Cell to be collapsed</param>
        void VisualizeCell(Cell cell)
        {
            if (cell.CollapsedVariant != null)
            {
                InstantiateCell(cell);
            }
        }

        /// <summary>
        /// Configures and triggers the automated performance evaluation for the baseline WFC algorithm.
        /// </summary>
        private void RunWFCBenchmark()
        {
            // Define the grid sizes to test
            Vector3Int[] testSizes = new Vector3Int[]
            {
                new Vector3Int(10, 3, 10),
                new Vector3Int(15, 3, 15),
                new Vector3Int(20, 4, 20),
                new Vector3Int(25, 4, 25),
                new Vector3Int(30, 5, 30)
            };

            // Delegate the execution to the BenchmarkUtils class
            EntropyBenchmark.RunBenchmark("WFC_Heap_Entropy.csv", 100, testSizes, RunSingleBenchmarkCycle);
        }

        /// <summary>
        /// Callback method invoked by BenchmarkUtils for each test iteration.
        /// Executes the generation algorithm and measures hardware-level execution time.
        /// </summary>
        /// <param name="size">Target grid dimensions for the current iteration</param>
        /// <returns>Execution time in milliseconds.</returns>
        private (double totalTime, double entropyTime) RunSingleBenchmarkCycle(Vector3Int size)
        {
            // Grid configuration
            mapWidth = size.x;
            mapFloors = size.y;
            mapDepth = size.z;

            // Environment reset and constraints initialization
            ClearScene();
            ResetGrid();

            WFCSolver solver = new WFCSolver(grid, mapWidth, mapFloors, mapDepth);
            solver.ApplyBoundaryConstraints();
            SetStartAndFinish(solver);

            // Performance measurement
            Stopwatch sw = Stopwatch.StartNew();

            // Target algorithm execution
            solver.RunWFC();

            sw.Stop();

            double totalTime = sw.Elapsed.TotalMilliseconds;
            double entropyTime = solver.LastEntropySearchTimeMs;

            return (totalTime, entropyTime);
        }

        
        /// <summary>
        /// Benchmark the performance of the ACO-WFC hybrid algorithm.
        /// Tests Beta sweep, swarm size sweep, and scalability sweep.
        /// </summary>
        public void RunACOBenchmark()
        {
            string filePath = "ACO_Benchmark_SqrtDyn.csv";
            StringBuilder csv = new StringBuilder();
            int runsPerConfig = 100;

            // CSV header
            csv.AppendLine(
                "Test_Phase,Map_Size,Colony_Size,Max_Iter,Beta,Rho,Avg_Time_ms,Avg_Path_Length,Success_Rate_%");

            // Fixed parameters for the algorithm
            Vector3Int bigMap = new Vector3Int(30, 5, 30);
            int hardCells = bigMap.x * bigMap.y * bigMap.z;
            int dynColony = Mathf.Clamp(hardCells / 200, 10, 30); // Dynamic colony size
            int dynIter = Mathf.Clamp(hardCells / 100, 15, 40); // Dynamic iteration count

            // First test phase: Beta sweep
            // For finding best Beta value for this project

            Debug.Log("Beta Sweep");

            float[] betaValues = { 7.8f, 7.9f, 8.0f, 8.1f, 8.2f };

            // Perform beta sweep test for each beta value
            foreach (float b in betaValues)
            {
                string phase = $"Beta_Sweep";
                //RunTestBatch(csv, phase, bigMap, dynColony, dynIter, b, runsPerConfig);
            }

            // // Second test phase: Swarm Size Sweep
            // For finding best swarm size for stability and performance

            Debug.Log("Swarm Size Sweep");
            float optimalBeta = 7.9f; // Fixed Beta value 
            (int col, int iter)[] swarmConfigs =
            {
                // Fast
                (5, 5), (5, 10), (10, 5),

                // Mid
                (10, 15), (10, 20), (15, 10), (20, 10),

                // High ant, low iterations
                (25, 10), (30, 10), (40, 10), (30, 15),

                // Low ants, high iterations
                (10, 30), (10, 40), (12, 40), (5, 40),

                // Balanced mid-high
                (20, 40), (30, 30), (40, 20)
            };

            // Perform swarm size sweep test for each swarm size
            foreach (var swarm in swarmConfigs)
            {
                string phase = $"Swarm_Size";
                //RunTestBatch(csv, phase, bigMap, swarm.col, swarm.iter, optimalBeta, runsPerConfig);
            }


            // Scalability Sweep
            // For evaluating how the algorithm scales with increasing map sizes

            Debug.Log("Scalability Sweep");
            Vector3Int[] scalingMaps =
            {
                new Vector3Int(10, 3, 10),
                new Vector3Int(15, 4, 15),
                new Vector3Int(20, 4, 20),
                new Vector3Int(25, 5, 25),
                new Vector3Int(30, 5, 30)
            };

            // Perform scalability sweep test for each scaling map size
            foreach (var map in scalingMaps)
            {
                int cells = map.x * map.y * map.z;

                float sqrt = Mathf.Sqrt(cells);
                int colony = Mathf.Clamp(Mathf.RoundToInt(sqrt * 0.5f), 10, 40);
                int iterations = Mathf.Clamp(Mathf.RoundToInt(sqrt * 0.2f), 8, 20);
                
                string phase = $"Scalability";
                RunTestBatch(csv, phase, map, colony, iterations, optimalBeta, runsPerConfig);
                
            }

            // Write the collected benchmark data to a CSV file
            File.WriteAllText(filePath, csv.ToString());
            Debug.Log($"Benchmark saved to: {filePath}");
        }

        /// <summary>
        /// Helper method to run a batch of tests for a given phase and map size.
        /// </summary>
        private void RunTestBatch(StringBuilder csv, string phase, Vector3Int size, int colony, int iter, float beta,
            int runs)
        {
            double sumTime = 0;
            double sumPathLength = 0;
            int successCount = 0;

            mapWidth = size.x;
            mapFloors = size.y;
            mapDepth = size.z;

            for (int run = 0; run < runs; run++)
            {
                // Clear the scene and reset the grid
                ClearScene();
                ResetGrid();

                // Initialize WFC and set start and finish positions
                WFCSolver wfc = new WFCSolver(grid, size.x, size.y, size.z);
                wfc.ApplyBoundaryConstraints();
                SetStartAndFinish(wfc);

                // Initialize the ACO solver
                ACOHybridSolver aco = new ACOHybridSolver(grid, size.x, size.y, size.z, wfc, beta, colony, iter);

                Vector3Int startPos = new Vector3Int(0, 0, 0);
                Vector3Int endPos = new Vector3Int(size.x - 1, size.y - 1, size.z - 1);

                // Start the ACO algorithm to build the path with timer
                float startTime = Time.realtimeSinceStartup;
                List<Vector3Int> path = aco.RunACOHybrid(startPos, endPos);
                float duration = (Time.realtimeSinceStartup - startTime) * 1000f;

                sumTime += duration;

                if (path != null)
                {
                    successCount++;
                    sumPathLength += path.Count;
                }
            }

            double avgTime = sumTime / runs;
            double avgPath = successCount > 0 ? (sumPathLength / successCount) : -1;
            float successRate = (successCount / (float)runs) * 100f;

            // CSV writing
            string mapStr = $"{size.x}x{size.y}x{size.z}";
            csv.AppendLine(
                $"{phase},{mapStr},{colony},{iter},{beta:F1},{0.15},{avgTime:F0},{avgPath:F1},{successRate:F0}");

            Debug.Log(
                $"[{phase}] Map: {mapStr} | Col: {colony} | Iter: {iter} | Beta: {beta:F1} --> Time: {avgTime:F0}ms | Path: {avgPath:F1} | Success: {successRate}%");
        }
    }
}
