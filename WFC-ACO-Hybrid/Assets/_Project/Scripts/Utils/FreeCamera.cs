using UnityEngine;
using UnityEngine.InputSystem; 

namespace _Project.Scripts.Utils
{
    public class FreeCamera : MonoBehaviour
    {
        [Header("Movement settings")]
        public float moveSpeed = 20f;
        public float fastMoveSpeed = 60f;
        public float lookSpeed = 0.2f; 

        private float rotationX = 0f;
        private float rotationY = 0f;

        void Start()
        {
            Vector3 rot = transform.localRotation.eulerAngles;
            rotationX = rot.y;
            rotationY = rot.x;
        }

        void Update()
        {
            if (Mouse.current == null || Keyboard.current == null) return;

            // Look around with right mouse button
            if (Mouse.current.rightButton.isPressed)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                
                rotationX += mouseDelta.x * lookSpeed;
                rotationY -= mouseDelta.y * lookSpeed;
                
                rotationY = Mathf.Clamp(rotationY, -90f, 90f); 
                
                transform.localRotation = Quaternion.Euler(rotationY, rotationX, 0);
            }

            // Movement with WASD + QE for up/down
            float currentSpeed = Keyboard.current.leftShiftKey.isPressed ? fastMoveSpeed : moveSpeed;
            
            float horizontal = 0f;
            float vertical = 0f;
            float upDown = 0f;

            if (Keyboard.current.wKey.isPressed) vertical += 1f;
            if (Keyboard.current.sKey.isPressed) vertical -= 1f;
            if (Keyboard.current.aKey.isPressed) horizontal -= 1f;
            if (Keyboard.current.dKey.isPressed) horizontal += 1f;

            if (Keyboard.current.eKey.isPressed) upDown += 1f;
            if (Keyboard.current.qKey.isPressed) upDown -= 1f;

            // Normalize movement direction to prevent faster diagonal movement
            Vector3 moveDirection = new Vector3(horizontal, upDown, vertical).normalized;
            
            transform.Translate(moveDirection * currentSpeed * Time.deltaTime, Space.Self);
        }
    }
}