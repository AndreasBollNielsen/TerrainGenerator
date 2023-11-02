using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MemoryHelper
{
    public static int CalcTotalMemory(Mesh mesh)
    {
        int vertexCount = mesh.vertexCount;
        int triangleCount = mesh.triangles.Length / 3; // Divided by 3 because triangles are represented as indices

        int vertexSize = 12; // Size of a Vector3 (3 * 4 bytes)
        int normalSize = 12; // Size of a Vector3 (3 * 4 bytes)
        int colorSize = 16; // Size of a Color32 (4 * 4 bytes)

        int vertexDataSize = vertexCount * vertexSize;
        int normalDataSize = vertexCount * normalSize;
        int colorDataSize = vertexCount * colorSize;

        int triangleDataSize = triangleCount * sizeof(int) * 3; // 3 indices per triangle, each index is an integer (4 bytes)

        int totalMemoryUsage = vertexDataSize + normalDataSize + colorDataSize + triangleDataSize;
        return totalMemoryUsage;
    }

    /// <summary>
    /// returns a string with the converted bytes to KB,MB or GB
    /// </summary>
    /// <param name="totalBytes"></param>
    /// <returns></returns>
    public static string ConvertBytes(int totalBytes)
    {
        float KB = totalBytes/ 1024f;
        float MB = totalBytes / (1024f * 1024f);
        float GB = totalBytes / (1024f * 1024f * 1024f);

        if(KB < 1000)
        {
            return $"{KB} KB";
        }
        else if(MB < 1000)
        {
            return $"{MB} MB";
        }
        else if (MB >= 1000 )
        {
            return $"{GB} GB";
        }

        return "0 bytes";
    }
}
