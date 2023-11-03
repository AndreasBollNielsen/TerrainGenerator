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
    [SerializeField] Vector3 Origin;

    [SerializeField] Color startColor = Color.blue;  // Color for low DistanceToSurface values
    [SerializeField] Color endColor = new Color(1, 0, 0, 0);     // Color for high DistanceToSurface values
    [SerializeField] bool debug;
    TerrainTile[,] tiles;

    // List<TerrainTile> tiles = new List<TerrainTile>();
    // Dictionary<Vector2, TerrainTile> tiles = new Dictionary<Vector2, TerrainTile>();


    private void Start()
    {
        currentVoxelSize = voxelSize;
        //  maxChunkWidth = width;

        currentChunkSizeX = maxXchunks;
        currentChunkSizeZ = maxZchunks;
        InitializeTileMap();

        for (int xtile = 0; xtile < maxXTiles; xtile++)
        {


            for (int ytile = 0; ytile < maxZTiles; ytile++)
            {
                InitializeHeightmap(xtile, ytile);

                GameObject go = new GameObject();
                go.AddComponent<TextMeshProUGUI>().text = tiles[xtile, ytile].Width.ToString();


                ////calculate max chunkwidth
                //maxChunkWidth = Mathf.FloorToInt(width * Mathf.Pow(2, ytile + xtile ));
                //maxChunkWidth = Mathf.Clamp(maxChunkWidth, 256, 2048);
                //Debug.Log($" tiles: {xtile}:{ytile} new width: {maxChunkWidth}");


                // Calculate max chunk width based on tile position
                maxChunkWidth = width * (ytile + 1);
                maxChunkWidth = Mathf.Clamp(maxChunkWidth, 256, 2048);
                Debug.Log(maxChunkWidth);
                // Iterate over chunks within the tile
                int currentChunkSizeX = maxXchunks;
                int currentChunkSizeZ = maxZchunks;
                if (maxChunkWidth > width)
                {
                    ////calculate max chunks size
                    currentChunkSizeX /= 2;
                    currentChunkSizeX = Mathf.Max(1, currentChunkSizeX);
                    currentChunkSizeZ /= 2;
                    currentChunkSizeZ = Mathf.Max(1, currentChunkSizeZ);


                }

                //check neighbor
                //Vector3 tilepos = new Vector3(ytile * heightmapWidth, (xtile - 1) * heightmapWidth);
                //if (xtile > 0)
                //{
                //    TerrainTile currentTile = tiles[tilepos];
                //    if (currentTile != null)
                //    {
                //        Debug.Log($"tilepos: {tilepos} currentTile: {currentTile.With} maxChunkWidth: {maxChunkWidth}");
                //        if (currentTile.With < maxChunkWidth)
                //        {
                //            maxChunkWidth = currentTile.With * 2;
                //        }
                //        else
                //        {
                //            maxChunkWidth = currentTile.With;
                //        }
                //    }

                //}


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
                // tile.With = maxChunkWidth;
                Vector3 tileposition = new Vector3(heightmapWidth * xtile, 0, heightmapWidth * ytile);
                terrainTile.transform.position = tileposition;
                terrainTile.name = $"Tile_{xtile}_{ytile}";
                terrainTile.transform.SetParent(gameObject.transform, false);

                tiles[xtile, ytile] = tile;



                ////calculate max chunks size
                //currentChunkSizeX /= 2;
                //currentChunkSizeX = Mathf.Max(1, currentChunkSizeX);
                //currentChunkSizeZ /= 2;
                //currentChunkSizeZ = Mathf.Max(1, currentChunkSizeZ);


                ////calc dist from tile 0
                //Vector3 origin = tiles[0].GetTileObject().transform.position;
                //float dist = Vector3.Distance(origin, terrainTile.transform.position);
                //int tilepos = Mathf.FloorToInt(dist / heightmapWidth);



                //Debug.Log(Mathf.FloorToInt(width * Mathf.Pow(2, tilepos + 1)));

            }
        }








    }

    private void Update()
    {
        // regenerate voxels on voxelsize change
        if (currentVoxelSize != voxelSize)
        {
            InitializeHeightmap(0, 0);
            InitializeVoxelSize(256);
            GenerateVoxelStructure(0, 0);
            DestroyMesh();
            GenerateMesh(Vector2Int.zero, Vector2Int.zero, 256);

            currentVoxelSize = voxelSize;
        }

        if (debug)
            DisplayVoxels(xVoxels, yVoxels);
    }

    void initTiles()
    {
        for (int x = 0; x < tiles.GetLength(0); x++)
        {
            for (int y = 0; y < tiles.GetLength(1); y++)
            {
                tiles[x, y] = new TerrainTile();
            }
        }
    }

    void InitializeTileMap()
    {
        tiles = new TerrainTile[maxXTiles, maxZTiles];
        initTiles();

        int width = tiles.GetLength(0); // Assuming a rectangular array
        int height = tiles.GetLength(1);
        int centerX = (int)Origin.x;
        int centerY = (int)Origin.z;



        tiles[centerX, centerY].Width = 256; // Initialize with the original value
        int lastValue = tiles[centerX, centerY].Width;
        int currentValue = lastValue *2;

        for (int radius = 1; radius <= 10; radius++)
        {
            for (int angle = 0; angle < 360; angle++) // Iterate through all angles (0-359 degrees)
            {
                double radianAngle = angle * (Math.PI / 180); // Convert angle to radians
                int newX = centerX + (int)(radius * Math.Cos(radianAngle));
                int newY = centerY + (int)(radius * Math.Sin(radianAngle));

                // Check if the calculated point is within the array bounds
                if (newX >= 0 && newX < width && newY >= 0 && newY < height)
                {

                    // Update the array with the current value
                    tiles[newX, newY].Width = currentValue;

                    Debug.Log($"{newX}:{newY} value: {currentValue}");

                    // Update lastValue for the next iteration
                    lastValue = currentValue;
                }
            }
             currentValue = lastValue * 2; // Calculate the current value
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


        //  Debug.Log($"resolution: {voxelSize + resolutionMultiplier} position: {chunkpos.x}_{chunkpos.y}");

        MeshGenerator generator = GetComponent<MeshGenerator>();
        generator.GenerateMesh(voxelData.Length, xVoxels, yVoxels, (voxelSize + resolutionMultiplier), new Vector2(maxChunkWidth, height), new Vector2Int(chunkpos.x, chunkpos.y));
    }

    private void DestroyMesh()
    {
        MeshGenerator generator = GetComponent<MeshGenerator>();
        generator.DestroyMesh();
    }
}
