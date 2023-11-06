using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor.Rendering;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;


public class VoxelGenerator : MonoBehaviour
{

    [SerializeField] int height = 4;
    [SerializeField] int width = 4;
    [Range(1, 8)][SerializeField] int maxXchunks;
    [Range(1, 8)][SerializeField] int maxZchunks;
    [Range(1, 7)][SerializeField] int maxXTiles;
    [Range(1, 7)][SerializeField] int maxZTiles;
    [Range(1, 64)][SerializeField] int voxelSize;
    [Range(-10, 100)][SerializeField] float displayThreshold;
    int currentVoxelSize;
    int xVoxels;
    int yVoxels;
    int zVoxels;
    VoxelData[] voxelData; // One-dimensional array to store voxel colors
    Color[] heightMap;
    int heightmapWidth;
    int currentChunkSizeX;
    int currentChunkSizeZ;
    int maxChunkWidth;


    [SerializeField] Color startColor = Color.blue;  // Color for low DistanceToSurface values
    [SerializeField] Color endColor = new Color(1, 0, 0, 0);     // Color for high DistanceToSurface values
    [SerializeField] bool debug;
    TerrainTile[,] tiles;

    public Transform player;
    Vector3 lastpos;
    private int lastTileX;
    private int lastTileZ;
    int numChanges;

    private void Start()
    {
        tiles = new TerrainTile[maxXTiles, maxZTiles];
        currentVoxelSize = voxelSize;
        lastpos = player.position;
        lastTileX = Mathf.FloorToInt(lastpos.x / 2048); // Initial tile X
        lastTileZ = Mathf.FloorToInt(lastpos.z / 2048);

        currentChunkSizeX = maxXchunks;
        currentChunkSizeZ = maxZchunks;
        UpdateTilemap();

        for (int xtile = 0; xtile < maxXTiles; xtile++)
        {
            for (int ytile = 0; ytile < maxZTiles; ytile++)
            {
                InitializeHeightmap(xtile, ytile);


                // Calculate max chunk width based on tile position
                maxChunkWidth = tiles[xtile, ytile].Width;
                int currentChunkSizeX = tiles[xtile, ytile].NumChunks;
                int currentChunkSizeZ = currentChunkSizeX;



                //iterating each chunk inside a tile
                for (int x = 0; x < currentChunkSizeX; x++)
                {
                    for (int z = 0; z < currentChunkSizeZ; z++)
                    {
                        //initialize voxelsize
                        InitializeVoxelSize(maxChunkWidth);

                        // Generate the voxel structure
                        GenerateVoxelStructure(maxChunkWidth * x, maxChunkWidth * z);

                        // generate mesh
                        GenerateMesh(new Vector2Int(maxChunkWidth * x, maxChunkWidth * z), new Vector2Int(heightmapWidth * xtile, heightmapWidth * ytile), maxChunkWidth);


                    }
                }

                //generate new tile
                TerrainTile tile = tiles[xtile, ytile];
                tile.AddChunks(GetComponent<MeshGenerator>().CopyChunks());
                GetComponent<MeshGenerator>().ClearList();
                GameObject terrainTile = new GameObject();
                tile.SetChunksParent(terrainTile);
                Vector3 tileposition = new Vector3(heightmapWidth * xtile, 0, heightmapWidth * ytile);
                terrainTile.transform.position = tileposition;
                terrainTile.name = $"Tile_{xtile}_{ytile}";
                terrainTile.transform.SetParent(gameObject.transform, false);

                tiles[xtile, ytile] = tile;

            }
        }







    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {


            for (int x = 0; x < tiles.GetLength(0); x++)
            {
                for (int y = 0; y < tiles.GetLength(0); y++)
                {
                    Color col = new Color(0, 0, 0);

                    switch (tiles[x, y].Width)
                    {
                        case 256:
                            col = Color.green;
                            break;
                        case 512:
                            col = Color.blue;
                            break;
                        case 1024:
                            col = Color.yellow;
                            break;
                        case 2048:
                            col = Color.red;
                            break;
                        default:
                            break;
                    }
                    Gizmos.color = col;
                    Vector3 cubepos = new Vector3(x * 2048.2f, 0, y * 2048.2f) + new Vector3(1024, 0, 1024);
                    Gizmos.DrawWireCube(cubepos, new Vector3(2048, 5, 2048));

                    if (tiles[x, y].Width == 0)
                        Debug.Log($"pos: {x}:{y} value: {tiles[x, y].Width}");
                }
            }





        }
#endif
    }

    private void Update()
    {
        // regenerate voxels on voxelsize change
        if (currentVoxelSize != voxelSize)
        {
            InitializeHeightmap(0, 0);
            InitializeVoxelSize(256);
            GenerateVoxelStructure(0, 0);
           // DestroyMesh();
            GenerateMesh(Vector2Int.zero, Vector2Int.zero, 256);

            currentVoxelSize = voxelSize;
        }

        if (debug)
        {
            DisplayVoxels(xVoxels, yVoxels);

        }

        // Debug.Log($"playerpos: {player.position} lastpos: {lastpos}");

        int currentTileX = Mathf.FloorToInt(player.position.x / 2048); // Current tile X
        int currentTileZ = Mathf.FloorToInt(player.position.z / 2048);

        // Check if the player has moved to a new tile
        if (currentTileX != lastTileX || currentTileZ != lastTileZ)
        {
            numChanges = 0;
            UpdateTilemap();
            Debug.Log($"number of changed tiles {numChanges}");
            lastTileX = currentTileX;
            lastTileZ = currentTileZ;
        }


    }

    void initTiles()
    {
        for (int x = 0; x < tiles.GetLength(0); x++)
        {
            for (int y = 0; y < tiles.GetLength(1); y++)
            {
                if (tiles[x, y] != null)
                {
                    tiles[x, y].Width = 0;
                    tiles[x, y].NumChunks = 0;

                }
                else
                {
                    tiles[x, y] = new TerrainTile(x,y);
                    tiles[x, y].LODChanged += VoxelGenerator_LODChanged;
                }
            }
        }
    }

    private void VoxelGenerator_LODChanged(Vector2Int tilepos)
    {

        //update mesh chunk per tile
        DestroyMesh(tilepos);

        numChanges++;
        
    }

    void UpdateTilemap()
    {
        Debug.Log("updating tiles");

        initTiles();

        int tileWidth = tiles.GetLength(0); // Assuming a rectangular array
        int height = tiles.GetLength(1);
        int tileSize = 2048;
        int centerX = Mathf.FloorToInt(player.position.x / tileSize);
        int centerY = Mathf.FloorToInt(player.position.z / tileSize);


        // Debug.Log($"pos: {centerX}:{centerY}");
        tiles[centerX, centerY].Width = 256; // Initialize with the original value
        int lastValue = tiles[centerX, centerY].Width;
        int currentValue = lastValue * 2;

        for (float radius = 0; radius <= 10; radius += 1.1f)
        {
            for (int angle = 0; angle < 360; angle++) // Iterate through all angles (0-359 degrees)
            {
                double radianAngle = angle * (Math.PI / 180); // Convert angle to radians
                int newX = centerX + Mathf.RoundToInt(radius * Mathf.Cos((float)radianAngle));
                int newY = centerY + Mathf.RoundToInt(radius * Mathf.Sin((float)radianAngle));



                // Check if the calculated point is within the array bounds
                if (newX >= 0 && newX < tileWidth && newY >= 0 && newY < height)
                {
                    //skip if tile is origin
                    if (newX == centerX && newY == centerY || tiles[newX, newY].Width >= width)
                    {
                        continue;
                    }

                    // Update the array with the current value
                    tiles[newX, newY].Width = currentValue;


                    // Update lastValue for the next iteration
                    lastValue = currentValue;

                    //  Debug.Log($"{newX}:{newY} value: {tiles[newX, newY].Width}");
                }
            }
            currentValue = lastValue * 2; // Calculate the current value
            currentValue = Mathf.Clamp(currentValue, 256, 2048);
        }
    }

    void InitializeVoxelSize(int maxWidth)
    {
        // Calculate the number of voxels in each dimension
        xVoxels = maxWidth / voxelSize;
        yVoxels = height / voxelSize;
        zVoxels = xVoxels;

        // Initialize the voxelData array based on the calculated dimensions
        int totalVoxels = xVoxels * yVoxels * zVoxels;
        voxelData = new VoxelData[totalVoxels];

        // Debug.Log("voxels initialized");
    }

    void InitializeHeightmap(int x, int y)
    {
        string textureName = $"Textures\\heightmap_{x}_{y}";
        // Debug.Log(textureName);
        Texture2D heightmapTexture = Resources.Load<Texture2D>(textureName);

        if (heightmapTexture != null)
        {
            heightMap = heightmapTexture.GetPixels();
            heightmapWidth = heightmapTexture.width;
            // Debug.Log(heightmapWidth);
        }
        else
        {
            Debug.LogError($"Heightmap texture '{textureName}' not found in Resources.");
        }
    }

    private void GenerateVoxelStructure(int offsetX, int offsetZ)
    {
        for (int _index = 0; _index < voxelData.Length; _index++)
        {

            //get x & z coords
            int z = _index % xVoxels;
            int y = (_index / xVoxels) % yVoxels;
            int x = _index / (xVoxels * yVoxels);

            // Apply the x and z offsets here
            int xOffset = offsetX;
            int zOffset = offsetZ;

            // Multiply the voxel positions by the voxel size first
            x *= voxelSize;
            z *= voxelSize;

            // Apply offsets
            x += xOffset;
            z += zOffset;


            //using perlin noise
            //float perlinNoise = Mathf.PerlinNoise((float)x * voxelSize / 16f * 1.5f, (float)z * voxelSize / 16f * 1.5f);
            // voxelData[_index].DistanceToSurface = (y * voxelSize) - (10.0f * perlinNoise);


            //sample the height map
            int index = (x * heightmapWidth) + z;
            Color sampledColor = heightMap[index];
            float sampledHeight = sampledColor.r;
            float scaledHeight = sampledHeight * 2000;

            voxelData[_index].DistanceToSurface = (y * voxelSize) - scaledHeight;
        }

        //  Debug.Log("done filling data structure");
    }

    private void DisplayVoxels(int voxelwidth, int voxelheight)
    {


        for (int index = 0; index < voxelData.Length; index++)
        {
            // Convert the 1D index to 3D coordinates
            int x = index % voxelwidth;
            int y = (index / voxelwidth) % voxelheight;
            int z = index / (voxelwidth * voxelheight);

            // Calculate the position of the voxel in world space
            Vector3 voxelPosition = new Vector3(
                x * voxelSize,
                y * voxelSize,
                z * voxelSize
            );

            // Calculate a t value based on DistanceToSurface
            float t = Mathf.InverseLerp(0f, 10f, voxelData[index].DistanceToSurface);

            // Interpolate between startColor and endColor based on t
            Color voxelcol = Color.Lerp(startColor, endColor, t);

            // Call the DrawCube method to draw a cube at the voxel's position
            if (voxelData[index].DistanceToSurface <= displayThreshold)
            {
                DrawCube(voxelPosition, voxelSize, voxelcol);

            }
        }
    }

    private void DrawCube(Vector3 position, int size, Color color)
    {
        // Calculate half of the size to create the cube from the center
        float halfSize = size / 2f;

        // Define the cube's vertices
        Vector3[] vertices = new Vector3[]
        {
            new Vector3(position.x - halfSize, position.y - halfSize, position.z - halfSize),
            new Vector3(position.x + halfSize, position.y - halfSize, position.z - halfSize),
            new Vector3(position.x + halfSize, position.y - halfSize, position.z + halfSize),
            new Vector3(position.x - halfSize, position.y - halfSize, position.z + halfSize),
            new Vector3(position.x - halfSize, position.y + halfSize, position.z - halfSize),
            new Vector3(position.x + halfSize, position.y + halfSize, position.z - halfSize),
            new Vector3(position.x + halfSize, position.y + halfSize, position.z + halfSize),
            new Vector3(position.x - halfSize, position.y + halfSize, position.z + halfSize)
        };

        // Draw the edges of the cube using Debug.DrawLine
        Debug.DrawLine(vertices[0], vertices[1], color);
        Debug.DrawLine(vertices[1], vertices[2], color);
        Debug.DrawLine(vertices[2], vertices[3], color);
        Debug.DrawLine(vertices[3], vertices[0], color);
        Debug.DrawLine(vertices[4], vertices[5], color);
        Debug.DrawLine(vertices[5], vertices[6], color);
        Debug.DrawLine(vertices[6], vertices[7], color);
        Debug.DrawLine(vertices[7], vertices[4], color);
        Debug.DrawLine(vertices[0], vertices[4], color);
        Debug.DrawLine(vertices[1], vertices[5], color);
        Debug.DrawLine(vertices[2], vertices[6], color);
        Debug.DrawLine(vertices[3], vertices[7], color);
    }

    public float GetVoxelSample(Vector3 worldposition)
    {
        int x = Mathf.FloorToInt(worldposition.x / voxelSize);
        int y = Mathf.FloorToInt(worldposition.y / voxelSize);
        int z = Mathf.FloorToInt(worldposition.z / voxelSize);


        // Clamp to the bounds of the voxel grid
        x = Mathf.Clamp(x, 0, xVoxels - 1);
        y = Mathf.Clamp(y, 0, yVoxels - 1);
        z = Mathf.Clamp(z, 0, zVoxels - 1);

        int voxelIndex = x + y * xVoxels + z * (xVoxels * yVoxels);
        return voxelData[voxelIndex].DistanceToSurface;
    }

    private void GenerateMesh(Vector2Int chunkpos, Vector2Int tilepos, int maxChunkWidth)
    {


        Vector2 compositedChunk = new Vector2(chunkpos.x, chunkpos.y) + tilepos;

        float dist = Vector2.Distance(compositedChunk, Vector2.zero);
        int resolutionMultiplier = Mathf.FloorToInt(dist / 256);


        // Debug.Log($"resolution: {voxelSize + resolutionMultiplier} position: {chunkpos.x}_{chunkpos.y}");

        MeshGenerator generator = GetComponent<MeshGenerator>();
        generator.GenerateMesh(voxelData.Length, xVoxels, yVoxels, (voxelSize + resolutionMultiplier), new Vector2(maxChunkWidth, height), new Vector2Int(chunkpos.x, chunkpos.y));
    }

    private void DestroyMesh(Vector2Int tilepos)
    {
        MeshGenerator generator = GetComponent<MeshGenerator>();

        generator.DestroyMesh(tiles[tilepos.x,tilepos.y].RemoveChunks());
    }
}
