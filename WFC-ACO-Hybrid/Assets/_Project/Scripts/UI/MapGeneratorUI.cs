using UnityEngine;

namespace _Project.Scripts.MapGeneration.Core
{
    public class MapGeneratorUI : MonoBehaviour
    {
        [Header("Map Generator")]
        public MapGenerator mapGenerator;
        
        [Header("Minimap")]
        public RenderTexture minimapTexture;

        // UI data
        private int selectedMapIndex = 2; 
        private readonly string[] mapSizeLabels = { "Small (10x3x10)", "Medium (20x4x20)", "Large (30x5x30)" };
        private readonly Vector3Int[] mapSizes = { 
            new Vector3Int(10, 3, 10), 
            new Vector3Int(20, 4, 20), 
            new Vector3Int(30, 5, 30) 
        };

        private int selectedAlgoIndex = 1; 
        private readonly string[] algoLabels = { "DFS", "ACO", "Pure WFC" };

        private string resultText = "Waiting for input...";
        private bool isGenerating = false;
        private bool showMenu = true;
        
        private bool generateNextFrame = false; // Flag for triggering generation on next frame

        // Texture for the background of the menu
        private Texture2D solidBackground;
        
        // Minimap data
        private int currentMinimapFloor = 0;
        private MinimapController minimapController;

        /// <summary>
        /// Initializes the UI and sets up the cursor.
        /// </summary>
        private void Start()
        {
            solidBackground = new Texture2D(1, 1);
            solidBackground.SetPixel(0, 0, new Color(0.15f, 0.15f, 0.15f, 1f));
            solidBackground.Apply();
            minimapController = FindFirstObjectByType<MinimapController>();
            
            UpdateCursorState();
        }

        /// <summary>
        /// Handles input and updates the UI.
        /// ESC toggles the menu
        /// </summary>
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && !isGenerating)
            {
                showMenu = !showMenu;
                UpdateCursorState();
            }

            // Check for generation flag
            if (generateNextFrame)
            {
                generateNextFrame = false;
                
                showMenu = false;       
                UpdateCursorState();    
                StartGeneration();      
            }
            
            if (!showMenu && minimapController != null && mapGenerator != null && Camera.main != null)
            {
                // Calculate floor based on camera's Y position
                int calculatedFloor = Mathf.FloorToInt(Camera.main.transform.position.y / mapGenerator.tileSize);
                
                // Clamp the calculated floor to valid range
                calculatedFloor = Mathf.Clamp(calculatedFloor, 0, mapGenerator.mapFloors - 1);

                // Only update if the floor has changed to avoid unnecessary updates
                if (calculatedFloor != currentMinimapFloor)
                {
                    currentMinimapFloor = calculatedFloor;
                    minimapController.UpdateMinimapSlicing(currentMinimapFloor, mapGenerator.tileSize, mapGenerator.mapWidth, mapGenerator.mapDepth);
                }
            }
        }

        /// <summary>
        /// Updates the cursor state based on the showMenu flag.
        /// </summary>
        private void UpdateCursorState()
        {
            Cursor.visible = showMenu;
            Cursor.lockState = showMenu ? CursorLockMode.None : CursorLockMode.Locked;
        }

        /// <summary>
        /// Draws the UI elements.
        /// </summary>
        private void OnGUI()
        {
            if (mapGenerator == null)
            {
                return;
            }
            
            DrawMinimap();

            // Little hint when menu is closed
            if (!showMenu)
            {
                GUI.Box(new Rect(20, 20, 160, 40), "<b>ESC = Open menu</b>");
                
                // Little box
                GUIStyle resultStyle = new GUIStyle(GUI.skin.box);
                resultStyle.normal.background = solidBackground;
                
                GUI.Box(new Rect(20, 70, 200, 100), "", resultStyle); 
                GUI.Label(new Rect(30, 80, 180, 90), resultText); 
                
                return; 
            }

            
            // Full screen background
            GUIStyle bgStyle = new GUIStyle();
            bgStyle.normal.background = solidBackground;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, bgStyle);

            // Window panel
            float panelWidth = 650; 
            float panelHeight = 780;
            float startX = (Screen.width - panelWidth) / 2;
            float startY = (Screen.height - panelHeight) / 2;

            GUILayout.BeginArea(new Rect(startX, startY, panelWidth, panelHeight), GUI.skin.window);
            
            GUILayout.Space(30);
            
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("<size=28><b>WFC based generator</b></size>");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            
            GUILayout.Space(50);

            // Grid size selection
            GUILayout.Label("<size=18><b>1. Grid size:</b></size>");
            GUILayout.Space(10);
            selectedMapIndex = GUILayout.SelectionGrid(selectedMapIndex, mapSizeLabels, 1, GUILayout.Height(90));
            
            GUILayout.Space(40);

            // Algorithm selection
            GUILayout.Label("<size=18><b>2. Algorithm::</b></size>");
            GUILayout.Space(10);
            selectedAlgoIndex = GUILayout.SelectionGrid(selectedAlgoIndex, algoLabels, 1, GUILayout.Height(90));
            
            GUILayout.Space(50);

            // Generate button
            GUI.enabled = !isGenerating; 
            if (GUILayout.Button("<size=22><b>G E N E R A T E</b></size>", GUILayout.Height(70)))
            {
                generateNextFrame = true;
            }
            GUI.enabled = true;

            GUILayout.Space(20);

            // Exit button
            if (GUILayout.Button("<size=22><b>E X I T</b></size>", GUILayout.Height(60)))
            {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
            }

            GUILayout.EndArea();
        }
        
        /// <summary>
        /// Draws the minimap texture.
        /// </summary>
        void DrawMinimap()
        {
            if (minimapTexture != null)
            {
                int mapSize = 450;
                int padding = 20;
                Rect minimapRect = new Rect(Screen.width - mapSize - padding, padding, mapSize, mapSize);
            
                string title = $"Minimap - Floor {currentMinimapFloor}";
                GUI.Box(new Rect(minimapRect.x - 5, minimapRect.y - 5, mapSize + 10, mapSize + 10), title);
            
                GUI.DrawTexture(minimapRect, minimapTexture, ScaleMode.ScaleToFit, false);
            }
        }

        /// <summary>
        /// Starts the generation process.
        /// </summary>
        private void StartGeneration()
        {
            isGenerating = true;
            resultText = "Generating map...";
            Vector3Int chosenSize = mapSizes[selectedMapIndex];
            mapGenerator.GenerateMapFromUI(chosenSize.x, chosenSize.y, chosenSize.z, selectedAlgoIndex, this);
        }

        /// <summary>
        /// Updates the result text based on the generation status.
        /// </summary>
        /// <param name="success">Whether the generation was successful or not</param>
        /// <param name="timeMs">Time taken for generation in milliseconds</param>
        /// <param name="attempts">Number of attempts/restarts during generation</param>
        /// <param name="pathLength">Length of the path found (if successful)</param>
        public void UpdateResult(bool success, float timeMs, int attempts, int pathLength)
        {
            isGenerating = false;
            
            // Reset minimap floor
            currentMinimapFloor = 0;
            if (minimapController != null)
            {
                minimapController.UpdateMinimapSlicing(currentMinimapFloor, mapGenerator.tileSize, mapGenerator.mapWidth, mapGenerator.mapDepth);
            }
            
            // Reset camera position
            if (Camera.main != null)
            {
                Camera.main.transform.position = new Vector3(0, mapGenerator.tileSize * 0.5f, 0);
                Camera.main.transform.rotation = Quaternion.Euler(0, 45, 0);
            }

            if (success)
            {
                resultText = $"State: <color=#00FF00><b>SUCCES</b></color>\n\n" +
                             $"Time: <b>{timeMs:F0} ms</b>\n" +
                             $"Restarts: <b>{attempts}</b>\n" +
                             $"Path length: <b>{pathLength}</b>";
            }
            else
            {
                resultText = $"State: <color=#FF0000><b>FAILURE</b></color>\n\n" +
                             $"Time: <b>{timeMs:F0} ms</b>\n" +
                             $"Tried {attempts} times.";
            }
        }
    }
}