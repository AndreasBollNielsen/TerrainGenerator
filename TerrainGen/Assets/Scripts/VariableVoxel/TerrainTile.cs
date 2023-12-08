using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TerrainTile
{
    //private List<Chunk_v> meshChunks;
    private List<Block> blocks;
    private GameObject tileObject;
    int width;
    int numChunks;
    int lastState;
    public int X;
    public int Y;
    Dictionary<int, int> lodSelector;
    public event Action<Vector2Int> LODChanged;
    public TerrainTile(int _x, int _y)
    {
        this.X = _x;
        this.Y = _y;

        //meshChunks = new List<Chunk_v>();
        blocks = new List<Block>();

        lodSelector = new Dictionary<int, int>() {
            {128,16 },
            {256,8 },
            {512,4 },
            {1024,2 },
            {2048,1 }
        };
    }

    public int Width
    {
        get => width;
        set
        {
            // Check if the new value is different from the current value
            if (width != value)
            {
                // Set LOD level when width changes
                if (value != 0)
                    NumChunks = lodSelector[value];

                width = value;
                //trigger event when tile state changes
                if (value > 0)
                {
                    if (lastState != value)
                    {
                        // Debug.Log($"changed from {lastState} to {value}");
                        //  Debug.Log($"triggered: {Width} tilepos: {X}:{Y}");
                        lastState = value;
                        LODChanged?.Invoke(new Vector2Int(X, Y));
                    }
                }
            }
            // Set the new value
            //  width = value;

        }
    }
    public int NumChunks { get => numChunks; set => numChunks = value; }

    public void AddChunks(List<Chunk_v> chunks)
    {
        for (int i = 0; i < blocks.Count; i++)
        {

            var chunk = chunks.FirstOrDefault(x => x.blockId == i);
            if (chunk != null)
            {
                blocks[i].AddChunk(chunk);
            }
        }


    }

    public void AddBlocks(List<Block> _blocks)
    {
        blocks.Clear();
        blocks.AddRange(_blocks);
    }

    public List<Block> GetBlocks()
    {
        return blocks;
    }

    public GameObject GetTileObject()
    {
        if (tileObject != null)
        {
            return tileObject;

        }
        return null;
    }

    public void SetChunksParent(GameObject parent)
    {
        foreach (var block in blocks)
        {
            if (block.GetChunk() != null)
            {
                block.GetChunk().chunkObject.transform.SetParent(parent.transform, false);

            }
            else
            {
                Debug.LogError($"chunk class at {block.GetPosition()} is null");
            }

        }

        if (tileObject == null)
        {
            tileObject = parent;

        }
    }
}
