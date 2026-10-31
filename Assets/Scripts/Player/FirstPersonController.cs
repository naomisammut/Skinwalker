using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    public Transform cam;
    public float walkSpeed = 4f;
    public float mouseSensitivity = 2f;
    public float gravity = -20f;

    CharacterController controller;
    float xRotation = 0f;
    float verticalVelocity = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Mouse look
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation = Mathf.Clamp(xRotation - my, -85f, 85f);
        cam.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mx);

        // Walking (WASD / arrow keys)
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 move = (transform.right * h + transform.forward * v);
        if (move.magnitude > 1f) move.Normalize();

        // Gravity
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * walkSpeed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        // Press Esc to free the mouse
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}