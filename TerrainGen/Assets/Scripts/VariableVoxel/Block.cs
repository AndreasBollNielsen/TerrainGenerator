using System.Collections;
using System.Collections.Generic;
using Unity.Entities.UniversalDelegates;
using UnityEngine;

public class Block
{
    public int NumChunks;
    public int Width;
    Chunk_v chunk;
    public int X;
    public int Y;
    public bool Loaded;

    Dictionary<int, int> lodSelector;

    public Block(int _width)
    {
        this.Width = _width;


        lodSelector = new Dictionary<int, int>() {
            {128,16 },
            {256,8 },
            {512,4 },
            {1024,2 },
            {2048,1 }
        };
    }

    public Vector2 GetPosition()
    {
        return new Vector2(X, Y);
    }

    public void AddChunk(Chunk_v _chunk)
    {
        chunk = _chunk;
       
    }

    public GameObject DestroyChunk()
    {
        if (chunk != null)
        {
            chunk.mesh.Clear();

            // Assuming chunkObject is a reference to the GameObject you want to destroy
            GameObject chunkGameObject = chunk.chunkObject;


            // Set the chunk reference to null to avoid further usage
            chunk = null;

            return chunkGameObject;
        }

        return null; // No chunk to destroy
    }

    public Chunk_v GetChunk()
    {
        return chunk;
    }
}
