using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainTile
{
    private List<GameObject> meshChunks;
    private GameObject tileObject;
    int width;
    int numChunks;

    public TerrainTile()
    {
        meshChunks = new List<GameObject>();
    }

    public int Width { get => width; set => width = value; }
    public int NumChunks { get => numChunks; set => numChunks = value; }

    public void AddChunks(List<GameObject> chunks)
    {
        meshChunks.AddRange(chunks);
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
