using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk_v
{
    public int blockId;
    public Mesh mesh;
    public GameObject chunkObject;
    List<int> topEdge;
    List<int> bottomEdge;
    List<int> leftEdge;
    List<int> rightEdge;
   public List<int> border;


    public Chunk_v()
    {
        List<int> topEdge = new List<int>();
        List<int> bottomEdge = new List<int>();
        List<int> leftEdge = new List<int>();
        List<int> rightEdge = new List<int>();
        border = new List<int>();
    }

    public Vector3 GetChunkPos()
    {
        return chunkObject.transform.position;
    }
    public void CopyEdges(List<int> top, List<int> bottom, List<int> left, List<int> right)
    {
        topEdge = new List<int>(top);
        bottomEdge = new List<int>(bottom);
        leftEdge = new List<int>(left);
        rightEdge = new List<int>(right);
        border.AddRange(top);
        border.AddRange(bottom);
        border.AddRange(left);
        border.AddRange(right);
    }


    public List<int> GetEdges(Vector3 edge)
    {
        if (edge == Vector3.up)
        {
            return topEdge;
        }
        else if (edge == Vector3.down)
        {
            return bottomEdge;
        }
        else if (edge == Vector3.left)
        {
            return leftEdge;
        }
        else if (edge == Vector3.right)
        {
            return rightEdge;
        }

        return null;
    }
}
