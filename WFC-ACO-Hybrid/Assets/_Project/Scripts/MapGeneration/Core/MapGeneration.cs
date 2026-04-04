using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq; 
using UnityEngine;
using _Project.Scripts.MapGeneration.Data;
using _Project.Scripts.Pathfinding;
using _Project.Scripts.Utils;
using Debug = UnityEngine.Debug;

namespace _Project.Scripts.MapGeneration.Core
{
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
            //RunWFC();
            //StartCoroutine(GenerateDFSHybridAnimated());
            //GenerateValidDFSMap();
            RunWFCBenchmark();
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
        void SetStartAndFinish(WFCSolver solver,bool visualize = false)
        {
            // Start in the first floor, first corner
            Cell startCell = grid[0, 0, 0];
            var filteredStart = startVariants.Where(v => v.Sockets[0] == "path" ||
                                                         v.Sockets[1] == "path").ToList();
            solver.ForceCollapse(startCell, filteredStart);
            
            if (visualize)
            {
                VisualizeCell(startCell);
            }

            // Finish in the opposite corner at the last floor 
            Cell endCell = grid[mapWidth - 1, mapFloors - 1, mapDepth - 1];
            var filteredEnd = finishVariants.Where(v => v.Sockets[2] == "path" ||
                                                        v.Sockets[3] == "path").ToList();
            solver.ForceCollapse(endCell, filteredEnd);
            
            if (visualize)
            {
                VisualizeCell(endCell);
            }
            
        }

        /// <summary>
        /// Generates a valid map by repeatedly running the hybrid DFS algorithm until a valid map is found.
        /// Iterates up to a maximum number of attempts to prevent infinite loops.
        /// Uses DFS to guide the WFC process and visualizes the generation after the generation.
        /// Time and path length are measured for comparison.
        /// </summary>
        private void GenerateValidDFSMap()
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
                
                DFSHybridSolver dfsSolver = new DFSHybridSolver(grid, mapWidth, mapFloors, mapDepth, solver);

                Vector3Int startPos = new Vector3Int(0, 0, 0);
                Vector3Int endPos = new Vector3Int(mapWidth - 1, mapFloors - 1, mapDepth - 1);

                // Retrieve the explicit DFS backbone path as an ordered sequence of grid coordinates.
                List<Vector3Int> builtPath = dfsSolver.RunDFSHybrid(startPos, endPos);

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
        /// Generates a valid map by repeatedly running the hybrid DFS algorithm until a valid map is found.
        /// Iterates up to a maximum number of attempts to prevent infinite loops.
        /// Uses DFS to guide the WFC process and visualizes the generation in real-time.
        /// </summary>
        /// <returns></returns>
        public IEnumerator GenerateDFSHybridAnimated()
        {
            int attempt = 0;
            bool success = false;
            int maxAttempts = CalculateMaxAttempts();
            
            while (!success && attempt < maxAttempts)
            {
                attempt++;
                ClearScene();
                ResetGrid();
                
                // Initialize the WFC solver and set the start with finish and boundary constraints
                WFCSolver solver = new WFCSolver(grid, mapWidth, mapFloors, mapDepth);
                solver.ApplyBoundaryConstraints();
                SetStartAndFinish(solver, true); 
                DFSHybridSolver dfsSolver = new DFSHybridSolver(grid, mapWidth, mapFloors, mapDepth, solver);

                // Set start and finish positions in the corners of the map
                Vector3Int startPos = new Vector3Int(0, 0, 0);
                Vector3Int endPos = new Vector3Int(mapWidth - 1, mapFloors - 1, mapDepth - 1);

                // Run the hybrid DFS algorithm with visualization
                yield return StartCoroutine(dfsSolver.RunDFSHybridAnimated(startPos, endPos, VisualizeCell));
                
                // Check if the WFC process completed successfully and if a valid path exists
                PathValidator validator = new PathValidator(grid, mapWidth, mapFloors, mapDepth);
                
                if (solver.IsFullyCollapsed() && validator.IsPathPossibleDFS(startPos, endPos))
                {
                    Debug.Log("Valid map generated after" + attempt + " attempts.");
                    success = true;
                }
                else
                {
                    Debug.LogWarning("Attempt number" + attempt +" failed.)");
                    yield return new WaitForSeconds(0.5f); // Wait before retrying
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
            EntropyBenchmark.RunBenchmark("WFC_Naive_Entropy.csv", 100, testSizes, RunSingleBenchmarkCycle);
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
        
    }
}