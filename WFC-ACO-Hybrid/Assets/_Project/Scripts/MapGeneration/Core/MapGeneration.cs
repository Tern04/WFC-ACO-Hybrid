using System.Collections;
using System.Collections.Generic;
using System.Linq; 
using UnityEngine;
using _Project.Scripts.MapGeneration.Data;

namespace _Project.Scripts.MapGeneration.Core
{
    public class MapGenerator : MonoBehaviour
    {
        [Header("Map Settings")] 
        public int mapWidth = 10;
        public int mapHeight = 10;
        public float tileSize = 20f; 

        [Header("WFC Data")] 
        public List<TileData> allAvailableTiles; // List of all available tiles
        
        [Header("Special Tiles")]
        public TileData startTileData;
        public TileData finishTileData;

        private Cell[,] grid; // 2D array representing the map
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
            StartCoroutine(RunWFCAnimated());
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
            grid = new Cell[mapWidth, mapHeight];
            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 0; y < mapHeight; y++)
                {
                    grid[x, y] = new Cell(new Vector2Int(x, y), standardVariants);
                }
            }
        }

        /// <summary>
        /// Sets the start and finish tiles on the map.
        /// </summary>
        void SetStartAndFinish(bool visualize = false)
        {
            // Start tile on (0,0)
            Cell startCell = grid[0, 0];
            
            // Filter based on the path sockets
            startCell.AvailableVariants = startVariants
                .Where(v => v.Sockets[0] == "path" || v.Sockets[1] == "path")
                .ToList();

            CollapseCell(startCell);
            Propagate(startCell);
            if (visualize) VisualizeCell(startCell);

            // Finish tile on (mapWidth-1, mapHeight-1)
            Cell endCell = grid[mapWidth - 1, mapHeight - 1];
            
            // Filter based on the path sockets
            endCell.AvailableVariants = finishVariants
                .Where(v => v.Sockets[2] == "path" || v.Sockets[3] == "path")
                .ToList();

            CollapseCell(endCell);
            Propagate(endCell);
            if (visualize) VisualizeCell(endCell);
        }
        
        /// <summary>
        /// Main WFC algorithm.
        /// </summary>
        public void RunWFC()
        {
            SetStartAndFinish();
            
            while (!IsFullyCollapsed())
            {
                Cell nextCell = GetCellWithLowestEntropy();
                
                // No possible cell to collapse
                if (nextCell == null || nextCell.Entropy == 0)
                {
                    Debug.LogError("Error: No possible cell to collapse.");
                    return; 
                }

                CollapseCell(nextCell);
                Propagate(nextCell);
            }

            InstantiateTiles();
        }

        /// <summary>
        /// Checks if the map is fully collapsed.
        /// </summary>
        /// <returns>True if the map is fully collapsed, false otherwise</returns>
        bool IsFullyCollapsed()
        {
            foreach (var cell in grid)
            {
                if (!cell.IsCollapsed) return false;
            }
            return true;
        }

        /// <summary>
        /// Returns the cell with the lowest entropy.
        /// </summary>
        /// <returns>Cell with the lowest entropy</returns>
        Cell GetCellWithLowestEntropy()
        {
            Cell bestCell = null;
            int lowestEntropy = int.MaxValue;

            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 0; y < mapHeight; y++)
                {
                    Cell cell = grid[x, y];
                    if (!cell.IsCollapsed && cell.Entropy < lowestEntropy)
                    {
                        lowestEntropy = cell.Entropy;
                        bestCell = cell;
                    }
                }
            }
            return bestCell;
        }

        /// <summary>
        /// Collapses a cell by choosing a random variant from its available variants.
        /// </summary>
        /// <param name="cell">Cell to be collapsed</param>
        void CollapseCell(Cell cell)
        {
            int randomIndex = Random.Range(0, cell.AvailableVariants.Count);
            cell.CollapsedVariant = cell.AvailableVariants[randomIndex];
            cell.AvailableVariants.Clear();
            cell.AvailableVariants.Add(cell.CollapsedVariant);
            cell.IsCollapsed = true;
        }

        /// <summary>
        /// Propagates the collapsed cell's variant to its neighboring cells.'
        /// </summary>
        /// <param name="collapsedCell">Collapsed cell</param>
        void Propagate(Cell collapsedCell)
        {
            Stack<Cell> stack = new Stack<Cell>();
            stack.Push(collapsedCell);

            while (stack.Count > 0)
            {
                Cell current = stack.Pop();

                // Check all 4 directions
                Vector2Int[] directions = { 
                    new Vector2Int(0, 1),  // North
                    new Vector2Int(1, 0),  // East
                    new Vector2Int(0, -1), // South
                    new Vector2Int(-1, 0)  // West
                };

                for (int i = 0; i < 4; i++)
                {
                    Vector2Int neighborPos = current.GridPosition + directions[i];

                    // Check if the neighbor is within the map bounds
                    if (neighborPos.x >= 0 && neighborPos.x < mapWidth && neighborPos.y >= 0 && neighborPos.y < mapHeight)
                    {
                        Cell neighbor = grid[neighborPos.x, neighborPos.y];
                        if (neighbor.IsCollapsed) continue;

                        // Cut down the neighbours variants
                        bool changed = ConstrainNeighbor(current, neighbor, i);
                        
                        if (changed)
                        {
                            stack.Push(neighbor);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reduces the possible tile variants for the neighbor Cell by enforcing socket compatibility
        /// </summary>
        /// <param name="current">The current Cell that is influencing its neighbor.</param>
        /// <param name="neighbor">The neighboring Cell whose available variants are being constrained.</param>
        /// <param name="directionIndex">The direction from the current Cell to the neighbor Cell</param>
        /// <returns>
        /// Returns true if the neighbor's list of possible variants has changed, otherwise false.
        /// </returns>
        bool ConstrainNeighbor(Cell current, Cell neighbor, int directionIndex)
        {
            bool changed = false;
            // directionIndex: 0:N, 1:E, 2:S, 3:W
            int neighborSideIndex = (directionIndex + 2) % 4;

            List<TileVariant> toRemove = new List<TileVariant>();

            foreach (var neighborVariant in neighbor.AvailableVariants)
            {
                bool possible = false;
                foreach (var currentVariant in current.AvailableVariants)
                {
                    // Check for socket compatibility
                    if (currentVariant.Sockets[directionIndex] == neighborVariant.Sockets[neighborSideIndex])
                    {
                        possible = true;
                        break;
                    }
                }

                if (!possible)
                {
                    toRemove.Add(neighborVariant);
                    changed = true;
                }
            }

            foreach (var variant in toRemove)
            {
                neighbor.AvailableVariants.Remove(variant);
            }

            return changed;
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
                    Vector3 pos = new Vector3(cell.GridPosition.x * tileSize, 0, cell.GridPosition.y * tileSize);
                    Quaternion rot = Quaternion.Euler(0, cell.CollapsedVariant.Rotation * 90, 0);
                    Instantiate(cell.CollapsedVariant.Data.prefab, pos, rot, transform);
                }
            }
        }

        /// <summary>
        /// Determines whether a path exists between two points on the grid.
        /// Implements a breadth-first search algorithm to check connectivity.
        /// </summary>
        /// <param name="start">The starting point of the path search.</param>
        /// <param name="end">The target point to check for connectivity.</param>
        /// <returns>True if a path exists from the start to the end point; otherwise, false.</returns>
        public bool IsPathPossible(Vector2Int start, Vector2Int end)
        {
            Queue<Vector2Int> frontier = new Queue<Vector2Int>();
            frontier.Enqueue(start);

            HashSet<Vector2Int> reached = new HashSet<Vector2Int>();
            reached.Add(start);

            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();

                if (current == end) return true; // Path was found

                foreach (Vector2Int next in GetPathNeighbors(current))
                {
                    if (!reached.Contains(next))
                    {
                        reached.Add(next);
                        frontier.Enqueue(next);
                    }
                }
            }

            return false; // path does not exist
        }

        /// <summary>
        /// Returns a list of all path neighbors of a given position.
        /// </summary>
        /// <param name="pos">Position of tile on the grid</param>
        /// <returns></returns>
        List<Vector2Int> GetPathNeighbors(Vector2Int pos)
        {
            List<Vector2Int> neighbors = new List<Vector2Int>();
            Cell currentCell = grid[pos.x, pos.y];
            
            if (currentCell.CollapsedVariant == null) return neighbors;

            Vector2Int[] directions = { 
                new Vector2Int(0, 1),  // N (index 0)
                new Vector2Int(1, 0),  // E (index 1)
                new Vector2Int(0, -1), // S (index 2)
                new Vector2Int(-1, 0)  // W (index 3)
            };

            for (int i = 0; i < 4; i++)
            {
                
                if (currentCell.CollapsedVariant.Sockets[i] == "path")
                {
                    Vector2Int neighborPos = pos + directions[i];
                    
                    if (neighborPos.x >= 0 && neighborPos.x < mapWidth && neighborPos.y >= 0 && neighborPos.y < mapHeight)
                    {
                        Cell neighborCell = grid[neighborPos.x, neighborPos.y];
                        int oppositeSide = (i + 2) % 4;
                        
                        if (neighborCell.CollapsedVariant != null && 
                            neighborCell.CollapsedVariant.Sockets[oppositeSide] == "path")
                        {
                            neighbors.Add(neighborPos);
                        }
                    }
                }
            }
            return neighbors;
        }
        
        /// <summary>
        /// Generates a valid map by repeatedly running the WFC algorithm until a valid map is found.
        /// Iterates up to a maximum number of attempts to prevent infinite loops.
        /// After a valid map is generated, the tiles are instantiated in the scene.
        /// </summary>
        public void GenerateValidMap()
        {
            int attempts = 0;
            bool success = false;

            while (!success && attempts < 100)
            {
                attempts++;
                ClearScene();
                InitializeGrid(); // Reset of data
                
                RunWFC();

                if (IsPathPossible(new Vector2Int(0, 0), new Vector2Int(mapWidth - 1, mapHeight - 1)))
                {
                    success = true;
                    Debug.Log($"Map generated with {attempts} attempts");
                    InstantiateTiles();
                }
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
        /// Animated version of the WFC algorithm.
        /// Adds a small delay after collapsing each cell to visualize the generation process in real-time.
        /// </summary>
        /// <returns></returns>
        public IEnumerator RunWFCAnimated()
        {
            SetStartAndFinish(visualize: true);
            while (!IsFullyCollapsed())
            {
                Cell nextCell = GetCellWithLowestEntropy();
        
                if (nextCell == null || nextCell.Entropy == 0)
                {
                    Debug.LogError("Error: No possible cell to collapse!");
                    yield break; 
                }

                CollapseCell(nextCell);
                Propagate(nextCell);
                
                VisualizeCell(nextCell); 
                yield return new WaitForSeconds(0.05f); // Small delay
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
                Vector3 pos = new Vector3(cell.GridPosition.x * tileSize, 0, cell.GridPosition.y * tileSize);
                Quaternion rot = Quaternion.Euler(0, cell.CollapsedVariant.Rotation * 90, 0);
                Instantiate(cell.CollapsedVariant.Data.prefab, pos, rot, transform);
            }
        }
        
    }
}