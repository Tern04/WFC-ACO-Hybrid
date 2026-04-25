using UnityEngine;

/// <summary>
/// Class for controlling the minimap camera.
/// </summary>
public class MinimapController : MonoBehaviour
{
    private Camera minimapCam; // Reference to the minimap camera
    
    [Header("Settings")]
    public GameObject marker; // Marker where the camera is located
    public float markerHeightOffset = 2f; // High offset for the marker

    
    private Transform playerTransform; // Reference to the player's transform
    private _Project.Scripts.MapGeneration.Core.MapGenerator generator; // Reference to the map generator

    /// <summary>
    /// Initializes the minimap camera and finds the player's transform.'
    /// </summary>
    void Awake()
    {
        minimapCam = GetComponent<Camera>();
        generator = FindFirstObjectByType<_Project.Scripts.MapGeneration.Core.MapGenerator>();
    }

    /// <summary>
    /// Updates the marker position based on the player's position.'
    /// </summary>
    void Update()
    {
        if (playerTransform == null && Camera.main != null)
        {
            playerTransform = Camera.main.transform;
        }

        // Movement of the marker
        if (playerTransform != null && marker != null && generator != null)
        {
            // Calculate floor
            int calculatedFloor = Mathf.FloorToInt(playerTransform.position.y / generator.tileSize);
            
            // Stop the marker from going out of bounds
            int clampedFloor = Mathf.Clamp(calculatedFloor, 0, generator.mapFloors - 1);
            
            // Calculate the Y position of the marker based on the clamped floor
            float currentFloorY = clampedFloor * generator.tileSize;
            
            // Move the marker to the calculated position
            marker.transform.position = new Vector3(playerTransform.position.x, currentFloorY + markerHeightOffset, playerTransform.position.z);
        }
    }

    /// <summary>
    /// Updates the minimap slicing based on the current floor and map dimensions.
    /// </summary>
    public void UpdateMinimapSlicing(int currentFloor, float tileSize, int mapWidth, int mapDepth)
    {
        if (generator == null) return;

        // Center of the map
        float centerX = (mapWidth * tileSize) / 2f - (tileSize / 2f);
        float centerZ = (mapDepth * tileSize) / 2f - (tileSize / 2f);

        // Camera position
        float targetY = (currentFloor * tileSize) + (tileSize * 0.5f);
        transform.position = new Vector3(centerX, targetY, centerZ);
        
        // Set the camera's field of view based on the map dimensions
        minimapCam.nearClipPlane = 0.1f;
        minimapCam.farClipPlane = tileSize; 

        // Set the camera's orthographic size based on the map dimensions
        float maxDim = Mathf.Max(mapWidth, mapDepth);
        minimapCam.orthographicSize = (maxDim * tileSize) / 2f;
    }
}