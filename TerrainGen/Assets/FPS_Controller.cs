using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPS_Controller : MonoBehaviour
{
    public float walkingSpeed = 7.5f;
    public float runningSpeed = 11.5f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;
    public Camera playerCamera;
    public float lookSpeed = 2.0f;
    public float lookXLimit = 45.0f;

    public Texture2D heightMap;
    public Vector2 terrainOrigin;
    // public int terrainSize;
    public float heightScale = 2000f;
    public int heightResolution = 2048;

    public float playerHeight = 1.8f;
    private float[,] heightData;

    CharacterController characterController;
    Vector3 moveDirection = Vector3.zero;
    float rotationX = 0;

    [HideInInspector]
    public bool canMove = true;

    void Start()
    {
        characterController = GetComponent<CharacterController>();

        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Load height data from texture
        heightData = new float[heightResolution, heightResolution];
        for (int x = 0; x < heightResolution; x++)
        {
            for (int y = 0; y < heightResolution; y++)
            {
                Color pixelColor = heightMap.GetPixel(x, y);
                heightData[x, y] = pixelColor.r; // Assuming height is stored in the red channel
            }
        }
    }

    void Update()
    {
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float curSpeedX = canMove ? (isRunning ? runningSpeed : walkingSpeed) * Input.GetAxis("Vertical") : 0;
        float curSpeedZ = canMove ? (isRunning ? runningSpeed : walkingSpeed) * Input.GetAxis("Horizontal") : 0;

        float previousYVelocity = moveDirection.y;
        Debug.Log($"Previous Y velocity: {previousYVelocity}");

        // Vandret bev�gelse
        moveDirection = (forward * curSpeedX) + (right * curSpeedZ);

        // Behold tidligere Y velocity
        moveDirection.y = previousYVelocity;

        // Flyt horisontalt f�rst
        Vector3 horizontalMove = new Vector3(moveDirection.x, 0, moveDirection.z);
        characterController.Move(horizontalMove * Time.deltaTime);

        // Terrain grounding
        Vector3 pos = transform.position;
        float terrainHeight = SampleHeightCPU(pos.x, pos.z);

        bool grounded = false;

        float targetGroundY = terrainHeight + playerHeight;
        float distanceToGround = transform.position.y - targetGroundY;
        float test = targetGroundY - terrainHeight;
        //    Debug.Log($"target y: {targetGroundY} distance to ground: {distanceToGround} y position: {pos.y} movedirection:{moveDirection.y}");
        if (distanceToGround <= 0.05f)
        {
            moveDirection.y = -2f; // holder den grounded
            grounded = true;
            Debug.Log("Grounded! Current Y velocity set to: " + moveDirection.y);
        }
        else
        {
            moveDirection.y -= gravity * Time.deltaTime;

            grounded = false;
        }

        // Jump
        if (Input.GetKeyDown(KeyCode.Space) && canMove && grounded)
        {
            moveDirection.y = jumpSpeed;
            Debug.Log("Jumping! Current Y velocity: " + moveDirection.y);
        }

        // Flyt vertikalt
        characterController.Move(Vector3.up * moveDirection.y * Time.deltaTime);

        var flags = characterController.Move(Vector3.up * moveDirection.y * Time.deltaTime);
        if ((flags & CollisionFlags.Below) != 0)
        {
            Debug.Log("HIT BELOW: " + flags);
        }

        // Tving position (sikrer pr�cis grounding)
        //transform.position = new Vector3(transform.position.x, pos.y, transform.position.z);

        // Kamera rotation
        if (canMove)
        {
            rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
            transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
        }
    }

    float SampleHeightCPU(float worldX, float worldZ)
    {
        int x = Mathf.Clamp(Mathf.RoundToInt(worldX), 0, heightResolution - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt(worldZ), 0, heightResolution - 1);

        return heightData[x, z] * heightScale;
    }
}
