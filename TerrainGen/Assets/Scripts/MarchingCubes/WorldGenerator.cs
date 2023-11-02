using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;

public class WorldGenerator : MonoBehaviour
{
    private static WorldGenerator _instance;
    public static WorldGenerator Instance { get { return _instance; } }

    public Texture2D HeightMap;
    public Texture2D[] heightmaps;

    public int WorldChunks = 10;
    public int width { get { return WorldData.ChunkWidth * WorldChunks + 1; } }
    public int height { get { return WorldData.ChunkHeight + 1; } }
    public VoxelData[] terrainMap;
    // Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();
    List<Chunk> chunkList = new List<Chunk>();
    public bool useJob;
    WorldData.SharedMethod _sharedMethod;
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        _sharedMethod = new WorldData.SharedMethod();

        //add event to check for changes in editor mode
        EditorApplication.playModeStateChanged += LogPlayModeState;
        // HeightMap = ReadTexture();
        // StartCoroutine(InitializeTerrainMap());
        if (!useJob)
        {
            InitializeTerrainMap();
        }
        else
        {
            for (int i = 0; i < 4; i++)
            {
                string[] strings = heightmaps[i].name.Split("_");

                Vector3Int coords = new Vector3Int(int.Parse(strings[1]), 0, int.Parse(strings[2]));
                // heightmaps[i].name =$"{strings[0]}-{strings[1]}" ;
                // Debug.Log($"coords: {coords}");

                // Debug.Log($"altered name: {heightmaps[i].name}");
                InitializeTerrain_Job(heightmaps[i], coords);
            }
        }

       
        


        // GenerateTerrain();
        //  StartCoroutine(GenerateTerrain());
    }

    //fires event when editor changes play mode
    private void LogPlayModeState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            if (chunkList.Count > 0)
            {

                //foreach (Chunk cnk in chunkList)
                //{
                //    Destroy(cnk.filter.mesh);
                //    Destroy(cnk.chunkObject);
                //}
            }
        }
        Debug.Log(state);
    }

    IEnumerator GenerateTerrain(Vector3Int tilepos)
    {
        int totalMemory = 0;
        GameObject tile = new GameObject();
        tile.name = $"Tile_{tilepos.x}_{tilepos.z}";
        tile.transform.SetParent(this.transform);
        float percentageIncrease = WorldChunks * WorldChunks / 100f;
        float percentageCompletion = 0;
        int chunktotal = WorldChunks * WorldChunks;
        int chunknum = 0;
        for (int x = 0; x < WorldChunks; x++)
        {
            for (int z = 0; z < WorldChunks; z++)
            {

                Vector3Int chunkpos = new Vector3Int(x * WorldData.ChunkWidth, 0, z * WorldData.ChunkWidth);
                Chunk chunk = new Chunk(chunkpos);
                chunk.chunks.Add(chunkpos, chunk);
                chunk.chunkObject.transform.SetParent(tile.transform);
                // chunks.Add(chunkpos, new Chunk(chunkpos));
                //chunks[chunkpos].chunkObject.transform.SetParent(tile.transform);
                percentageCompletion += percentageIncrease;
                chunkList.Add(chunk);
                totalMemory += chunk.Totalmemory;
                //  Debug.Log($"{tile.name} chunk {chunknum} out of {chunktotal}");
                chunknum++;
                yield return new WaitForEndOfFrame();
            }
        }

        //move tile to tile position
        tile.transform.position = new Vector3(WorldGenerator.Instance.width * tilepos.x, 0, WorldGenerator.Instance.width * tilepos.z);

        //print total amount of mesh memory
        Debug.Log($"Mesh data: {MemoryHelper.ConvertBytes(totalMemory)}");

        terrainMap = null;
        //spawn player
        FindObjectOfType<FPS_Controller>().enabled = true;
    }

    void InitializeTerrainMap()
    {
        terrainMap = new VoxelData[width * height * width];
        //VoxelData[] tempTerrain = new VoxelData[5 * 5 * 5];
        //List<float> threedDist = new List<float>();
        //List<float> flatDist = new List<float>();
        //List<Vector3Int> posThreeD = new List<Vector3Int>();
        //List<Vector3Int> posFlat = new List<Vector3Int>();

        for (int x = 0; x < width; x++)
        {
            //count += increment;
            //Debug.Log($"{Mathf.RoundToInt(count)} percent done...");
            // yield return new WaitForEndOfFrame();
            for (int y = 0; y < height; y++)
            {

                for (int z = 0; z < width; z++)
                {
                    //get terrain height from a perlin noise
                    float terrainheight = WorldData.GetTerrainHeight(x, z, HeightMap);

                    //  Debug.Log("3d array: " +terrainheight);
                    //threedDist.Add(terrainheight);
                    //posThreeD.Add(new Vector3Int(x, y, z));
                    //if(x< 10 && y == 0 && z < 10)
                    //{
                    //    Color sampledColor = HeightMap.GetPixel(x, z);
                    //    Debug.Log(sampledColor);
                    //}

                    //set height value
                    //terrainMap[x, y, z] = new VoxelData((float)y - terrainheight, SetTerrainmaterial(y));
                }
            }
        }

        //test flattened array
        //for (int i = 0; i < tempTerrain.Length; i++)
        //{
        //    int z = i % 5;
        //    int y = (i / 5) % 5;
        //    int x = i / (5 * 5);

        //    float terrainheight = WorldData.GetTerrainHeight(x, z, HeightMap);
        //    flatDist.Add(terrainheight);
        //    posFlat.Add(new Vector3Int(x, y, z));
        //   // Debug.Log("flat array: " + terrainheight);
        //    tempTerrain[i] = new VoxelData((float)y - terrainheight, SetTerrainmaterial(y));
        //}

        //for (int j = 0; j < threedDist.Count; j++)
        //{
        //    Debug.Log($"3d array: {threedDist[j]} flat array: {flatDist[j]}");
        //    Debug.Log($"3d pos array: {posThreeD[j]} flat pos array: {posFlat[j]}");
        //}

        printMemory();

        // yield return new WaitForSeconds(1);

        //run mesh generation
        //  StartCoroutine(GenerateTerrain());
        Debug.Log(terrainMap[0].DistanceToSurface);
    }

    void printMemory()
    {
        int sizeOfInt = sizeof(int);
        int sizeOfFloat = sizeof(float);


        int voxelSizeInBytes = sizeOfInt + sizeOfFloat;
        int arraySizeInBytes = voxelSizeInBytes * terrainMap.Length;

        float sizeInMB = arraySizeInBytes / (1024f * 1024f);
        float sizeInGB = arraySizeInBytes / (1024f * 1024f * 1024f);

        Debug.Log("Voxel Data size in MB: " + sizeInMB);
        if (sizeInMB > 1000)
        {
            Debug.Log("Voxel Data in GB: " + sizeInGB);
        }
    }

    void InitializeTerrain_Job(Texture2D heightmap, Vector3Int tilepos)
    {
        HeightMap = heightmap;

        //copy data to nativearray
        NativeArray<Color> heightmapCols = new NativeArray<Color>(heightmap.GetPixels(), Allocator.TempJob);
        NativeArray<VoxelData> voxelDatas = new NativeArray<VoxelData>(width * height * width, Allocator.TempJob);



        //generate voxels
        InitializeVoxelMap_Job job = new InitializeVoxelMap_Job
        {
            dataArray = voxelDatas,
            heightMap = heightmapCols,
            width = width,
            height = height,
            depth = width,
            MaxTerrainHeight = WorldData.MaxTerrainHeight,
            BaseHeight = WorldData.BaseHeight,
            textureSize = HeightMap.width,
            _sharedMethod = _sharedMethod
        };
        JobHandle handle = job.Schedule(voxelDatas.Length, 64);
        handle.Complete();
        Debug.Log($"voxels {tilepos.x}_{tilepos.z} complete...");
        //copy voxels
        terrainMap = new VoxelData[width * height * width];
        voxelDatas.CopyTo(terrainMap);


        //dispose
        heightmapCols.Dispose();
        voxelDatas.Dispose();
        printMemory();



        //run mesh generation
        StartCoroutine(GenerateTerrain(tilepos));
    }

    public Vector3Int VoxelToChunkPos(Vector3 voxelPos)
    {
        int chunkX = Mathf.FloorToInt(voxelPos.x / WorldData.ChunkWidth) * WorldData.ChunkWidth;
        int chunkZ = Mathf.FloorToInt(voxelPos.z / WorldData.ChunkWidth) * WorldData.ChunkWidth;

        return new Vector3Int(chunkX, 0, chunkZ);
    }

    public Chunk GetChunk(Vector3 pos)
    {
        int x = (int)pos.x;
        int y = (int)pos.y;
        int z = (int)pos.z;

        // return chunks[new Vector3Int(x, y, z)];
        return null;
    }

    int SetTerrainmaterial(float height)
    {
        int materialIndex = 3;
        if (height >= WorldData.BaseHeight)
        {
            materialIndex = 0;
        }
        else if (height > 30)
        {
            materialIndex = 1;
        }
        else if (height > 0)
        {
            materialIndex = 2;
        }


        return materialIndex;
    }



    [BurstCompile]
    public struct InitializeVoxelMap_Job : IJobParallelFor
    {
        public NativeArray<VoxelData> dataArray;
        [ReadOnly] public NativeArray<Color> heightMap;
        public int width;
        public int height;
        public int depth;
        public float BaseHeight; //base level of terrain
        public float MaxTerrainHeight;
        public int textureSize;
        public WorldData.SharedMethod _sharedMethod;
        public void Execute(int index)
        {


            // Convert the 1D index to 3D coordinates
            int x = index % width;
            int y = (index / width) % height;
            int z = index / (width * height);



            // Access the data array using the 3D coordinates
            int dataIndex = x + y * width + z * (width * height);



            // float terrainheight = GetTerrainHeight(index);
            float terrainheight = _sharedMethod.GetTerrainHeight(this, index);
            float terrainDist = (float)y - terrainheight;
            dataArray[dataIndex] = new VoxelData(terrainDist, SetTerrainmaterial(terrainDist, y));
        }

        float GetTerrainHeight(int _index)
        {
            int z = _index % width;
            int x = _index / (width * height);
            int y = (_index / width) % height;


            // int heightIndex = x + y * width + z * (width * height);


            Color sampledColor = heightMap[x * textureSize + z];
            float sampledHeight = sampledColor.grayscale;
            float scaledHeight = sampledHeight * MaxTerrainHeight + BaseHeight;

            if (x < 10 && y == 0 && z < 10)
            {
                //Color sampledColor = HeightMap.GetPixel(x, z);
                // Debug.Log(sampledColor);
            }

            return scaledHeight;
            // return 1;


        }

        int SetTerrainmaterial(float height, int ypos)
        {
            // float dist = (float)ypos - height;

            int materialIndex = 1;

            if (height > -1.5f)
            {
                materialIndex = 0;
            }



            return materialIndex;
        }
    }
}

