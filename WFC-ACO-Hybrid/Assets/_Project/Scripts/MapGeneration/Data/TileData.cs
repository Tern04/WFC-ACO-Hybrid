using UnityEngine;

namespace _Project.Scripts.MapGeneration.Data
{
    
    [CreateAssetMenu(fileName = "NewTile", menuName = "WFC/Tile Data")]
    public class TileData : ScriptableObject
    {
        public GameObject prefab; // Prefab of the tile that will be instantiated in the scene
        public string tileName;   // Name of the tile for identification
        
        [Header("Sockets (Neighbourhood Connections)")]
        // Sockets for neighboring connections 
        //public string socketUp; // Socket for the tile above
        //public string socketDown; // Socket for the tile below
        public string socketNorth; // Socket for the tile to the north
        public string socketEast; // Socket for the tile to the east
        public string socketSouth; // Socket for the tile to the south
        public string socketWest; // Socket for the tile to the west
    
        [Header("WFC Settings")]
        public float baseWeight = 1f; // Frequency of the tile being chosen during the WFC process
    }
    
}
