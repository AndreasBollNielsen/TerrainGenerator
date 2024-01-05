using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Profiling;
using static Block;
using static Unity.Collections.AllocatorManager;
using static UnityEditor.Progress;
using static WorldData;
//using UnityEngine.UIElements;
//using static Unity.Collections.AllocatorManager;
//using static UnityEditor.PlayerSettings;
//using static UnityEngine.EventSystems.EventTrigger;


public class VoxelGenerator : MonoBehaviour
{

    [SerializeField] int height;
    [SerializeField] int minChunkWidth;
    // [Range(1, 16)][SerializeField] int maxXchunks;
    // [Range(1, 16)][SerializeField] int maxZchunks;
    [Range(1, 7)][SerializeField] int maxXTiles;
    [Range(1, 7)][SerializeField] int maxZTiles;
    [Range(1, 64)][SerializeField] int minVoxelSize;
    [Range(-100, 100)][SerializeField] float displayThreshold;

    int xVoxels;
    int yVoxels;
    int zVoxels;
    VoxelData_v2[] CurrentvoxelData; // One-dimensional array to store voxel colors
    Color[] heightMap;
    // Color[] WaterMap;


    // public Vector3 offset;
    //  public Vector3 scale;

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

    List<NativeArray<float>> heightmaps = new List<NativeArray<float>>();
    //List<List<float>> ManagedHeightmaps = new List<List<float>>();
    Dictionary<Vector2Int, List<float>> ManagedHeightmaps = new Dictionary<Vector2Int, List<float>>();
    int currentVoxelWidth;
    int currentVoxelHeight;
    Vector3 currentVoxelPos;
    List<Block> oldBlocks = new List<Block>();


    private void Start()
    {
        StartCoroutine(cacheHeightMaps());

        WorldData.TriangleTable_1D = WorldData.Convert2DTo1D(WorldData.TriangleTable);
        Constants.minChunkWidth = minChunkWidth;
        Constants.minVoxelSize = minVoxelSize;
        Constants.height = height;

        tiles = new TerrainTile[maxXTiles, maxZTiles];

        lastpos = player.position;
        lastTileX = Mathf.RoundToInt(player.position.x / minChunkWidth); // Initial tile X
        lastTileZ = Mathf.RoundToInt(player.position.z / minChunkWidth);

        initTiles();










    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR



        if (Application.isPlaying)
        {

            if (debug)
            {
                //needs refactor to use blocks
                DisplayVoxels(currentVoxelWidth, currentVoxelHeight, currentVoxelPos);

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
                            case 64:
                                col = Color.blue;
                                break;
                            case 128:
                                col = new Color(128, 0, 128);
                                break;
                            case 256:
                                col = Color.green;
                                break;
                            case 512:
                                col = Color.yellow;
                                break;
                            case 1024:
                                col = new Color(255, 165, 0);
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
                                case 32:
                                    col = Color.white;
                                    break;
                                case 64:
                                    col = Color.blue;
                                    break;
                                case 128:
                                    col = new Color(128, 0, 128);
                                    break;
                                case 256:
                                    col = Color.green;
                                    break;
                                case 512:
                                    col = Color.yellow;
                                    break;
                                case 1024:
                                    col = new Color(255, 165, 0);
                                    break;
                                case 2048:
                                    col = Color.red;
                                    break;
                                default:
                                    break;
                            }
                            Gizmos.color = col;

                            //if (blocks[i].Width < 64)
                            //{
                            //    Gizmos.color = Color.white;
                            //}


                            Vector3 cubepos = new Vector3(xblock, 1500, yblock);
                            Vector3 cubescale = new Vector3(width, 512, width);
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

        int currentBlockX = Mathf.RoundToInt(player.position.x / minChunkWidth);
        int currentBlockZ = Mathf.RoundToInt(player.position.z / minChunkWidth);

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
                        //   UpdateMesh(new Vector2Int(x, y));
                        StartCoroutine(UpdateMeshAsync(new Vector2Int(x, y)));
                    }
                }
            }

            lastTileX = currentBlockX;
            lastTileZ = currentBlockZ;
        }

        //quit application if escape is pushed
        if (Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();
        }

    }
    IEnumerator InitializeWorld()
    {
        Debug.Log("generating chunks");
        //initialize terrain data
        WorldData.TerrainData terrainData = new WorldData.TerrainData
        {
            CornerTable = new NativeArray<Vector3Int>(WorldData.CornerTable, allocator: Allocator.Persistent),
            EdgeIndexes = new NativeArray<int>(WorldData.EdgeIndexes_1D, Allocator.Persistent),
            TriangleTable = new NativeArray<int>(WorldData.TriangleTable_1D, Allocator.Persistent),
        };

        int heightmapCounter = 0;
        int counter = maxXTiles * maxZTiles;
        for (int xtile = 0; xtile < maxXTiles; xtile++)
        {
            for (int ytile = 0; ytile < maxZTiles; ytile++)
            {





                // Debug.Log($"generating tile: {xtile}:{ytile}");

                //generating blocks
                Profiler.BeginSample("test_generating blocks");
                var blocks = GenerateBlocks(xtile, ytile);
                Profiler.EndSample();
                int numblocks = blocks.Count;
                // numblocks = 2;
                //object[] parameters = new object[3];
                //parameters[0] = heightmaps[heightmapCounter];
                //parameters[1] = terrainData;
                //parameters[2] = new JobHandle();

                NativeArray<JobHandle> jobs = new NativeArray<JobHandle>(numblocks, allocator: Allocator.Persistent);
                for (int i = 0; i < numblocks; i++)
                {
                    blocks[i].GenerateMesh(heightmaps[heightmapCounter], terrainData);
                    // blocks[i].testjob(parameters);
                    blocks[i].SetBlockId(i);
                    jobs[i] = blocks[i].GetJob();
                }
                JobHandle completeHandle = JobHandle.CombineDependencies(jobs);
                JobHandle.ScheduleBatchedJobs();





                //dispose native data
                yield return new WaitForEndOfFrame();
                completeHandle.Complete();
                Profiler.BeginSample("test_generatetile");
                if (completeHandle.IsCompleted)
                {
                    //CurrentheightMap.Dispose();

                    jobs.Dispose();

                    //generate new tile
                    GenerateTile(xtile, ytile, blocks);

                    counter -= 1;
                    //  UnityEditor.EditorApplication.isPaused = true;
                }
                Profiler.EndSample();
                //set size of current voxel structure - only for debugging
                //CurrentvoxelData = blocks[0].GetVoxelData();
                //currentVoxelHeight = Constants.height + 1;
                //currentVoxelWidth = blocks[0].Width + 1;
                //currentVoxelPos = blocks[0].GetPosition();
                //TestVoxelGeneration();
                //currentVoxelHeight = 128 + 1;
                //currentVoxelWidth = 128 + 1;






                //iterating each chunk inside a tile
                //   GenerateChunks(blocks, heightmap);






                heightmapCounter++;
            }
        }


        if (counter == 0)
        {
            Profiler.BeginSample("test_dispose");
            terrainData.CornerTable.Dispose();
            terrainData.EdgeIndexes.Dispose();
            terrainData.TriangleTable.Dispose();
            Profiler.EndSample();
            yield return new WaitForEndOfFrame();
            StartCoroutine(AddHeightMap());



            Debug.Log($"disposed: {counter}");
        }
    }

    IEnumerator cacheHeightMaps()
    {
        int maxpercent = maxXTiles * maxZTiles;
        int percentage = 0;


        Color[] heightmap = new Color[2049 * 2049];
        for (int x = 0; x < maxXTiles; x++)
        {
            for (int y = 0; y < maxZTiles; y++)
            {
                heightmap = InitializeHeightmap(x, y);
                NativeArray<float> currentMap = new NativeArray<float>(heightmap.Length, allocator: Allocator.Persistent);
                for (int i = 0; i < heightmap.Length; i++)
                {
                    Color col = heightmap[i];
                    currentMap[i] = col.r;

                }

                heightmaps.Add(currentMap);
                percentage++;
                float progressPercentage = (float)percentage / maxpercent * 100f;
                Debug.Log($"caching heightmaps {progressPercentage}:percent");
                yield return new WaitForEndOfFrame();

            }
        }
        Resources.UnloadUnusedAssets();

        int numElements = (heightmap.Length * (maxXTiles * maxZTiles));
        Constants.CalcMemory<float>(numElements);
        Debug.Log("Done Caching. total bytes: ");
        // yield return new WaitForSeconds(3);
        StartCoroutine(InitializeWorld());
    }

    IEnumerator AddHeightMap()
    {
        //  Profiler.BeginSample("test_adding heightmap");
        int iterator = 0;
        for (int x = 0; x < maxXTiles; x++)
        {
            for (int z = 0; z < maxZTiles; z++)
            {
                ManagedHeightmaps.Add(new Vector2Int(x, z), heightmaps[iterator].ToList());
                heightmaps[iterator].Dispose();
                iterator++;
                yield return new WaitForEndOfFrame();
            }
        }
        heightmaps.Clear();
        //  Profiler.EndSample();
    }

    void GenerateTile(int xtile, int ytile, List<Block> blocks)
    {
        //generate new tile
        int tileSize = Constants.heightmapWidth - 1;
        TerrainTile tile = tiles[xtile, ytile];
        tile.AddBlocks(blocks);
        //  tile.AddChunks(GetComponent<MeshGenerator>().CopyChunks());
        //  GetComponent<MeshGenerator>().ClearList();
        GameObject terrainTile = new GameObject();
        tile.SetChunksParent(terrainTile);
        Vector3 tileposition = new Vector3(tileSize * xtile, 0, tileSize * ytile);
        terrainTile.transform.position = tileposition;
        terrainTile.name = $"Tile_{xtile}_{ytile}";
        terrainTile.transform.SetParent(gameObject.transform, false);
        tiles[xtile, ytile] = tile;

        var fps = FindObjectOfType<FPS_Controller>().enabled = true;
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
            filledBlocks = DividePosition(tileCenter, 2048, 2048, 2, playerpos);

        }
        else
        {
            Block block = new Block(2048, new Vector2Int(tileX, tileY), Mathf.RoundToInt(tileCenter.x), Mathf.RoundToInt(tileCenter.y));
            //block.X = Mathf.RoundToInt(tileCenter.x);
            //block.Y = Mathf.RoundToInt(tileCenter.y);
            filledBlocks.Add(block);
        }

        //int num = filledBlocks.Count(x => x.Width == 128);
        // Debug.Log(num);
        filledBlocks = filledBlocks.OrderBy(block => block.Width).ToList();


        Profiler.EndSample();
        return filledBlocks;
    }

    List<Block> DividePosition(Vector2 center, float width, float height, int divisions, Vector2 playerpos)
    {
        List<Block> tempblocks = new List<Block>();

        // Calculate half of the width and height
        float halfWidth = width / 2.0f;
        float halfHeight = height / 2.0f;

        float centerX = center.x;
        float centerY = center.y;
        // int nextLevel = level + 1;
        bool ignoreplayer = false;

        int tileX = Mathf.RoundToInt((center.x / 2048) - 0.5f);
        int tileY = Mathf.RoundToInt((center.y / 2048) - 0.5f);



        //ignore distance calculation & fill remaining blocks
        if (width == minChunkWidth)
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
                // adjust to set rule for lowest chunk
                if (width > minChunkWidth)
                {

                    Vector2 pos = new Vector2(adjustedPosition.x, adjustedPosition.z);
                    var blocks = DividePosition(pos, halfWidth, halfHeight, divisions, playerpos);
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
            Block block = new Block((int)width, new Vector2Int(tileX, tileY), (int)position.x, (int)position.z);
            //block.X = (int)position.x;
            //block.Y = (int)position.z;
            return block;
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

    IEnumerator UpdateMeshAsync(Vector2Int tilepos)
    {

        Debug.Log("rebuilding " + tilepos);
        WorldData.TerrainData terrainData = new WorldData.TerrainData
        {
            CornerTable = new NativeArray<Vector3Int>(WorldData.CornerTable, allocator: Allocator.Persistent),
            EdgeIndexes = new NativeArray<int>(WorldData.EdgeIndexes_1D, Allocator.Persistent),
            TriangleTable = new NativeArray<int>(WorldData.TriangleTable_1D, Allocator.Persistent),
        };



        NativeArray<float> currentMap = new NativeArray<float>(ManagedHeightmaps[tilepos].ToArray(), allocator: Allocator.Persistent);


        var blocks = tiles[tilepos.x, tilepos.y].GetBlocks();
        int numLoaded = blocks.Count(x => x.Loaded == true);
        int numpriority = blocks.Count(x => x.Width < 64 && x.Loaded == false);
        int numLowPriority = blocks.Count(x => x.Width >= 64 && x.Loaded == false);
        //  numpriority = numLoaded;
        object[] parameters = new object[2];
        parameters[0] = currentMap;
        parameters[1] = terrainData;

        ScheduledBlock hightPriorityBlock = new ScheduledBlock(numpriority);
        ScheduledBlock LowPriorityBlock = new ScheduledBlock(numLowPriority);
        Debug.Log($"loaded blocks {numLoaded} total blocks {blocks.Count}");
        // Profiler.BeginSample("test_priorityjob");
        int priorityCounter = 0;
        int lowPriorityCounter = 0;

        for (int i = 0; i < blocks.Count; i++)
        {
            if (!blocks[i].Loaded)
            {
                //start high priority jobs
                if (blocks[i].Width < 64)
                {
                    // StartCoroutine(blocks[i].testjob(parameters));
                    blocks[i].GenerateMesh(currentMap, terrainData);

                    hightPriorityBlock.blockIds.Add(i);
                    hightPriorityBlock.jobHandles[priorityCounter] = blocks[i].GetJob();

                    //  yield return hightPriorityBlock.jobHandles[priorityCounter];
                    priorityCounter++;
                    // Debug.Log($"priority: {priorityCounter}");
                }

            }
            blocks[i].SetBlockId(i);


        }

        JobHandle HighPriorityHandle = JobHandle.CombineDependencies(hightPriorityBlock.jobHandles);
        yield return HighPriorityHandle;
        HighPriorityHandle.Complete();

        for (int i = 0; i < blocks.Count; i++)
        {
            if (!blocks[i].Loaded)
            {

                if (blocks[i].Width >= 64)
                {
                    // StartCoroutine(blocks[i].testjob(parameters));
                    blocks[i].GenerateMesh(currentMap, terrainData);
                    LowPriorityBlock.blockIds.Add(i);
                    LowPriorityBlock.jobHandles[lowPriorityCounter] = blocks[i].GetJob();

                    yield return LowPriorityBlock.jobHandles[lowPriorityCounter];
                    lowPriorityCounter++;
                }
            }
            blocks[i].SetBlockId(i);


        }
        JobHandle LowPriorityHandle = JobHandle.CombineDependencies(LowPriorityBlock.jobHandles);
        LowPriorityHandle.Complete();
        JobHandle combinedJobs = JobHandle.CombineDependencies(HighPriorityHandle, LowPriorityHandle);
        //  JobHandle.ScheduleBatchedJobs();



        yield return combinedJobs;
        combinedJobs.Complete();

        //  Profiler.EndSample();
        //   UnityEditor.EditorApplication.isPaused = true;
        //for (int i = 0; i < hightPriorityBlock.blockIds.Count; i++)
        //{
        //    int blockId = hightPriorityBlock.blockIds[i];
        //    if (!blocks[blockId].Loaded)
        //    {
        //        blocks[blockId].SetMesh();
        //        blocks[blockId].Loaded = true;
        //    }
        //}


        //   yield return LowPriorityHandle;
        //  LowPriorityHandle.Complete();

        foreach (var block in oldBlocks)
        {
            if (block != null)
            {
                //block.DestroyChunk().SetActive(false);
                 Destroy(block.DestroyChunk());
            }
        }
        oldBlocks.Clear();

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

        hightPriorityBlock.jobHandles.Dispose();
        hightPriorityBlock.blockIds.Clear();
        LowPriorityBlock.jobHandles.Dispose();
        LowPriorityBlock.blockIds.Clear();
        currentMap.Dispose();
        terrainData.CornerTable.Dispose();
        terrainData.EdgeIndexes.Dispose();
        terrainData.TriangleTable.Dispose();
        // UnityEditor.EditorApplication.isPaused = true;
        //  Debug.Log("finished updating mesh");




    }
    private void UpdateMesh(Vector2Int tilepos)
    {


        Debug.Log("rebuilding " + tilepos);
        WorldData.TerrainData terrainData = new WorldData.TerrainData
        {
            CornerTable = new NativeArray<Vector3Int>(WorldData.CornerTable, allocator: Allocator.Persistent),
            EdgeIndexes = new NativeArray<int>(WorldData.EdgeIndexes_1D, Allocator.Persistent),
            TriangleTable = new NativeArray<int>(WorldData.TriangleTable_1D, Allocator.Persistent),
        };



        //update mesh chunk per tile
        Profiler.BeginSample("test_converting Heightmap");
        NativeArray<float> currentMap = new NativeArray<float>(ManagedHeightmaps[tilepos].ToArray(), allocator: Allocator.Persistent);

        Profiler.EndSample();

        var blocks = tiles[tilepos.x, tilepos.y].GetBlocks();
        int numLoaded = blocks.Count(x => x.Loaded == false);
        NativeArray<JobHandle> jobs = new NativeArray<JobHandle>(numLoaded, allocator: Allocator.Persistent);

        int jobCounter = 0;
        for (int i = 0; i < blocks.Count; i++)
        {
            if (!blocks[i].Loaded)
            {
                blocks[i].GenerateMesh(currentMap, terrainData);
                jobs[jobCounter] = blocks[i].GetJob();
                jobCounter++;
            }
            blocks[i].SetBlockId(i);


        }
        JobHandle completeHandle = JobHandle.CombineDependencies(jobs);
        // updatehandle = completeHandle;
        JobHandle.ScheduleBatchedJobs();
        // Ensure that all jobs are completed before moving on
        completeHandle.Complete();



        for (int i = 0; i < blocks.Count; i++)
        {
            if (!blocks[i].Loaded)
            {
                blocks[i].SetMesh();
                blocks[i].Loaded = true;
            }
        }



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

        jobs.Dispose();
        currentMap.Dispose();
        terrainData.CornerTable.Dispose();
        terrainData.EdgeIndexes.Dispose();
        terrainData.TriangleTable.Dispose();
        // UnityEditor.EditorApplication.isPaused = true;
        Debug.Log("finished updating mesh");

    }

    bool UpdateBlocks(int x, int y)
    {
        Profiler.BeginSample("updateBlocks");

        if (x < 0 || y < 0 && x < maxXTiles - 1 && y < maxZTiles - 1)
        {
            return false;
        }

        //generate new blocks
        var newblocks = GenerateBlocks(x, y);
        newblocks = newblocks.OrderBy(block => block.Width).ThenByDescending(block => block.X).ThenByDescending(block => block.Y).ToList();

        var currentblocks = tiles[x, y].GetBlocks();
        currentblocks = currentblocks.OrderBy(block => block.Width).ThenByDescending(block => block.X).ThenByDescending(block => block.Y).ToList();
      //  List<int> blocklist = new List<int>();
      //  List<int> removableList = new List<int>();
        //  Debug.Log($"currentblocks {currentblocks.Count} newblocks {newblocks.Count}");
        //compare & insert new blocks
      //  int i = 0;
       // int numblocks = 0;
        bool blocksUpdated = false;
      //  int numequal = 0;
        // Debug.Log($" current blocks {currentblocks.Count} new blocks {newblocks.Count}");
        for (int j = 0; j < newblocks.Count; j++)
        {
            var newblock = newblocks[j];
            bool blockfound = false;

            //searching through old blocks for similar blocks
            for (int n = 0; n < currentblocks.Count; n++)
            {
                var curblock = currentblocks[n];

                //fist check width
                if (curblock.Width == newblock.Width)
                {
                    Vector2 currentpos = new Vector2(curblock.X, curblock.Y);
                    Vector2 newpos = new Vector2(newblock.X, newblock.Y);

                    if (curblock.X == newblock.X && curblock.Y == newblock.Y)
                    {
                       // numequal++;
                      //  blocklist.Add(n);
                        newblocks[j] = curblock;
                        blockfound = true;
                        currentblocks.RemoveAt(n);
                        // Debug.Log($"index: {j} old blocks: {currentpos} width: {curblock.Width} new block: {newpos}width: {newblock.Width}");
                        break;
                    }

                }



            }



        }

        //reorder new blocks
       // newblocks = newblocks.OrderBy(block => block.Width).ThenByDescending(block => block.X).ThenByDescending(block => block.Y).ToList();

      //  int oldblocks = newblocks.Count(x => x.Loaded == true);
        //int newblok = newblocks.Count(x => x.Loaded == false);
        //var nodublicates = blocklist.Distinct().ToList();
        //int remove = currentblocks.Count;

        int numtoremove = 0;
        for (int n = 0; n < currentblocks.Count; n++)
        {
            oldBlocks.Add(currentblocks[n]);

        }
      //  numblocks = oldBlocks.Count;

        //  Debug.Log($"non loaded {newblok} loaded: {oldblocks} num removable: {remove} converted: {nodublicates.Count}");
        newblocks = newblocks.OrderBy(x => x.Width).ToList();
        tiles[x, y].AddBlocks(newblocks);

        if (currentblocks.Count > 0)
        {
            blocksUpdated = true;

           // Debug.Log($"no change {x}:{y}");
            // Debug.Log(oldBlocks.Count);
        }
        


        //while (i < currentblocks.Count && i < newblocks.Count)
        //{
        //    var currentblock = currentblocks[i];
        //    var newblock = newblocks[i];
        //     Debug.Log($"block loaded: {currentblock.Loaded}");
        //    if (newblock.X != currentblock.X && newblock.Y != currentblock.Y || newblock.Width != currentblock.Width)
        //    {


        //         Extract the portion of the list to be updated
        //        List<Block> blocksToRemove = currentblocks.GetRange(i, currentblocks.Count - i);
        //        Debug.Log($"blocks to remove: {blocksToRemove.Count}");
        //         Remove elements from the current index to the end of the list
        //        currentblocks.RemoveRange(i, currentblocks.Count - i);

        //         Insert remaining elements from the newblocks list
        //        var blocks = newblocks.GetRange(i, newblocks.Count - i);
        //        numblocks = blocks.Count;
        //        currentblocks.InsertRange(i, newblocks.GetRange(i, newblocks.Count - i));

        //         Call DestroyChunk on the old part of the list
        //         Debug.Log($"number of chunks to remove: {blocksToRemove.Count} ");
        //        foreach (var block in blocksToRemove)
        //        {

        //             Assuming DestroyChunk is a method in the Block class
        //              Destroy(block.DestroyChunk());
        //            oldBlocks.Add(block.HideBlock());
        //        }
        //        blocksToRemove.Clear();
        //         Break out of the loop since we've handled the replacements
        //        blocksUpdated = true;
        //        break;
        //    }

        //    i++;
        //}

        if (currentblocks.Count > 0)
        {
            Debug.Log($"updating blocks {x}:{y} numblocks: {newblocks.Count} update mesh?: {blocksUpdated}");

        }



        Profiler.EndSample();
        // UnityEditor.EditorApplication.isPaused = true;
        return blocksUpdated;
    }



    Color[] InitializeHeightmap(int x, int y)
    {

        string textureName = $"Textures\\heightmap_{x}_{y}";
        // string WatertextureName = $"Textures\\WaterMask_{x}_{y}";
        // Debug.Log(textureName);
        Texture2D heightmapTexture = Resources.Load<Texture2D>(textureName);
        // Texture2D waterMaskTexture = Resources.Load<Texture2D>(WatertextureName);

        if (heightmapTexture != null)
        {
            Constants.heightmapWidth = heightmapTexture.width;
            return heightMap = heightmapTexture.GetPixels();




        }
        else
        {
            Debug.LogError($"Heightmap texture '{textureName}' not found in Resources.");
            return null;
        }
    }



    private void DisplayVoxels(int voxelwidth, int voxelheight, Vector3 chunkpos)
    {

        int voxel_Size = Mathf.Clamp(((voxelwidth - 1) / Constants.minChunkWidth) * Constants.minVoxelSize, 1, 256);
        //  Debug.Log(voxel_Size);
        voxelwidth = voxelwidth / minVoxelSize;
        voxelheight = voxelheight / minVoxelSize;
        for (int index = 0; index < CurrentvoxelData.Length; index++)
        {
            // Convert the 1D index to 3D coordinates
            int z = index % voxelwidth;
            int y = (index / voxelwidth) % voxelheight;
            int x = index / (voxelwidth * voxelheight);

            // Calculate the position of the voxel in world space
            Vector3 voxelPosition = new Vector3(
                x * voxel_Size,
                y * voxel_Size,
                z * voxel_Size
            );
            voxelPosition += new Vector3(chunkpos.x, 0, chunkpos.y);
            // Calculate a t value based on DistanceToSurface
            float t = Mathf.InverseLerp(0f, 10f, CurrentvoxelData[index].DistanceToSurface);

            // Interpolate between startColor and endColor based on t
            Color voxelcol = Color.Lerp(startColor, endColor, t);

            // Call the DrawCube method to draw a cube at the voxel's position
            if (CurrentvoxelData[index].DistanceToSurface <= displayThreshold)
            {
                // DrawCube(voxelPosition, voxelSize, voxelcol);
                Gizmos.color = voxelcol;
            }
            Gizmos.DrawWireCube(voxelPosition, new Vector3(voxel_Size, voxel_Size, voxel_Size));
        }
    }

    public float GetVoxelSample(Vector3 worldposition, int voxelSize, VoxelData[] voxelData)
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
            // UnityEditor.EditorApplication.isPlaying = false;


            return -1;
        }
        return voxelData[voxelIndex].DistanceToSurface;
    }

    #region Deprecated code

    //Deprecated - old method for initializing voxel structure size---------------------------------------------------------------------------------
    //VoxelData[] InitializeVoxelSize(int maxWidth, int voxelSize)
    //{

    //    // Calculate the number of voxels in each dimension
    //    xVoxels = Mathf.CeilToInt(maxWidth / voxelSize);
    //    yVoxels = Mathf.CeilToInt((float)height / voxelSize);
    //    zVoxels = xVoxels;

    //    // Initialize the voxelData array based on the calculated dimensions
    //    int totalVoxels = (xVoxels + 1) * (yVoxels + 1) * (zVoxels + 1);
    //    VoxelData[] voxelData = new VoxelData[totalVoxels];
    //    return voxelData;
    //    // Debug.Log($"voxels initialized {totalVoxels} xvoxels: {xVoxels + 1} yvoxels {yVoxels + 1} zvoxels {zVoxels + 1} maxwidth: {maxWidth}");
    //}
    //-----------------------------------------------------------------------------------------------------------------------------------------------

    //Deprecated - old method to generate chunks-----------------------------------------------------------------------------------------------------
    //private void GenerateVoxelStructure(int offsetX, int offsetZ, int voxelSize, VoxelData[] voxelData)
    //{
    //    int voxelWidth = xVoxels + 1;
    //    int voxelHeight = yVoxels + 1;
    //    int highestlevel = 0;

    //    for (int _index = 0; _index < voxelData.Length; _index++)
    //    {

    //        //get x & z coords
    //        int z = _index % voxelWidth;
    //        int y = (_index / voxelWidth) % voxelHeight;
    //        int x = _index / (voxelWidth * voxelHeight);

    //        // Apply the x and z offsets here
    //        int xOffset = offsetX;
    //        int zOffset = offsetZ;

    //        // Multiply the voxel positions by the voxel size first
    //        x *= voxelSize;
    //        z *= voxelSize;
    //        y *= voxelSize;

    //        // Apply offsets
    //        x += xOffset;
    //        z += zOffset;

    //        //using perlin noise
    //        //float perlinNoise = Mathf.PerlinNoise((float)x * voxelSize / 16f * 1.5f, (float)z * voxelSize / 16f * 1.5f);
    //        // voxelData[_index].DistanceToSurface = (y * voxelSize) - (10.0f * perlinNoise);

    //        //sample the height map
    //        int index = (x * Constants.heightmapWidth) + z;

    //        if (index >= heightMap.Length)
    //        {

    //            Debug.LogError($"x {x} y {y} z {z}");
    //            Debug.Log($"offset: {offsetX}:{offsetZ}");
    //            return;
    //        }

    //        Color sampledColor = heightMap[index];
    //        // Color sampleWater = WaterMap[index];
    //        float sampledHeight = sampledColor.r;
    //        // float water = sampleWater.r * 850;
    //        float scaledHeight = sampledHeight * height;

    //        //if (water > 0)
    //        //  cummulatedHeights.Add(sampledHeight);

    //        //  voxelData[_index].DistanceToSurface = (y * voxelSize) - scaledHeight;
    //        voxelData[_index].DistanceToSurface = y - scaledHeight;

    //        if (y > highestlevel)
    //        {
    //            highestlevel = y;
    //        }

    //        //if(y > 50)
    //        //{
    //        //    Debug.Log(y);
    //        //}
    //    }

    //    // Debug.Log($"done filling data structure. highest level: {highestlevel}");
    //}
    //-----------------------------------------------------------------------------------------------------------------------------------------------

    //Deprecated - old method for drawing wire cubes-------------------------------------------------------------------------------------------------
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
    //-----------------------------------------------------------------------------------------------------------------------------------------------

    //Deprecated - old method for scheduling mesh generation-----------------------------------------------------------------------------------------
    //private void GenerateMesh(Vector2Int chunkpos, int maxChunkWidth, int voxelSize, int blockId, VoxelData[] voxelData)
    //{
    //    int voxelLength = (xVoxels + 1) * (yVoxels + 1) * (zVoxels + 1);
    //    Vector2 chunkSize = new Vector2(maxChunkWidth, height);
    //    Vector2Int offset = new Vector2Int(chunkpos.x, chunkpos.y);
    //    //WorldData.TerrainData terrainData = new WorldData.TerrainData()
    //    //{
    //    //    CornerTable = WorldData.CornerTable,
    //    //    TriangleTable = WorldData.TriangleTable_1D,
    //    //    EdgeIndexes = WorldData.EdgeIndexes_1D,
    //    //};
    //    MeshGenerator generator = GetComponent<MeshGenerator>();
    //    // generator.GenerateMesh(voxelLength, xVoxels, yVoxels, voxelSize, chunkSize, offset, blockId, voxelData, terrainData);

    //}
    //-----------------------------------------------------------------------------------------------------------------------------------------------

    //Deprecated - old method for multithreaded chunk generation-------------------------------------------------------------------------------------
    //void GenerateChunks(List<Block> blocks, Color[] heightmap)
    //{
    //    // Debug.Log($"tilepos: {tilepos}");
    //    int blockId = 0;
    //    NativeArray<Color> heightMap = new NativeArray<Color>(heightmap, allocator: Allocator.TempJob);
    //    //initialize terrain data
    //    WorldData.TerrainData terrainData = new WorldData.TerrainData
    //    {
    //        CornerTable = new NativeArray<Vector3Int>(WorldData.CornerTable, allocator: Allocator.TempJob),
    //        EdgeIndexes = new NativeArray<int>(WorldData.EdgeIndexes_1D, Allocator.TempJob),
    //        TriangleTable = new NativeArray<int>(WorldData.TriangleTable_1D, Allocator.TempJob),
    //    };
    //    // NativeArray<JobHandle> jobs = new NativeArray<JobHandle>(blocks.Count,allocator: Allocator.TempJob);
    //    foreach (Block block in blocks)
    //    {
    //        //generate specific chunk level
    //        //if (block.Width > 128)
    //        //{
    //        //    continue;
    //        //}

    //        if (block.Loaded)
    //        {
    //            blockId++;
    //            continue;
    //        }


    //        int chunkWidth = block.Width;
    //        int x = block.X;
    //        int y = block.Y;
    //        int offsetX = x - (chunkWidth / 2);
    //        int offsetY = y - (chunkWidth / 2);
    //        int tileWidth = Constants.heightmapWidth - 1;
    //        block.Loaded = true;


    //        // Calculate the modulo to ensure that offsetX and offsetY are within the height map bounds
    //        offsetX = (offsetX + tileWidth) % tileWidth;
    //        offsetY = (offsetY + tileWidth) % tileWidth;


    //        //calculate voxelsize based on chunkWidth
    //        int voxel_Size = Mathf.Clamp((chunkWidth / width) * minVoxelSize, 1, 256);

    //        Profiler.BeginSample("initializing voxels");
    //        //initialize voxelsize
    //        var data = InitializeVoxelSize(chunkWidth + 1, (int)voxel_Size);
    //        Profiler.EndSample();



    //        Profiler.BeginSample("generating voxels");
    //        // Generate the voxel structure
    //        //  GenerateVoxelStructure(offsetY, offsetX, voxel_Size, data);

    //        //Generate voxel structure using job system
    //        NativeArray<VoxelData> voxelData = new NativeArray<VoxelData>(data, allocator: Allocator.TempJob);
    //        NativeList<Vector3> vertices = new NativeList<Vector3>(allocator: Allocator.TempJob);
    //        NativeList<int> triangles = new NativeList<int>(allocator: Allocator.TempJob);

    //        GenerateVoxelStructure_Job voxelStructure_Job = new GenerateVoxelStructure_Job()
    //        {
    //            offsetX = offsetY,
    //            offsetZ = offsetX,
    //            voxelSize = voxel_Size,
    //            heightmapWidth = Constants.heightmapWidth,
    //            height = height,
    //            voxelHeight = yVoxels + 1,
    //            voxelWidth = xVoxels + 1,
    //            //voxelData = voxelData,
    //            //heightMap = heightMap,
    //        };
    //        var job = voxelStructure_Job.Schedule(voxelData.Length, 64);


    //        Profiler.EndSample();

    //        // generate mesh
    //        Vector2Int chunkPos = new Vector2Int(offsetY, offsetX);
    //        // Vector2Int tileposOffset = new Vector2Int(heightmapWidth * tilepos.x, heightmapWidth * tilepos.y);

    //        Profiler.BeginSample("Generating mesh");
    //       // var meshjobhandle = GenerateMesh_Jobified(chunkPos, chunkWidth, voxel_Size, blockId, voxelData, vertices, triangles, job, terrainData);
    //        // GenerateMesh(chunkPos, chunkWidth, voxel_Size, blockId, data);

    //        JobHandle.CombineDependencies(job, meshjobhandle).Complete();
    //        Profiler.EndSample();

    //        Profiler.BeginSample("copying data");
    //        // Copy data to regular lists
    //        List<Vector3> newVerticesList = new List<Vector3>();
    //        List<int> newTrianglesList = new List<int>();

    //        for (int i = 0; i < vertices.Length; i++)
    //        {
    //            newVerticesList.Add(vertices[i]);
    //        }

    //        for (int i = 0; i < triangles.Length; i++)
    //        {
    //            newTrianglesList.Add(triangles[i]);
    //        }
    //        Profiler.EndSample();

    //        Profiler.BeginSample("finishing mesh");
    //        GetComponent<MeshGenerator>().CreateMesh(chunkPos, blockId, newVerticesList, newTrianglesList);

    //        blockId++;


    //        //dispose native arrays
    //        voxelData.Dispose();
    //        triangles.Dispose();
    //        vertices.Dispose();

    //        Profiler.EndSample();
    //    }
    //    // JobHandle.CompleteAll(jobs);
    //    heightMap.Dispose();
    //    terrainData.CornerTable.Dispose();
    //    terrainData.EdgeIndexes.Dispose();
    //    terrainData.TriangleTable.Dispose();
    //}
    //-----------------------------------------------------------------------------------------------------------------------------------------------

    #endregion

}

public class ScheduledBlock
{
    public List<int> blockIds;
    public NativeArray<JobHandle> jobHandles;

    public ScheduledBlock(int arraySize)
    {
        blockIds = new List<int>();
        jobHandles = new NativeArray<JobHandle>(arraySize, allocator: Allocator.Persistent);
    }
}