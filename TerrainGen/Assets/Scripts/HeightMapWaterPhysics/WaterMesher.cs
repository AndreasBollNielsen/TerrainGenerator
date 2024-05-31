using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterMesher : MonoBehaviour
{
    private WaterData waterData;
    private Mesh waterMesh;
    private Vector3[] vertices;
    private int[] triangles;
    private Vector2[] uv;

    public void Initialize(WaterData data)
    {
        waterData = data;
        InitializeWaterMesh();
    }

    void InitializeWaterMesh()
    {
        waterMesh = new Mesh();
        GetComponent<MeshFilter>().mesh = waterMesh;

        vertices = new Vector3[waterData.Width * waterData.Height];
        triangles = new int[(waterData.Width - 1) * (waterData.Height - 1) * 6];
        uv = new Vector2[waterData.Width * waterData.Height];

        int index = 0;
        for (int x = 0; x < waterData.Width; x++)
        {
            for (int y = 0; y < waterData.Height; y++)
            {
                vertices[index] = new Vector3(x, waterData.WaterHeightMap[x, y], y);
                uv[index] = new Vector2((float)x / waterData.Width, (float)y / waterData.Height);
                index++;
            }
        }

        index = 0;
        for (int x = 0; x < waterData.Width - 1; x++)
        {
            for (int y = 0; y < waterData.Height - 1; y++)
            {
                int vertexIndex = x * waterData.Height + y;

                triangles[index++] = vertexIndex;
                triangles[index++] = vertexIndex + waterData.Height + 1;
                triangles[index++] = vertexIndex + waterData.Height;

                triangles[index++] = vertexIndex;
                triangles[index++] = vertexIndex + 1;
                triangles[index++] = vertexIndex + waterData.Height + 1;
            }
        }

        waterMesh.vertices = vertices;
        waterMesh.triangles = triangles;
        waterMesh.uv = uv;
        waterMesh.RecalculateNormals();

       
    }

    public void UpdateWaterMesh()
    {
        for (int x = 0; x < waterData.Width; x++)
        {
            for (int y = 0; y < waterData.Height; y++)
            {
                int index = x * waterData.Height + y;
                vertices[index].y = waterData.WaterHeightMap[x, y];
            }
        }

        waterMesh.vertices = vertices;
        waterMesh.RecalculateNormals();
    }
}
