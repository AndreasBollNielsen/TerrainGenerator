using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Block;

public class Constants
{
    public static int heightmapWidth;
    public static int minChunkWidth;
    public static int minVoxelSize;
    public static int height;

    public static Color[] Currentheightmap;

    public static void CalcMemory<T>(int data)
    {
        int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
        int totalSizeBytes = size * data;

        float totalSizeMB = totalSizeBytes / (1024f * 1024f);
        float totalSizeGB = totalSizeMB / 1024f;

        Debug.Log($"Number of bytes for {typeof(T)}: {totalSizeBytes}");
        Debug.Log($"Number of megabytes for {typeof(T)}: {totalSizeMB} MB");
        Debug.Log($"Number of gigabytes for {typeof(T)}: {totalSizeGB} GB");
    }

}
