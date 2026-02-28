namespace _Project.Scripts.MapGeneration.Data
{
    public class TileVariant
    {
        public TileData Data;
        public int Rotation; // 0, 1, 2, 3
        public string[] Sockets; // Indexy 0:N, 1:E, 2:S, 3:W

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