using UnityEngine;

public class SpectatorCamera : MonoBehaviour
{
    [Header("Speed settings")]
    public float normalSpeed = 10f;
    public float fastSpeed = 30f; 
    public float lookSensitivity = 2f;

    private float rotationX = 0f;
    private float rotationY = 0f;
    

    /// <summary>
    /// Initializes the camera's rotation based on its current orientation in the scene.
    /// </summary>
    void Start()
    {
        Vector3 rot = transform.localRotation.eulerAngles;
        rotationX = rot.x;
        rotationY = rot.y;
    }

    /// <summary>
    /// Handles camera rotation and movement based on user input.
    /// </summary>
    void Update()
    {
        // Check if the cursor is locked - user is in the menu
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        // Camera rotation
        float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

        rotationY += mouseX;
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, -90f, 90f); 

        transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0f);

        // Camera movement
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? fastSpeed : normalSpeed;

        float moveRight = Input.GetAxis("Horizontal"); 
        float moveForward = Input.GetAxis("Vertical");   
        
        float moveUp = 0f;
        if (Input.GetKey(KeyCode.E)) moveUp = 1f;
        if (Input.GetKey(KeyCode.Q)) moveUp = -1f;

        Vector3 move = transform.right * moveRight + transform.forward * moveForward + transform.up * moveUp;
        
        transform.position += move * currentSpeed * Time.deltaTime;
    }
}   