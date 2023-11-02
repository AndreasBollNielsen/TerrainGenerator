using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainTile 
{
    private List<GameObject> meshChunks;
    private GameObject tileObject;

    public TerrainTile()
    {
        meshChunks = new List<GameObject>();
    }

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
