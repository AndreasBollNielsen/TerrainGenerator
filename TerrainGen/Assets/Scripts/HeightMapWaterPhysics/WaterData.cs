using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterData 
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public float[,] TerrainHeightMap { get; private set; }
    public float[,] WaterHeightMap { get; private set; }

    public WaterData(int width, int height)
    {
        Width = width;
        Height = height;
        TerrainHeightMap = new float[width, height];
        WaterHeightMap = new float[width, height];
    }
}
