using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HoleGenerator : MonoBehaviour
{
    Ray ray;

    // Start is called before the first frame update
    void Start()
    {
        TerrainData td = Terrain.activeTerrain.terrainData;
        //  var bounds = GetComponent<BoxCollider>();
        int size = td.heightmapResolution - 1;
        // Debug.Log(size);
        int layermask = 1 << LayerMask.NameToLayer("hole");
        // Debug.Log(layermask);
        bool[,] holeData = new bool[size, size];
        //var minBounds = new Vector2(transform.position.x - bounds.size.x, transform.position.z - bounds.size.z);
        //var maxBounds = new Vector2(transform.position.x + bounds.size.x, transform.position.z + bounds.size.z);

        //Debug.Log(minBounds);

        float scaleFactor = td.size.x / size;
        // Debug.Log(scaleFactor);

        //initialize array
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                holeData[i, j] = true;
            }
        }


        for (int x = 0; x < td.size.x; x++)
        {
            for (int y = 0; y < td.size.x; y++)
            {
                ray = new Ray(new Vector3(x, 1000, y), Vector3.down * 1000);
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layermask))
                {
                    //Debug.Log(hit.transform.name);
                  //  Debug.DrawRay(new Vector3(hit.point.x, 0, hit.point.z), Vector3.up * 100, Color.green, 1000);
                     int xScaled = Mathf.RoundToInt(hit.point.x / scaleFactor);
                    int yScaled = Mathf.RoundToInt(hit.point.z / scaleFactor);
                    holeData[yScaled, xScaled] = false;
                }
            }

        }


        

        //set hole data
        td.SetHoles(0, 0, holeData);
    }

    // Update is called once per frame
    void Update()
    {

    }
}
