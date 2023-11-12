using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TMPro;
using UnityEditor.Rendering;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;


public class VoxelGenerator : MonoBehaviour
{

    [SerializeField] int height;
    [SerializeField] int width;
    [Range(1, 16)][SerializeField] int maxXchunks;
    [Range(1, 16)][SerializeField] int maxZchunks;
    [Range(1, 7)][SerializeField] int maxXTiles;
    [Range(1, 7)][SerializeField] int maxZTiles;
    [Range(1, 64)][SerializeField] int voxelSize;
    [Range(-100, 100)][SerializeField] float displayThreshold;

    int xVoxels;
    int yVoxels;
    int zVoxels;
    VoxelData[] voxelData; // One-dimensional array to store voxel colors
    Color[] heightMap;
    // Color[] WaterMap;
    int heightmapWidth;
    List<Block> blocks;
    //int maxChunkWidth;


    [SerializeField] Color startColor = Color.blue;  // Color for low DistanceToSurface values
    [SerializeField] Color endColor = new Color(1, 0, 0, 0);     // Color for high DistanceToSurface values
    [SerializeField] bool debug;
    [SerializeField] bool DebugTiles;
    [SerializeField] bool DebugBlocks;
    TerrainTile[,] tiles;

    public Transform player;
    Vector3 lastpos;
    private int lastTileX;
    private int lastTileZ;
    int numChanges;
    List<float> cummulatedHeights = new List<float>();

    private void Start()
    {


        tiles = new TerrainTile[maxXTiles, maxZTiles];

        lastpos = player.position;
        lastTileX = Mathf.FloorToInt(lastpos.x / 2048); // Initial tile X
        lastTileZ = Mathf.FloorToInt(lastpos.z / 2048);


        UpdateTilemap();

        for (int xtile = 0; xtile < maxXTiles; xtile++)
        {
            for (int ytile = 0; ytile < maxZTiles; ytile++)
            {
                InitializeHeightmap(xtile, ytile);


                // Calculate max chunk width based on tile position
                int maxChunkWidth = tiles[xtile, ytile].Width;
                int currentChunkSizeX = tiles[xtile, ytile].NumChunks;
                int currentChunkSizeZ = currentChunkSizeX;
                Debug.Log(currentChunkSizeX);

                if (maxXchunks < currentChunkSizeX)
                {
                    currentChunkSizeX = maxXchunks;
                }

                if (maxZchunks < currentChunkSizeZ)
                {
                    currentChunkSizeZ = maxZchunks;
                }

                //iterating each chunk inside a tile
                GenerateChunks(currentChunkSizeX, currentChunkSizeZ, maxChunkWidth, new Vector2Int(xtile, ytile));

                blocks = GenerateBlocks(xtile, ytile);
                foreach (var block in blocks)
                {
                    //Debug.Log(block.Width);
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
                tiles[xtile, ytile].LODChanged += VoxelGenerator_LODChanged;
            }
        }




        cummulatedHeights.Sort((a, b) => -a.CompareTo(b));
        // Debug.Log("waterlevel: " + cummulatedHeights[0]);
        cummulatedHeights.Clear();

        var fps = FindObjectOfType<FPS_Controller>().enabled = true;
        // Invoke("testDynamicLOD", 5);

    }

    void GenerateChunks(int numChunkX, int numChunkZ, int chunkWidth, Vector2Int tilepos)
    {


        for (int x = 0; x < numChunkX; x++)
        {
            for (int z = 0; z < numChunkZ; z++)
            {
                //if(x < 3 &&  z < 3)
                //{
                //    continue;
                //}

                //initialize voxelsize
                InitializeVoxelSize(chunkWidth);

                // Generate the voxel structure
                GenerateVoxelStructure(chunkWidth * x, chunkWidth * z);

                // generate mesh
                Vector2Int chunkPos = new Vector2Int((chunkWidth/*+14*/) * x, (chunkWidth/*+14*/) * z);
                Vector2Int tileposOffset = new Vector2Int(heightmapWidth * tilepos.x, heightmapWidth * tilepos.y);
                GenerateMesh(chunkPos, tileposOffset, chunkWidth);

                // Debug.Log($"chunk: {x}:{z} Done!");
            }
        }
    }

    List<Block> GenerateBlocks(int tileX, int tileY)
    {


        TerrainTile tile = tiles[tileX, tileY];
        List<Block> filledBlocks = new List<Block>();
        int currentBlockWidth = 128;

        for (int radius = 128; radius < 2000; radius *= 2) // Generate 9 blocks with a width of 128 around the player
        {
            for (int angle = 0; angle < 360; angle++) // Iterate through all angles (0-359 degrees)
            {
                double radianAngle = angle * (Math.PI / 180); // Convert angle to radians
                                                              // float radius = radius * currentBlockWidth; // Distance from the player, increasing for each ring
                float newX = player.transform.position.x + radius * Mathf.Cos((float)radianAngle);
                float newY = player.transform.position.z + radius * Mathf.Sin((float)radianAngle);

                // Round to the nearest multiple of the current block width
                newX = Mathf.Round(newX / currentBlockWidth) * currentBlockWidth;
                newY = Mathf.Round(newY / currentBlockWidth) * currentBlockWidth;


                if (newX > 0 && newY > 0)
                {
                    Block block = new Block(currentBlockWidth);
                    block.X = (int)newX;
                    block.Y = (int)newY;

                    //check if block exists already
                    if (!filledBlocks.Any(x => x.X == block.X && x.Y == block.Y && x.Width == block.Width))
                    {
                        filledBlocks.Add(block);
                    Debug.Log($"X: {newX} Y: {newY} radius: {radius}");

                    }

                }
            }
            currentBlockWidth = radius;
        }
        var lastclock = filledBlocks[ filledBlocks.Count - 1];
        Debug.Log($" length {filledBlocks.Count} last index {lastclock.X}: {lastclock.Y}");

        



        return filledBlocks;
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR



        if (Application.isPlaying)
        {

            if (debug)
            {
                DisplayVoxels(xVoxels + 1, yVoxels + 1);

            }

            if (DebugTiles)
            {


                for (int x = 0; x < tiles.GetLength(0); x++)
                {
                    for (int y = 0; y < tiles.GetLength(0); y++)
                    {
                        Color col = new Color(0, 0, 0);

                        switch (tiles[x, y].Width)
                        {
                            case 128:
                                col = Color.cyan;
                                break;
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
                        Vector3 cubepos = new Vector3(x * 2048.2f, 1024, y * 2048.2f) + new Vector3(1024, 0, 1024);
                        Gizmos.DrawWireCube(cubepos, new Vector3(2048, 2000, 2048));

                        // if (tiles[x, y].Width == 0)
                        //  Debug.Log($"pos: {x}:{y} value: {tiles[x, y].Width}");
                    }
                }

            }

            if (DebugBlocks)
            {
                ////draw blocks
                for (int i = 0; i < blocks.Count; i++)
                {
                    int x = blocks[i].X;
                    int y = blocks[i].Y;
                    int width = blocks[i].Width;
                    //  Debug.Log($"x {x} y {y} width {width}");
                    Color col = new Color(0, 0, 0);

                    switch (blocks[i].Width)
                    {
                        case 128:
                            col = Color.cyan;
                            break;
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
                    Vector3 cubepos = new Vector3(x, 0, y);
                    Gizmos.DrawWireCube(cubepos, new Vector3(width, width, width));

                    // if (tiles[x, y].Width == 0)
                    //  Debug.Log($"pos: {x}:{y} value: {tiles[x, y].Width}");

                }
            }


        }
#endif
    }

    private void Update()
    {


        //if (debug)
        //{
        //    DisplayVoxels(xVoxels + 1, yVoxels + 1);

        //}

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
                    tiles[x, y] = new TerrainTile(x, y);

                }
            }
        }
    }

    void testDynamicLOD()
    {
        player.position = new Vector3(2500, 0, 1024);
        // VoxelGenerator_LODChanged(new Vector2Int(1, 0));

    }
    private void VoxelGenerator_LODChanged(Vector2Int tilepos)
    {
        //if(numChanges > 0)
        //{
        //    return;
        //}

        Debug.Log("rebuilding " + tilepos);
        //update mesh chunk per tile
        DestroyMesh(tilepos);

        //generate new mesh chunk
        // Calculate max chunk width based on tile position
        int maxChunkWidth = tiles[tilepos.x, tilepos.y].Width;
        int currentChunkSizeX = tiles[tilepos.x, tilepos.y].NumChunks;
        int currentChunkSizeZ = currentChunkSizeX;

        //  Debug.Log($"chunkWidth: {maxChunkWidth} currentX {currentChunkSizeX}");

        InitializeHeightmap(tilepos.x, tilepos.y);
        GenerateChunks(currentChunkSizeX, currentChunkSizeZ, maxChunkWidth, new Vector2Int(tilepos.x, tilepos.y));

        ////copy data to tile
        tiles[tilepos.x, tilepos.y].AddChunks(GetComponent<MeshGenerator>().CopyChunks());
        GetComponent<MeshGenerator>().ClearList();
        tiles[tilepos.x, tilepos.y].SetChunksParent(tiles[tilepos.x, tilepos.y].GetTileObject());

        Debug.Log("finished updating mesh");
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


        // Debug.Log($"pos: {centerX}:{centerY} tiles: {tiles.Length}");
        tiles[centerX, centerY].Width = width; // Initialize with the original value
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
            currentValue = Mathf.Clamp(currentValue, 128, 2048);
        }
    }

    void InitializeVoxelSize(int maxWidth)
    {

        // Calculate the number of voxels in each dimension
        xVoxels = maxWidth / voxelSize;
        yVoxels = Mathf.CeilToInt((float)height / voxelSize);
        zVoxels = xVoxels;

        // Initialize the voxelData array based on the calculated dimensions
        int totalVoxels = (xVoxels + 1) * (yVoxels + 1) * (zVoxels + 1);
        voxelData = new VoxelData[totalVoxels];

        Debug.Log($"voxels initialized {totalVoxels} xvoxels: {xVoxels + 1} yvoxels {yVoxels + 1} zvoxels {zVoxels + 1} maxwidth: {maxWidth}");
    }

    void InitializeHeightmap(int x, int y)
    {
        string textureName = $"Textures\\heightmap_{x}_{y}";
        string WatertextureName = $"Textures\\WaterMask_{x}_{y}";
        // Debug.Log(textureName);
        Texture2D heightmapTexture = Resources.Load<Texture2D>(textureName);
        Texture2D waterMaskTexture = Resources.Load<Texture2D>(WatertextureName);

        if (heightmapTexture != null)
        {
            heightMap = heightmapTexture.GetPixels();
            heightmapWidth = heightmapTexture.width;
            //  Debug.Log(heightmapWidth);

            //set watermask
            // WaterMap = waterMaskTexture.GetPixels();
        }
        else
        {
            Debug.LogError($"Heightmap texture '{textureName}' not found in Resources.");
        }
    }

    private void GenerateVoxelStructure(int offsetX, int offsetZ)
    {
        int voxelWidth = xVoxels + 1;
        int voxelHeight = yVoxels + 1;
        int highestlevel = 0;

        for (int _index = 0; _index < voxelData.Length; _index++)
        {

            //get x & z coords
            int z = _index % voxelWidth;
            int y = (_index / voxelWidth) % voxelHeight;
            int x = _index / (voxelWidth * voxelHeight);

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

            if (index > heightMap.Length)
            {

                Debug.Log($"x {x} y {y} z {z}");
                return;
            }

            Color sampledColor = heightMap[index];
            // Color sampleWater = WaterMap[index];
            float sampledHeight = sampledColor.r;
            // float water = sampleWater.r * 850;
            float scaledHeight = sampledHeight * 2000;

            //if (water > 0)
            //    cummulatedHeights.Add(scaledHeight);

            voxelData[_index].DistanceToSurface = (y * voxelSize) - scaledHeight;

            if (y > highestlevel)
            {
                highestlevel = y;
            }

            //if(y > 50)
            //{
            //    Debug.Log(y);
            //}
        }

        Debug.Log($"done filling data structure. highest level: {highestlevel}");
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
                // DrawCube(voxelPosition, voxelSize, voxelcol);
                Gizmos.color = voxelcol;
            }
            Gizmos.DrawWireCube(voxelPosition, new Vector3(voxelSize, voxelSize, voxelSize));
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

        int voxelsWidth = xVoxels + 1;
        int voxelsHeight = yVoxels + 1;


        //// Clamp to the bounds of the voxel grid
        //x = Mathf.Clamp(x, 0, xVoxels - 1);
        //y = Mathf.Clamp(y, 0, yVoxels - 1);
        //z = Mathf.Clamp(z, 0, zVoxels - 1);

        int voxelIndex = x + y * voxelsWidth + z * (voxelsWidth * voxelsHeight);
        if (voxelIndex >= voxelData.Length)
        {
            Debug.LogError($"index: {voxelIndex} length: {voxelData.Length} position: {x}:{y}:{z} voxelwidth: {voxelsWidth} voxelheight: {voxelsHeight}");
            return -1;
        }
        return voxelData[voxelIndex].DistanceToSurface;
    }

    private void GenerateMesh(Vector2Int chunkpos, Vector2Int tilepos, int maxChunkWidth)
    {


        Vector2 compositedChunk = new Vector2(chunkpos.x, chunkpos.y) + tilepos;

        float dist = Vector2.Distance(compositedChunk, player.position);
        int resolutionMultiplier = Mathf.FloorToInt(dist / 2048);


        // Debug.Log($"resolution: {voxelSize + resolutionMultiplier} position: {chunkpos.x}_{chunkpos.y}");
        int voxelLength = xVoxels * yVoxels * zVoxels;

        Debug.Log(resolutionMultiplier);
        MeshGenerator generator = GetComponent<MeshGenerator>();
        generator.GenerateMesh(voxelLength, xVoxels, yVoxels, (voxelSize + resolutionMultiplier), new Vector2(maxChunkWidth, height), new Vector2Int(chunkpos.x, chunkpos.y));
    }

    private void DestroyMesh(Vector2Int tilepos)
    {
        MeshGenerator generator = GetComponent<MeshGenerator>();

        generator.DestroyMesh(tiles[tilepos.x, tilepos.y].RemoveChunks());
    }
}
