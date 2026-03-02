namespace _Project.Scripts.MapGeneration.Data
{
    public class TileVariant
    {
        public TileData Data;
        public int Rotation; // 0, 1, 2, 3
        public string[] Sockets; // Indexes 0:N, 1:E, 2:S, 3:W

        /// <summary>
        /// Constructor for the TileVariant class. It takes a TileData object and a rotation value (0-3)
        /// and calculates the sockets based on the original tile data and the rotation.
        /// </summary>
        /// <param name="data">Tile data</param>
        /// <param name="rotation">Rotation value (0-3)</param>
        public TileVariant(TileData data, int rotation)
        {
            Data = data;
            Rotation = rotation;
            Sockets = new string[4];

            // Array for original sockets
            string[] orig = { data.socketNorth, data.socketEast, data.socketSouth, data.socketWest };

            for (int i = 0; i < 4; i++)
            {
                // Move sockets by the rotation
                Sockets[i] = orig[(i - rotation + 4) % 4];
            }
        }
    }
}