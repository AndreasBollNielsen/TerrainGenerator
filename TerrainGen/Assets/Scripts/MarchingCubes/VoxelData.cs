using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct VoxelData
{
    public float DistanceToSurface;
    public int TexIndex;


    public VoxelData(float dist, int index)
    {
        DistanceToSurface = dist;
        TexIndex = index;
    }
}
