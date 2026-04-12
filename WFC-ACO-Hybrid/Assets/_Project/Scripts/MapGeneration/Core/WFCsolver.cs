using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using _Project.Scripts.MapGeneration.Data;

namespace _Project.Scripts.MapGeneration.Core
{
    /// <summary>
    /// Class for the WFC solver.
    /// It handles the main WFC algorithm and its constraints.
    /// </summary>
    public class WFCSolver
    {
        // Map dimensions and grid
        private readonly Cell[,,] grid;
        private readonly int mapWidth;
        private readonly int mapFloors;
        private readonly int mapDepth;
        
        //History of removed variants for backtracking
        private readonly Stack<(Cell cell, TileVariant removedVariant)> removalHistory = new Stack<(Cell cell, TileVariant removedVariant)>();
        
        // Stack of snapshot markers for backtracking
        private readonly Stack<int> snapshotMarkers = new Stack<int>();
        
        private readonly CellMinHeap entropyHeap; // Min-heap for entropy-based priority queue
        
        // Flags for solver state
        private bool contradictionFound = false;
        
        public bool HasContradiction => contradictionFound;
        
        /// <summary>
        /// Property exposing the execution time spent exclusively in the entropy search phase during the last run.
        /// </summary>
        public double LastEntropySearchTimeMs { get; private set; }

        /// <summary>
        /// Constructor for the WFCSolver class.
        /// Initializes the solver with the grid, width, floors, and depth of the map.
        /// </summary>
        /// <param name="grid">The 3D grid of cells representing the map</param>
        /// <param name="width">Width of the map</param>
        /// <param name="floors">Number of floors in the map</param>
        /// <param name="depth">Depth of the map</param>
        public WFCSolver(Cell[,,] grid, int width, int floors, int depth)
        {
            this.grid = grid;
            this.mapWidth = width;
            this.mapFloors = floors;
            this.mapDepth = depth;
            
            this.entropyHeap = new CellMinHeap();
            InitializeEntropyHeap();
        }

        /// <summary>
        /// Initializes the entropy heap with all cells and their entropy values.
        /// </summary>
        private void InitializeEntropyHeap()
        {
            entropyHeap.Clear();
            foreach (var cell in grid)
            {
                if (cell.IsCollapsed)
                {
                    continue;
                }
                
                entropyHeap.Enqueue(cell, cell.Entropy);
            }
        }

        /// <summary>
        /// Applies boundary constraints to the map by removing any tile variants that would
        /// violate the solid wall requirement on the edges of the map.
        /// </summary>
        public void ApplyBoundaryConstraints()
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

                        foreach (var variant in toRemove)
                        {
                            cell.AvailableVariants.Remove(variant);
                        }
                        
                    }
                }
            }
        }

        /// <summary>
        /// Force the solver to collapse a cell to a specific variant.
        /// Used for setting start and finish tiles.
        /// </summary>
        public void ForceCollapse(Cell cell, List<TileVariant> allowedVariants)
        {
            cell.AvailableVariants = allowedVariants;
            CollapseCell(cell);
            Propagate(cell);
        }

        /// <summary>
        /// Main WFC algorithm.
        /// It repeatedly collapses the cell with the lowest entropy and propagates the constraints
        /// until the map is fully collapsed or a failure occurs (no valid variants for a cell).
        /// </summary>
        /// <returns>True if the map was successfully generated, false if a failure occurred.</returns>
        public bool RunWFC()
        {
            
            Stopwatch entropyStopwatch = new Stopwatch();
            LastEntropySearchTimeMs = 0;
            
            while (!IsFullyCollapsed())
            {
                entropyStopwatch.Start();
                Cell nextCell = GetCellWithLowestEntropyHeap();
                entropyStopwatch.Stop();
                
                if (nextCell == null || nextCell.Entropy == 0)
                {
                    return false; // Failure - no cell to collapse or a cell with no valid variants
                }

                CollapseCell(nextCell);
                Propagate(nextCell);
            }
            
            LastEntropySearchTimeMs = entropyStopwatch.ElapsedMilliseconds;
            return true;
        }

        /// <summary>
        /// Checks if the map is fully collapsed.
        /// </summary>
        /// <returns>True if the map is fully collapsed, false otherwise</returns>
        public bool IsFullyCollapsed()
        {
            foreach (var cell in grid)
            {
                if (!cell.IsCollapsed)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Returns the cell with the lowest entropy.
        /// </summary>
        /// <returns>Cell with the lowest entropy</returns>
        private Cell GetCellWithLowestEntropyNaive()
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
        /// Retrieves the cell with the lowest entropy from the min-heap.
        /// Actively discards stale and ghost references.
        /// </summary>
        /// <returns>The optimal uncollapsed Cell, or null if generation is complete.</returns>
        private Cell GetCellWithLowestEntropyHeap()
        {
            while (entropyHeap.Count > 0)
            {
                var node = entropyHeap.Dequeue();
                Cell cell = node.cell;
                int storedPriority = node.priority;
                
                // Check for collapsed cells or ghost entries
                if (cell.IsCollapsed || cell.Entropy != storedPriority)
                {
                    continue;
                }

                // Valid uncollapsed cell with available variants found
                if (cell.Entropy > 0)
                {
                    return cell;
                }
            }
            
            return null; 
        }
        
        /// <summary>
        /// Re-enqueues a cell into the heap when its entropy is reduced during propagation.
        /// </summary>
        private void UpdateCellEntropyInHeap(Cell cell)
        {
            if (!cell.IsCollapsed && cell.Entropy > 0)
            {
                entropyHeap.Enqueue(cell, cell.Entropy);
            }
        }

        /// <summary>
        /// Collapses the next lowest-entropy cell and propagates constraints.
        /// </summary>
        /// <param name="collapsedCell">Outputs the collapsed cell.</param>
        /// <returns>False if a contradiction occurred or no valid cell exists.</returns>
        public bool TryCollapseNextCell(out Cell collapsedCell)
        {
            collapsedCell = GetCellWithLowestEntropyHeap();
            if (collapsedCell == null || collapsedCell.Entropy == 0)
            {
                return false;
            }

            CollapseCell(collapsedCell);
            Propagate(collapsedCell);
            return true;
        }

        /// <summary>
        /// Collapses a cell by choosing a random variant from its available variants.
        /// </summary>
        /// <param name="cell">Cell to be collapsed</param>
        private void CollapseCell(Cell cell)
        {
            int randomIndex = Random.Range(0, cell.AvailableVariants.Count);
            cell.CollapsedVariant = cell.AvailableVariants[randomIndex];
            
            List<TileVariant> toRemove = new List<TileVariant>();

            foreach (var variant in cell.AvailableVariants)
            {
                if (variant != cell.CollapsedVariant)
                {
                    toRemove.Add(variant);
                }
            }

            foreach (var variant in toRemove)
            {
                RemoveVariantFromCell(cell, variant);
            }
            
            cell.IsCollapsed = true;
        }

        /// <summary>
        /// Helper for hybrid solvers that need deterministic collapse and propagation sequencing.
        /// </summary>
        public void CollapseAndPropagate(Cell cell)
        {
            CollapseCell(cell);
            Propagate(cell);
        }

        /// <summary>
        /// Propagates the collapsed cell's variant to its neighboring cells.'
        /// </summary>
        /// <param name="collapsedCell">Collapsed cell</param>
        private void Propagate(Cell collapsedCell)
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

            while (stack.Count > 0 && !contradictionFound)
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
                        
                        // Check if the neighbor's variant is compatible with the current cell's variant
                        bool changed = ConstrainNeighbor(current, neighbor, i);

                        if (changed)
                        {
                            stack.Push(neighbor);
                            
                            // Re-enqueue the neighbor in the heap to update its priority based on the new entropy
                            if (!neighbor.IsCollapsed)
                            {
                                UpdateCellEntropyInHeap(neighbor);
                            }
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
        private bool ConstrainNeighbor(Cell current, Cell neighbor, int directionIndex)
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
                RemoveVariantFromCell(neighbor, variant);
            }

            return changed;
        }
        
        /// <summary>
        /// Returns the opposite side of a direction index.
        /// </summary>
        /// <param name="directionIndex">Index of the direction North, South, ...</param>
        /// <returns></returns>
        public int GetOppositeSide(int directionIndex)
        {
            switch (directionIndex)
            {
                case < 4:
                    return (directionIndex + 2) % 4; // North -> South, East -> West...
                case 4:
                    return 5; // Up -> Down
                default:
                    return 4; // Down -> Up
            }
        }

        /// <summary>
        /// Saves the current state of the solver - the number of removed variants.
        /// </summary>
        public void SaveSnapshot()
        {
            snapshotMarkers.Push(removalHistory.Count);
        }

        /// <summary>
        /// Restores the removed variant from stack to the last saved snapshot.
        /// </summary>
        public void RestoreSnapshot()
        {
            if (snapshotMarkers.Count == 0)
            {
                return;
            }
            
            int targetHistorySize = snapshotMarkers.Pop();

            while (removalHistory.Count > targetHistorySize)
            {
                var record = removalHistory.Pop();
                
                // Add the removed variant back to the cell's available variants
                record.cell.AvailableVariants.Add(record.removedVariant);
                
                // Reset the cell's collapsed variant to null
                record.cell.IsCollapsed = false;
                record.cell.CollapsedVariant = null;
            }
        }

        /// <summary>
        /// Removes a variant from a cell's available variants.
        /// </summary>
        /// <param name="cell">The cell from which the variant is being removed.</param>
        /// <param name="variant">The tile variant that is being removed from the cell's available variants.</param>
        private void RemoveVariantFromCell(Cell cell, TileVariant variant)
        {
            cell.AvailableVariants.Remove(variant);
            removalHistory.Push((cell, variant));

            if (cell.AvailableVariants.Count == 0)
            {
                contradictionFound = true;
            }
        }

    }
}