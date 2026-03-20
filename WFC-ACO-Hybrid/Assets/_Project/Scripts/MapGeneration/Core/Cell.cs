using UnityEngine;
using _Project.Scripts.MapGeneration.Data;
using System.Collections.Generic;

namespace _Project.Scripts.MapGeneration.Core
{
    public class Cell
    {
        public Vector3Int GridPosition; // Position of the cell in the grid
        public bool IsCollapsed; // Indicates whether the cell has been collapsed to a single tile or not

        public List<TileVariant> AvailableVariants; // List of possible tiles that can still be placed in this cell based on the WFC constraints

        public TileVariant CollapsedVariant; // Final tile that was chosen
        
        public bool isMainPath = false;
        
        /// <summary>
        /// Constructor for the Cell class. Initializes the cell with its grid position and a list of
        /// all possible tile variants.
        /// </summary>
        /// <param name="pos">Position on the grid</param>
        /// <param name="allVariants">List of tile variants</param>
        public Cell(Vector3Int pos, List<TileVariant> allVariants)
        {
            GridPosition = pos;
            IsCollapsed = false;

            // All tile variations are initially available
            AvailableVariants = new List<TileVariant>(allVariants);
        }

        // Entropy - a number of possible tiles that can be placed
        public int Entropy => AvailableVariants.Count;
    }
}