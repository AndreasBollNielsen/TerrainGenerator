using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PersistenceManager : MonoBehaviour
{
    public Dictionary<Vector3Int, List<VoxelStack>> ModifiedVoxels = new Dictionary<Vector3Int, List<VoxelStack>>();


    // Start is called before the first frame update
    void Start()
    {
        int sizeX = 5; // Number of iterations along the X-axis
        int sizeY = 5; // Number of iterations along the Y-axis
        int sizeZ = 5; // Number of iterations along the Z-axis

        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    int randnum = Random.Range(0, 10);
                    Vector3Int newkey = new Vector3Int(x, y, z);
                    List<VoxelStack> stacks = new List<VoxelStack>();
                    for (int i = 0; i < randnum; i++)
                    {
                        Vector3 startpos = new Vector3(x, y, z) + new Vector3(Random.Range(-50, 50), Random.Range(-50, 50), Random.Range(-50, 50));
                        Vector3 endpos = startpos;
                        VoxelStack stack = new VoxelStack { Endpos = endpos, Startpos = startpos };
                        stacks.Add(stack);
                    }

                    ModifiedVoxels.Add(newkey, stacks);

                }
            }
        }

        // showdebugger();

        SortStacks(ModifiedVoxels[new Vector3Int(0, 0, 0)]);

        showdebugger();

    }

    private void showdebugger()
    {
        Debug.Log("---------------------------------------------------------------------");
        foreach (KeyValuePair<Vector3Int, List<VoxelStack>> keyValuePair in ModifiedVoxels)
        {
            var key = keyValuePair.Key;
            var points = keyValuePair.Value;
            for (int i = 0; i < points.Count; i++)
            {
                Debug.Log($"key: {key} num points: {points[i].Startpos}");

            }
        }
    }

    //load method to read data from disk

    //return list of points at position
    public List<VoxelStack> GetStacks(Vector3Int keypos)
    {
        if (ModifiedVoxels.ContainsKey(keypos))
        {
            return ModifiedVoxels[keypos];
        }
        return null;
    }

    //add points to dictionary. 
    public void AddToStack(Vector3Int key, List<VoxelStack> voxelStacks)
    {
        if (ModifiedVoxels.ContainsKey(key))
        {
            ModifiedVoxels[key].AddRange(voxelStacks);
        }
        else
        {
            ModifiedVoxels.Add(key, voxelStacks);
        }
    }

    //sort stacks
    public void SortStacks(List<VoxelStack> voxelStacks)
    {
        if (voxelStacks == null || voxelStacks.Count <= 1)
            return;

        QuickSortRecursive(voxelStacks, 0, voxelStacks.Count - 1);
    }

    private void QuickSortRecursive(List<VoxelStack> voxelStacks, int left, int right)
    {
        if (left < right)
        {
            int pivotIndex = Partition(voxelStacks, left, right);
            QuickSortRecursive(voxelStacks, left, pivotIndex - 1);
            QuickSortRecursive(voxelStacks, pivotIndex + 1, right);
        }
    }

    private int Partition(List<VoxelStack> voxelStacks, int left, int right)
    {
        VoxelStack pivot = voxelStacks[right];
        int i = left - 1;

        for (int j = left; j < right; j++)
        {
            if (!compareVoxels(voxelStacks[j],pivot))
            {
                i++;
                Swap(voxelStacks, i, j);
            }
        }

        Swap(voxelStacks, i + 1, right);
        return i + 1;
    }

    private bool compareVoxels(VoxelStack a, VoxelStack b)
    {
        if (a.Endpos.x < b.Endpos.x && a.Endpos.y < b.Endpos.y && a.Endpos.z < b.Endpos.z)
        {
            return true;
        }
        return false;
    }

    //private int CompareVoxelStacks(VoxelStack a, VoxelStack b)
    //{
    //    // Check if stack A represents a single point or a range
    //    bool aIsSinglePoint = a.Startpos == a.Endpos;

    //    // Check if stack B represents a single point or a range
    //    bool bIsSinglePoint = b.Startpos == b.Endpos;

    //    if (aIsSinglePoint && bIsSinglePoint)
    //    {
    //        // Both stacks represent single points, compare their positions directly
    //        return Vector3.Compare(a.Startpos, b.Startpos);
    //    }
    //    else if (aIsSinglePoint)
    //    {
    //        // Stack A represents a single point, compare its position with the range of stack B
    //        return Vector3.Compare(a.Startpos, b.Startpos);
    //    }
    //    else if (bIsSinglePoint)
    //    {
    //        // Stack B represents a single point, compare its position with the range of stack A
    //        return Vector3.Compare(a.Startpos, b.Startpos);
    //    }
    //    else
    //    {
    //        // Both stacks represent ranges, compare their positions
    //        return Vector3.Compare(a.Startpos, b.Startpos);
    //    }
    //}

    private void Swap(List<VoxelStack> voxelStacks, int i, int j)
    {
        VoxelStack temp = voxelStacks[i];
        voxelStacks[i] = voxelStacks[j];
        voxelStacks[j] = temp;
    }
}

public class VoxelStack
{
    private Vector3 startpos;
    private Vector3 endpos;

    public Vector3 Startpos { get => startpos; set => startpos = value; }
    public Vector3 Endpos { get => endpos; set => endpos = value; }
}