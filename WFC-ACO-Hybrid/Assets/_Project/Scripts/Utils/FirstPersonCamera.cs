using UnityEngine;
using UnityEngine.InputSystem;

namespace _Project.Scripts.Utils
{
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonCamera : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 10f;
        public float fastMoveSpeed = 20f;
        public float jumpHeight = 5f;
        public float gravity = -9.81f; // Gravity
        public float jumpBufferTime = 0.15f;
        public float coyoteTime = 0.1f;

        [Header("Mouse Look Settings")]
        public float lookSpeed = 0.1f;
        public Transform playerCamera; // Sem v Inspectoru přetáhneš svou Main Camera

        private CharacterController controller;
        private Vector3 velocity;
        private float xRotation = 0f;
        private float jumpBufferCounter = 0f;
        private float coyoteCounter = 0f;

        void Start()
        {
            controller = GetComponent<CharacterController>();
            
            // Locks the cursor and hides it
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            if (Mouse.current == null || Keyboard.current == null) return;

            // ESC - unlocks the cursor and makes it visible
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
            }

            // if the cursor is locked, we handle mouse look and movement
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                HandleMouseLook();
                HandleMovement();
            }
        }

        void HandleMouseLook()
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            float mouseX = mouseDelta.x * lookSpeed;
            float mouseY = mouseDelta.y * lookSpeed;

            // Handle vertical rotation (looking up and down)
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

            transform.Rotate(Vector3.up * mouseX);
        }

        void HandleMovement()
        {
            float currentSpeed = Keyboard.current.leftShiftKey.isPressed ? fastMoveSpeed : moveSpeed;

            float x = 0f;
            float z = 0f;

            if (Keyboard.current.wKey.isPressed) z += 1f;
            if (Keyboard.current.sKey.isPressed) z -= 1f;
            if (Keyboard.current.aKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed) x += 1f;

            // Moving in the direction the player is facing
            Vector3 move = transform.right * x + transform.forward * z;
            controller.Move(move.normalized * currentSpeed * Time.deltaTime);

            bool grounded = controller.isGrounded;

            if (grounded)
            {
                coyoteCounter = coyoteTime;
            }
            else
            {
                coyoteCounter -= Time.deltaTime;
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                jumpBufferCounter = jumpBufferTime;
            }
            else
            {
                jumpBufferCounter -= Time.deltaTime;
            }

            // Gravity apllication
            if (grounded && velocity.y < 0)
            {
                velocity.y = -2f; // Grounding the player to prevent floating
            }

            if (jumpBufferCounter > 0f && coyoteCounter > 0f)
            {
                // Convert desired jump height into initial vertical velocity.
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpBufferCounter = 0f;
                coyoteCounter = 0f;
            }

            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}