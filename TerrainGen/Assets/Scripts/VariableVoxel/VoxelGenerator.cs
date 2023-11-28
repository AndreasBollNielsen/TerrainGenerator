using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;

using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UIElements;
using static Unity.Collections.AllocatorManager;
using static UnityEditor.PlayerSettings;
using static UnityEngine.EventSystems.EventTrigger;


public class VoxelGenerator : MonoBehaviour
{

    [SerializeField] int height;
    [SerializeField] int width;
    [Range(1, 16)][SerializeField] int maxXchunks;
    [Range(1, 16)][SerializeField] int maxZchunks;
    [Range(1, 7)][SerializeField] int maxXTiles;
    [Range(1, 7)][SerializeField] int maxZTiles;
    [Range(1, 64)][SerializeField] int minVoxelSize;
    [Range(-100, 100)][SerializeField] float displayThreshold;

    int xVoxels;
    int yVoxels;
    int zVoxels;
    VoxelData[] voxelData; // One-dimensional array to store voxel colors
    Color[] heightMap;
    // Color[] WaterMap;
    int heightmapWidth;

    public Vector3 offset;
    public Vector3 scale;

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

    List<float> cummulatedHeights = new List<float>();

    private void Start()
    {



        tiles = new TerrainTile[maxXTiles, maxZTiles];

        lastpos = player.position;
        lastTileX = Mathf.RoundToInt(player.position.x / 128); // Initial tile X
        lastTileZ = Mathf.RoundToInt(player.position.z / 128);


        // UpdateTilemap();
        initTiles();
        for (int xtile = 0; xtile < maxXTiles; xtile++)
        {
            for (int ytile = 0; ytile < maxZTiles; ytile++)
            {
                InitializeHeightmap(xtile, ytile);


                // Calculate max chunk width based on tile position
                //int maxChunkWidth = tiles[xtile, ytile].Width;
                //int currentChunkSizeX = tiles[xtile, ytile].NumChunks;
                //int currentChunkSizeZ = currentChunkSizeX;
                //// Debug.Log(currentChunkSizeX);

                //if (maxXchunks < currentChunkSizeX)
                //{
                //    currentChunkSizeX = maxXchunks;
                //}

                //if (maxZchunks < currentChunkSizeZ)
                //{
                //    currentChunkSizeZ = maxZchunks;
                //}


                //generating blocks
                var blocks = GenerateBlocks(xtile, ytile);
               // Debug.Log($"block count: {blocks.Count}");

                //remove portion of the blocks
               //  blocks.RemoveRange(4, ( blocks.Count-4));

                //iterating each chunk inside a tile
                 GenerateChunks(blocks);

                //generate chunks async
                //Vector2Int tilepos = new Vector2Int(xtile, ytile);
                //object[] parameter = new object[2] { blocks, tilepos };
                //StartCoroutine("GenerateChunksAsync", parameter);


                //generate new tile
                GenerateTile(xtile, ytile, blocks);
                

            }
        }



        cummulatedHeights.Sort((a, b) => -a.CompareTo(b));
        // Debug.Log("waterlevel: " + cummulatedHeights[0]);
        cummulatedHeights.Clear();


        // Invoke("testDynamicLOD", 5);


    }

    void GenerateTile(int xtile, int ytile, List<Block> blocks)
    {
        //generate new tile
        int tileSize = heightmapWidth - 1;
        TerrainTile tile = tiles[xtile, ytile];
        tile.AddBlocks(blocks);
        tile.AddChunks(GetComponent<MeshGenerator>().CopyChunks());
        GetComponent<MeshGenerator>().ClearList();
        GameObject terrainTile = new GameObject();
        tile.SetChunksParent(terrainTile);
        Vector3 tileposition = new Vector3(tileSize * xtile, 0, tileSize * ytile);
        terrainTile.transform.position = tileposition;
        terrainTile.name = $"Tile_{xtile}_{ytile}";
        terrainTile.transform.SetParent(gameObject.transform, false);
        tiles[xtile, ytile] = tile;

        var fps = FindObjectOfType<FPS_Controller>().enabled = true;
    }

    void GenerateChunks(List<Block> blocks)
    {
        // Debug.Log($"tilepos: {tilepos}");
        int blockId = 0;
        foreach (Block block in blocks)
        {
            //generate specific chunk level
            //if (block.Width > 128)
            //{
            //    continue;
            //}

            if (block.Loaded)
            {
                blockId++;
                continue;
            }


            int chunkWidth = block.Width;
            int x = block.X;
            int y = block.Y;
            int offsetX = x - (chunkWidth / 2);
            int offsetY = y - (chunkWidth / 2);
            int tileWidth = heightmapWidth - 1;
            block.Loaded = true;


            // Calculate the modulo to ensure that offsetX and offsetY are within the height map bounds
            offsetX = (offsetX + tileWidth) % tileWidth;
            offsetY = (offsetY + tileWidth) % tileWidth;


            //calculate voxelsize based on chunkWidth
            int voxel_Size = Mathf.Clamp((chunkWidth / width) * minVoxelSize, 1, 256);


            Profiler.BeginSample("generating voxels");
            //initialize voxelsize
            InitializeVoxelSize(chunkWidth + 1, (int)voxel_Size);




            // Generate the voxel structure
            GenerateVoxelStructure(offsetY, offsetX, voxel_Size);
            Profiler.EndSample();

            // generate mesh
            Vector2Int chunkPos = new Vector2Int(offsetY, offsetX);
            // Vector2Int tileposOffset = new Vector2Int(heightmapWidth * tilepos.x, heightmapWidth * tilepos.y);

            Profiler.BeginSample("Generating mesh");
            GenerateMesh(chunkPos, chunkWidth, voxel_Size, blockId);

            blockId++;
            Profiler.EndSample();

        }
    }

    IEnumerator GenerateChunksAsync(object[] myparams)
    {
        var blocks = (List<Block>)myparams[0];
        Vector2Int tilepos = (Vector2Int)myparams[1];
        int blockId = 0;
        foreach (Block block in blocks)
        {
            if (block.Loaded)
            {
                blockId++;
                continue;
            }


            int chunkWidth = block.Width;
            int x = block.X;
            int y = block.Y;
            int offsetX = x - (chunkWidth / 2);
            int offsetY = y - (chunkWidth / 2);
            int tileWidth = heightmapWidth - 1;
            block.Loaded = true;


            // Calculate the modulo to ensure that offsetX and offsetY are within the height map bounds
            offsetX = (offsetX + tileWidth) % tileWidth;
            offsetY = (offsetY + tileWidth) % tileWidth;


            //calculate voxelsize based on chunkWidth
            int voxel_Size = Mathf.Clamp((chunkWidth / width) * minVoxelSize, 1, 256);


          
            //initialize voxelsize
            InitializeVoxelSize(chunkWidth + 1, (int)voxel_Size);



            // Generate the voxel structure
            GenerateVoxelStructure(offsetY, offsetX, voxel_Size);

           

            // generate mesh
            Vector2Int chunkPos = new Vector2Int(offsetY, offsetX);
            // Vector2Int tileposOffset = new Vector2Int(heightmapWidth * tilepos.x, heightmapWidth * tilepos.y);
           
            GenerateMesh(chunkPos, chunkWidth, voxel_Size, blockId);
          
            blockId++;
            yield return new WaitForEndOfFrame();

        }

        GenerateTile(tilepos.x, tilepos.y, blocks);

    }

    List<Block> GenerateBlocks(int tileX, int tileY)
    {
        Profiler.BeginSample("generating blocks");
        int tileSize = 2048;
        List<Block> filledBlocks = new List<Block>();
        Vector2 playerpos = new Vector2(player.transform.position.x, player.transform.position.z);
        Vector2 tileCenter = new Vector2((tileX + 0.5f) * tileSize, (tileY + 0.5f) * tileSize);
        float dist = Vector2.Distance(playerpos, tileCenter);


        //divide if player is within tile
        if (dist < 3072)
        {
            filledBlocks = DividePosition(tileCenter, 2048, 2048, 2, playerpos, 1);

        }
        else
        {
            Block block = new Block(2048);
            block.X = Mathf.RoundToInt(tileCenter.x);
            block.Y = Mathf.RoundToInt(tileCenter.y);
            filledBlocks.Add(block);
        }



        filledBlocks = filledBlocks.OrderBy(block => block.Width).ToList();


        Profiler.EndSample();
        return filledBlocks;
    }

    List<Block> DividePosition(Vector2 center, float width, float height, int divisions, Vector2 playerpos, int level)
    {
        List<Block> tempblocks = new List<Block>();

        // Calculate half of the width and height
        float halfWidth = width / 2.0f;
        float halfHeight = height / 2.0f;

        float centerX = center.x;
        float centerY = center.y;
        int nextLevel = level + 1;
        bool ignoreplayer = false;


        //ignore distance calculation & fill remaining blocks
        if (width == 128)
        {
            ignoreplayer = true;
        }

        Vector2[] positions = new Vector2[]
{
    new Vector2(centerX -halfWidth   / 2,  centerY + halfHeight/ 2),   // Top Left
    new Vector2(centerX + halfWidth / 2,  centerY + halfHeight / 2),    // Top Right
    new Vector2(centerX -halfWidth / 2, centerY -halfHeight  / 2),  // Bottom Left
    new Vector2(centerX + halfWidth / 2, centerY -halfHeight  / 2)    // Bottom Right
};

        int index = 0;
        foreach (var position in positions)
        {
            Vector3 adjustedPosition = new Vector3(position.x, 0, position.y);
            index++;
            float dist = Vector2.Distance(playerpos, new Vector2(adjustedPosition.x, adjustedPosition.z));

            //divide if player is within block
            if (dist < width || ignoreplayer)
            {

                if (width >= 512)
                {

                    Vector2 pos = new Vector2(adjustedPosition.x, adjustedPosition.z);
                    var blocks = DividePosition(pos, halfWidth, halfHeight, divisions, playerpos, nextLevel);
                    tempblocks.AddRange(blocks);

                }
                else
                {
                    // Add a block for each adjusted position
                    tempblocks.Add(CreateBlock(adjustedPosition, width / divisions));
                }
            }
            else
            {
                // Add a block for each adjusted position
                tempblocks.Add(CreateBlock(adjustedPosition, width / divisions));

            }

        }

        // Debug.Log(tempblocks.Count);
        return tempblocks;

        // Helper method to create a block based on a position and width
        Block CreateBlock(Vector3 position, float width)
        {
            Block block = new Block((int)width);
            block.X = (int)position.x;
            block.Y = (int)position.z;
            return block;
        }
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR



        if (Application.isPlaying)
        {

            if (debug)
            {
                //needs refactor to use blocks
                //DisplayVoxels(xVoxels + 1, yVoxels + 1,);

            }

            if (DebugTiles)
            {


                for (int x = 0; x < tiles.GetLength(0); x++)
                {
                    for (int y = 0; y < tiles.GetLength(1); y++)
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

                for (int x = 0; x < tiles.GetLength(0); x++)
                {
                    for (int y = 0; y < tiles.GetLength(1); y++)
                    {


                        var blocks = tiles[x, y].GetBlocks();
                        // Debug.Log(blocks.Count);
                        ////draw blocks
                        for (int i = 0; i < blocks.Count; i++)
                        {
                            int xblock = blocks[i].X;
                            int yblock = blocks[i].Y;
                            int width = blocks[i].Width;
                            var chunk = blocks[i].GetChunk();
                            // Debug.Log($"xblock {xblock} yblock {yblock} width {width}");
                            Color col = new Color(0, 0, 0);

                            int pX = Mathf.FloorToInt(player.transform.position.x / width) * width;
                            int pY = Mathf.FloorToInt(player.transform.position.z / width) * width;

                            switch (blocks[i].Width)
                            {
                                case 64:
                                    col = Color.white;
                                    break;
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

                            if (blocks[i].Width < 64)
                            {
                                Gizmos.color = Color.white;
                            }


                            Vector3 cubepos = new Vector3(xblock, 1500, yblock);
                            Vector3 cubescale = new Vector3(width, 512, width) - scale;
                            Gizmos.DrawWireCube(cubepos, cubescale);
                            // Debug.Log($"blockpos: {cubepos} width: {width}");




                        }

                    }
                }
            }


        }
#endif
    }

    private void Update()
    {

        int currentBlockX = Mathf.RoundToInt(player.position.x / 128); // Current tile X
        int currentBlockZ = Mathf.RoundToInt(player.position.z / 128);

        // Check if the player has moved to a new tile
        if (currentBlockX != lastTileX || currentBlockZ != lastTileZ)
        {
            for (int x = 0; x < tiles.GetLength(0); x++)
            {
                for (int y = 0; y < tiles.GetLength(1); y++)
                {
                    //update blocks and recreate mesh if blocks has changed
                    if (UpdateBlocks(x, y))
                    {
                        UpdateMesh(new Vector2Int(x, y));
                    }
                }
            }

            lastTileX = currentBlockX;
            lastTileZ = currentBlockZ;
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


    private void UpdateMesh(Vector2Int tilepos)
    {
        Debug.Log("rebuilding " + tilepos);

        //update mesh chunk per tile
        InitializeHeightmap(tilepos.x, tilepos.y);
        GenerateChunks(tiles[tilepos.x, tilepos.y].GetBlocks());

        ////copy data to tile
        tiles[tilepos.x, tilepos.y].AddChunks(GetComponent<MeshGenerator>().CopyChunks());
        GetComponent<MeshGenerator>().ClearList();

        var tileobject = tiles[tilepos.x, tilepos.y].GetTileObject();
        if (tileobject != null)
        {
            // Debug.Log($"tileobject: {tileobject.name}");
            tiles[tilepos.x, tilepos.y].SetChunksParent(tileobject);

        }
        else
        {
            Debug.Log($"tileobject at: {tilepos} is null");
        }



        Debug.Log("finished updating mesh");

    }

    bool UpdateBlocks(int x, int y)
    {
        Debug.Log("updating blocks");

        if (x < 0 || y < 0 && x < maxXTiles - 1 && y < maxZTiles - 1)
        {
            return false;
        }

        //generate new blocks
        var newblocks = GenerateBlocks(x, y);
        var currentblocks = tiles[x, y].GetBlocks();



        //compare & insert new blocks
        int i = 0;
        bool blocksUpdated = false;
        while (i < currentblocks.Count && i < newblocks.Count)
        {
            var currentblock = currentblocks[i];
            var newblock = newblocks[i];
            // Debug.Log($"block loaded: {currentblock.Loaded}");
            if (newblock.X != currentblock.X || newblock.Y != currentblock.Y)
            {
                // Extract the portion of the list to be updated
                List<Block> blocksToRemove = currentblocks.GetRange(i, currentblocks.Count - i);

                // Remove elements from the current index to the end of the list
                currentblocks.RemoveRange(i, currentblocks.Count - i);

                // Insert remaining elements from the newblocks list
                currentblocks.InsertRange(i, newblocks.GetRange(i, newblocks.Count - i));

                // Call DestroyChunk on the old part of the list
                // Debug.Log($"number of chunks to remove: {blocksToRemove.Count} ");
                foreach (var block in blocksToRemove)
                {

                    // Assuming DestroyChunk is a method in the Block class
                    Destroy(block.DestroyChunk());
                }
                blocksToRemove.Clear();
                // Break out of the loop since we've handled the replacements
                blocksUpdated = true;
                break;
            }

            i++;
        }
        return blocksUpdated;
    }

    void InitializeVoxelSize(int maxWidth, int voxelSize)
    {

        // Calculate the number of voxels in each dimension
        xVoxels = Mathf.CeilToInt(maxWidth / voxelSize);
        yVoxels = Mathf.CeilToInt((float)height / voxelSize);
        zVoxels = xVoxels;

        // Initialize the voxelData array based on the calculated dimensions
        int totalVoxels = (xVoxels + 1) * (yVoxels + 1) * (zVoxels + 1);
        voxelData = new VoxelData[totalVoxels];

        // Debug.Log($"voxels initialized {totalVoxels} xvoxels: {xVoxels + 1} yvoxels {yVoxels + 1} zvoxels {zVoxels + 1} maxwidth: {maxWidth}");
    }

    void InitializeHeightmap(int x, int y)
    {
      //  string textureName = $"Textures\\heightmap_{x}_{y}";
        string textureName = $"RawHeightmaps\\Heightmap_{x}_{y}";
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

    private void GenerateVoxelStructure(int offsetX, int offsetZ, int voxelSize)
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

            if (index >= heightMap.Length)
            {

                Debug.LogError($"x {x} y {y} z {z}");
                Debug.Log($"offset: {offsetX}:{offsetZ}");
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

        // Debug.Log($"done filling data structure. highest level: {highestlevel}");
    }

    private void DisplayVoxels(int voxelwidth, int voxelheight, int voxelSize)
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

    //Deprecated
    //private void DrawCube(Vector3 position, int size, Color color)
    //{
    //    // Calculate half of the size to create the cube from the center
    //    float halfSize = size / 2f;

    //    // Define the cube's vertices
    //    Vector3[] vertices = new Vector3[]
    //    {
    //        new Vector3(position.x - halfSize, position.y - halfSize, position.z - halfSize),
    //        new Vector3(position.x + halfSize, position.y - halfSize, position.z - halfSize),
    //        new Vector3(position.x + halfSize, position.y - halfSize, position.z + halfSize),
    //        new Vector3(position.x - halfSize, position.y - halfSize, position.z + halfSize),
    //        new Vector3(position.x - halfSize, position.y + halfSize, position.z - halfSize),
    //        new Vector3(position.x + halfSize, position.y + halfSize, position.z - halfSize),
    //        new Vector3(position.x + halfSize, position.y + halfSize, position.z + halfSize),
    //        new Vector3(position.x - halfSize, position.y + halfSize, position.z + halfSize)
    //    };

    //    // Draw the edges of the cube using Debug.DrawLine
    //    Debug.DrawLine(vertices[0], vertices[1], color);
    //    Debug.DrawLine(vertices[1], vertices[2], color);
    //    Debug.DrawLine(vertices[2], vertices[3], color);
    //    Debug.DrawLine(vertices[3], vertices[0], color);
    //    Debug.DrawLine(vertices[4], vertices[5], color);
    //    Debug.DrawLine(vertices[5], vertices[6], color);
    //    Debug.DrawLine(vertices[6], vertices[7], color);
    //    Debug.DrawLine(vertices[7], vertices[4], color);
    //    Debug.DrawLine(vertices[0], vertices[4], color);
    //    Debug.DrawLine(vertices[1], vertices[5], color);
    //    Debug.DrawLine(vertices[2], vertices[6], color);
    //    Debug.DrawLine(vertices[3], vertices[7], color);
    //}

    public float GetVoxelSample(Vector3 worldposition, int voxelSize)
    {
        int x = Mathf.FloorToInt(worldposition.x / voxelSize);
        int y = Mathf.FloorToInt(worldposition.y / voxelSize);
        int z = Mathf.FloorToInt(worldposition.z / voxelSize);

        int voxelsWidth = xVoxels + 1;
        int voxelsHeight = yVoxels + 1;

        int voxelIndex = x + y * voxelsWidth + z * (voxelsWidth * voxelsHeight);
        if (voxelIndex >= voxelData.Length)
        {
            Debug.LogError($"worldpos: {worldposition} position: {x}:{y}:{z} voxelwidth: {voxelsWidth} voxelheight: {voxelsHeight} voxelSize: {voxelSize}");
            UnityEditor.EditorApplication.isPlaying = false;


            return -1;
        }
        return voxelData[voxelIndex].DistanceToSurface;
    }

    private void GenerateMesh(Vector2Int chunkpos, int maxChunkWidth, int voxelSize, int blockId)
    {
        int voxelLength = (xVoxels + 1) * (yVoxels + 1) * (zVoxels + 1);
        Vector2 chunkSize = new Vector2(maxChunkWidth, height);
        Vector2Int offset = new Vector2Int(chunkpos.x, chunkpos.y);

        MeshGenerator generator = GetComponent<MeshGenerator>();
        generator.GenerateMesh(voxelLength, xVoxels, yVoxels, voxelSize, chunkSize, offset, blockId);
    }


}
