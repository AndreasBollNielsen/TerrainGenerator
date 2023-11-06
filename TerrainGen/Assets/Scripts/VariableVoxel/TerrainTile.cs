using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class TerrainTile
{
    private List<GameObject> meshChunks;
    private GameObject tileObject;
    int width;
    int numChunks;
    int lastState;
    public int X;
    public int Y;
    Dictionary<int, int> lodSelector;
    public event Action<Vector2Int> LODChanged;
    public TerrainTile(int _x,int _y)
    {
        this.X = _x;
        this.Y = _y;

        meshChunks = new List<GameObject>();

        lodSelector = new Dictionary<int, int>() {
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
               
                //trigger event when tile state changes
                if (value > 0)
                {
                    if (lastState != value)
                    {
                       // Debug.Log($"changed from {lastState} to {value}");
                        LODChanged?.Invoke(new Vector2Int(X,Y));
                        lastState = value;
                    }
                }
            }
            // Set the new value
            width = value;

        }
    }
    public int NumChunks { get => numChunks; set => numChunks = value; }

    public void AddChunks(List<GameObject> chunks)
    {
        meshChunks.Clear();
        meshChunks.AddRange(chunks);


    }

    public List<GameObject> RemoveChunks()
    {
        return meshChunks;
    }

    public GameObject GetTileObject()
    {
        return tileObject;
    }

    public void SetChunksParent(GameObject parent)
    {
        foreach (var chunk in meshChunks)
        {
            chunk.transform.SetParent(parent.transform, false);

        }

        tileObject = parent;
    }
}
