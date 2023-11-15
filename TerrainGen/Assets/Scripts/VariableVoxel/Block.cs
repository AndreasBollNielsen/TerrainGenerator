using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Block
{
    public int NumChunks;
    public int Width;
    List<Chunk_v> chunks;
    public int X;
    public int Y;
   
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
}
