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
            //StartCoroutine(RunWFCAnimated());
            GenerateValidMap();
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
        /// Sets the start and finish tiles on the map.
        /// </summary>
        void SetStartAndFinish(bool visualize = false)
        {
            // Start in the first floor, first corner
            Cell startCell = grid[0, 0, 0];
            startCell.AvailableVariants = startVariants
                .Where(v => v.Sockets[0] == "path" || v.Sockets[1] == "path")
                .ToList();
            
            CollapseCell(startCell);
            Propagate(startCell);
            if (visualize)
            {
                VisualizeCell(startCell);
            }

            // Finish in the opposite corner at the last floor 
            Cell endCell = grid[mapWidth - 1, mapFloors - 1, mapDepth - 1];
            endCell.AvailableVariants = finishVariants
                .Where(v => v.Sockets[2] == "path" || v.Sockets[3] == "path")
                .ToList();
            
            CollapseCell(endCell);
            Propagate(endCell);
            if (visualize)
            {
                VisualizeCell(endCell);
            }
            
        }

        /// <summary>
        /// Main WFC algorithm.
        /// </summary>
        public void RunWFC()
        {
            SetStartAndFinish();
            ApplyBoundaryConstraints();
            
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
                for (int y = 0; y < mapFloors; y++)
                {
                    for (int z = 0; z < mapDepth; z++)
                    {
                        Cell cell = grid[x, y, z];
                        if (!cell.IsCollapsed && cell.Entropy < lowestEntropy)
                        {
                            lowestEntropy = cell.Entropy;
                            bestCell = cell;
                        }
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
            
            // Check all 6 directions
            Vector3Int[] directions = { 
                new Vector3Int(0, 0, 1),    // North - 0
                new Vector3Int(1, 0, 0),    // East - 1
                new Vector3Int(0, 0, -1),   // South - 2
                new Vector3Int(-1, 0, 0),   // West - 3
                new Vector3Int(0, 1, 0),    // Up - 4
                new Vector3Int(0, -1, 0)    // Down - 5
            };

            while (stack.Count > 0)
            {
                Cell current = stack.Pop();

                for (int i = 0; i < 6; i++)
                {
                    Vector3Int nPos = current.GridPosition + directions[i];
                    
                    // Check if the neighbor is within the map bounds
                    if (nPos.x >= 0 && nPos.x < mapWidth && 
                        nPos.y >= 0 && nPos.y < mapFloors && 
                        nPos.z >= 0 && nPos.z < mapDepth)
                    {
                        Cell neighbor = grid[nPos.x, nPos.y, nPos.z];
                        
                        if (neighbor.IsCollapsed)
                        {
                            continue;
                        }

                        
                        bool changed = ConstrainNeighbor(current, neighbor, i);

                        if (changed)
                        {
                            stack.Push(neighbor);
                        }
                        
                    }
                }
            }
        }

        int GetOppositeSide(int directionIndex)
        {
            if (directionIndex < 4)
            {
                return (directionIndex + 2) % 4; // North -> South, East -> West...
            }

            if (directionIndex == 4)
            {
                return 5; // Up -> Down
            }

            return 4; // Down -> Up
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
            int neighborSideIndex = GetOppositeSide(directionIndex);

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
                    Vector3 pos = new Vector3(cell.GridPosition.x * tileSize,
                        cell.GridPosition.y * tileSize,
                        cell.GridPosition.z * tileSize);
                    
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
        public bool IsPathPossible(Vector3Int start, Vector3Int end)
        {
            Queue<Vector3Int> frontier = new Queue<Vector3Int>();
            frontier.Enqueue(start);

            HashSet<Vector3Int> reached = new HashSet<Vector3Int>();
            reached.Add(start);

            while (frontier.Count > 0)
            {
                Vector3Int current = frontier.Dequeue();

                if (current == end) return true; // Path was found

                foreach (Vector3Int next in GetPathNeighbors(current))
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
        List<Vector3Int> GetPathNeighbors(Vector3Int pos)
        {
            List<Vector3Int> neighbors = new List<Vector3Int>();
            Cell currentCell = grid[pos.x, pos.y, pos.z];
            
            if (currentCell.CollapsedVariant == null) return neighbors;

            Vector3Int[] directions = { 
                new Vector3Int(0, 0, 1),    // North - 0
                new Vector3Int(1, 0, 0),    // East - 1
                new Vector3Int(0, 0, -1),   // South - 2
                new Vector3Int(-1, 0, 0),   // West - 3
                new Vector3Int(0, 1, 0),    // Up - 4
                new Vector3Int(0, -1, 0)    // Down - 5
            };

            for (int i = 0; i < 6; i++)
            {
                string socket = currentCell.CollapsedVariant.Sockets[i];   
                
                // Check if the neighbor is a path
                if (socket == "path" || socket.StartsWith("path_vertical") || socket.StartsWith("stairs_up"))
                {
                    Vector3Int nPos = pos + directions[i];
                    
                    if (nPos.x >= 0 && nPos.x < mapWidth && nPos.y >= 0 && nPos.y < mapFloors && nPos.z >= 0 && nPos.z < mapDepth)
                    {
                        Cell neighborCell = grid[nPos.x, nPos.y, nPos.z];
                        int oppositeSide = GetOppositeSide(i);

                        if (neighborCell.CollapsedVariant != null)
                        {
                            string neighborSocket = neighborCell.CollapsedVariant.Sockets[oppositeSide];
                            
                            // Check if the neighbor's socket is compatible with the current cell's socket
                            if (neighborSocket == "path" || neighborSocket.StartsWith("path_vertical") || neighborSocket.StartsWith("stairs_up"))
                            {
                                neighbors.Add(nPos);
                            }
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

            while (!success && attempts < 200)
            {
                attempts++;
                ClearScene();
                InitializeGrid(); // Reset of data
                ApplyBoundaryConstraints();
                
                RunWFC();

                if (IsFullyCollapsed() && IsPathPossible(new Vector3Int(0, 0, 0),
                        new Vector3Int(mapWidth - 1, mapFloors - 1, mapDepth - 1)))
                {
                    success = true;
                    Debug.Log($"Valid map generated after {attempts} attempts.");
                    InstantiateTiles();
                }
            }
            if (!success)
            {
                Debug.LogError("Failed to generate a valid map after 200 attempts.");
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
            ApplyBoundaryConstraints();
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
        
        void ApplyBoundaryConstraints()
        {
            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 0; y < mapFloors; y++)
                {
                    for (int z = 0; z < mapDepth; z++)
                    {
                        Cell cell = grid[x, y, z];
                        List<TileVariant> toRemove = new List<TileVariant>();

                        foreach (var variant in cell.AvailableVariants)
                        {
                            bool isValid = true;
                            
                            // Borders on X and Z axis must be solid
                            if (z == mapDepth - 1 && variant.Sockets[0] != "wall") isValid = false;
                            if (x == mapWidth - 1 && variant.Sockets[1] != "wall") isValid = false;
                            if (z == 0 && variant.Sockets[2] != "wall") isValid = false;
                            if (x == 0 && variant.Sockets[3] != "wall") isValid = false;
                            
                            // Borders on Y axis must be solid - bottom floor and upper floor ceiling
                            if (y == mapFloors - 1 && variant.Sockets[4] != "wall") isValid = false; // UP limit
                            if (y == 0 && variant.Sockets[5] != "wall") isValid = false; // DOWN limit

                            if (!isValid) toRemove.Add(variant);
                        }

                        foreach (var variant in toRemove) cell.AvailableVariants.Remove(variant);
                    }
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