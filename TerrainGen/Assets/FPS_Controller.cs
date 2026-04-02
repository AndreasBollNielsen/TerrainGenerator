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
    public float flySpeedMultiplier = 3.0f;
    public float groundSnapSpeed = 10f;

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
    bool flyMode = false;

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
        if (Input.GetKeyDown(KeyCode.F))
            flyMode = !flyMode;

        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float speed = isRunning ? runningSpeed : walkingSpeed;

        if (flyMode)
        {
            if (canMove)
            {
                float flySpeed = speed * flySpeedMultiplier;
                float curSpeedX = flySpeed * Input.GetAxis("Vertical");
                float curSpeedZ = flySpeed * Input.GetAxis("Horizontal");
                float curSpeedY = flySpeed * (Input.GetKey(KeyCode.Space) ? 1f : Input.GetKey(KeyCode.LeftControl) ? -1f : 0f);

                Vector3 flyMove = (forward * curSpeedX) + (right * curSpeedZ) + (Vector3.up * curSpeedY);
                characterController.Move(flyMove * Time.deltaTime);
            }
        }
        else
        {
            float curSpeedX = canMove ? speed * Input.GetAxis("Vertical") : 0;
            float curSpeedZ = canMove ? speed * Input.GetAxis("Horizontal") : 0;

            float previousYVelocity = moveDirection.y;
            moveDirection = (forward * curSpeedX) + (right * curSpeedZ);
            moveDirection.y = previousYVelocity;

            // Horizontal move
            Vector3 horizontalMove = new Vector3(moveDirection.x, 0, moveDirection.z);
            characterController.Move(horizontalMove * Time.deltaTime);

            // Terrain grounding — sample AFTER horizontal move so slope is accounted for
            float terrainHeight = SampleHeightCPU(transform.position.x, transform.position.z);

            float targetGroundY = terrainHeight + playerHeight;
            float distanceToGround = transform.position.y - targetGroundY;
            bool grounded = false;

            if (distanceToGround <= 0.15f)
            {
                grounded = true;
            }
            else
            {
                moveDirection.y -= gravity * Time.deltaTime;
            }

            // Jump — checked before grounding snap so it isn't zeroed out the same frame
            if (Input.GetKeyDown(KeyCode.Space) && canMove && grounded)
            {
                moveDirection.y = jumpSpeed;
                grounded = false;
            }

            if (grounded)
            {
                moveDirection.y = 0f;
                float snappedY = Mathf.Lerp(transform.position.y, targetGroundY, groundSnapSpeed * Time.deltaTime);
                characterController.Move(Vector3.up * (snappedY - transform.position.y));
            }
            else
            {
                characterController.Move(Vector3.up * moveDirection.y * Time.deltaTime);
            }
        }

        // Camera rotation
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
        float fx = Mathf.Clamp(worldX, 0f, heightResolution - 1);
        float fz = Mathf.Clamp(worldZ, 0f, heightResolution - 1);

        int x0 = Mathf.Min((int)fx, heightResolution - 2);
        int z0 = Mathf.Min((int)fz, heightResolution - 2);
        int x1 = x0 + 1;
        int z1 = z0 + 1;

        float tx = fx - x0;
        float tz = fz - z0;

        float h00 = heightData[x0, z0];
        float h10 = heightData[x1, z0];
        float h01 = heightData[x0, z1];
        float h11 = heightData[x1, z1];

        float h = Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
        return h * heightScale;
    }
}
