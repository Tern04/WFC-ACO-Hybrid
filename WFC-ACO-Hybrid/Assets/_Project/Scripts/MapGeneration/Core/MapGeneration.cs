using System.Collections;
using System.Collections.Generic;
using System.Linq; 
using UnityEngine;
using _Project.Scripts.MapGeneration.Data;
using _Project.Scripts.Pathfinding;

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
            StartCoroutine(GenerateAnimatedMap());
            //GenerateValidMap();
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
        /// Generates a valid map by repeatedly running the WFC algorithm until a valid map is found.
        /// Iterates up to a maximum number of attempts to prevent infinite loops.
        /// After a valid map is generated, the tiles are instantiated in the scene.
        /// </summary>
        private void GenerateValidMap()
        {
            int attempts = 0;
            bool success = false;

            while (!success && attempts < 200)
            {
                attempts++;
                ClearScene();
                ResetGrid();
                
                WFCSolver solver = new WFCSolver(grid, mapWidth, mapFloors, mapDepth);
                solver.ApplyBoundaryConstraints();
                SetStartAndFinish(solver);


                bool wfcSuccess = solver.RunWFC();

                if (wfcSuccess)
                {
                    PathValidator validator = new PathValidator(grid, mapWidth, mapFloors, mapDepth);

                    if (validator.IsPathPossibleDFS(new Vector3Int(0, 0, 0),
                            new Vector3Int(mapWidth - 1, mapFloors - 1, mapDepth - 1)))
                    {
                        success = true;
                        Debug.Log($"Valid map generated after {attempts} attempts.");
                        InstantiateTiles();
                    }
                }
                
            }
            
            if (!success)
            {
                Debug.LogError("Failed to generate a valid map after 200 attempts.");
            }
            
        }
        
        /// <summary>
        /// Generates an animated map by running the WFC algorithm and visualizing each cell as it collapses.
        /// </summary>
        public IEnumerator GenerateAnimatedMap()
        {
            ClearScene();
            ResetGrid();
            
            WFCSolver solver = new WFCSolver(grid, mapWidth, mapFloors, mapDepth);
            solver.ApplyBoundaryConstraints();
            SetStartAndFinish(solver, true); // Visualize the start and finish tiles
            
            // Run the WFC algorithm and visualize each cell as it collapses
            yield return StartCoroutine(solver.RunWFCAnimated(VisualizeCell));
            
            PathValidator validator = new PathValidator(grid, mapWidth, mapFloors, mapDepth);
            if (validator.IsPathPossibleDFS(new Vector3Int(0, 0, 0), new Vector3Int(mapWidth - 1, mapFloors - 1, mapDepth - 1)))
            {
                Debug.Log("Animated map generated successfully with a valid path from start to finish.");
            }
            else
            {
                Debug.LogWarning("Did not find a valid path from start to finish.");
            }
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
                    Vector3 pos = new Vector3(cell.GridPosition.x * tileSize,
                        cell.GridPosition.y * tileSize,
                        cell.GridPosition.z * tileSize);
                    
                    Quaternion rot = Quaternion.Euler(0, cell.CollapsedVariant.Rotation * 90, 0);
                    Instantiate(cell.CollapsedVariant.Data.prefab, pos, rot, transform);
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
                Vector3 pos = new Vector3(cell.GridPosition.x * tileSize, cell.GridPosition.y * tileSize, cell.GridPosition.z * tileSize);
                Quaternion rot = Quaternion.Euler(0, cell.CollapsedVariant.Rotation * 90, 0);
                Instantiate(cell.CollapsedVariant.Data.prefab, pos, rot, transform);
            }
        }
        
    }
}