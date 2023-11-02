using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainTile 
{
    private List<GameObject> meshChunks;

    public TerrainTile()
    {
        meshChunks = new List<GameObject>();
    }

    public void AddChunks(List<GameObject> chunks)
    {
        meshChunks.AddRange(chunks);
    }

    public void SetChunksParent(GameObject parent)
    {
        foreach (var chunk in meshChunks)
        {
            chunk.transform.SetParent(parent.transform, false);

          //  Debug.Log("adding chunk");
        }
    }
}
