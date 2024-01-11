using System.Collections;
using System.Collections.Generic;
using Unity.Burst.CompilerServices;
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;

public class TerrainInteraction : MonoBehaviour
{
    Camera cam;
    RaycastHit hit;
    public float cubeSize = 2.0f;
    private float gridSize = 1.0f;
    public Mesh graphics;
    public Material mat;
    Vector3 graphicsPos = Vector3.zero;
   
    // Start is called before the first frame update
    void Start()
    {
        cam = Camera.main;

    }

    // Update is called once per frame
    void Update()
    {
        var rayOrigin = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0.0f));





        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 1f));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            Vector3 gridpos = GetNearestGridPosition(hit.point);
            graphicsPos = gridpos;
            
            //draw a cube
            Matrix4x4 matrix = new Matrix4x4();
            matrix.SetTRS(graphicsPos, Quaternion.identity, Vector3.one*3);
            Graphics.DrawMesh(graphics, matrix, mat, 0, cam, 0, null, false, false);

            if (Input.GetMouseButtonDown(0))
            {

                if (hit.transform.tag == "Terrain")
                {
                    WorldGenerator.Instance.GetChunk(hit.transform.position).AddTerrain(gridpos);
                    
                }


            }

            if (Input.GetMouseButtonDown(1))
            {
                if (Physics.Raycast(rayOrigin, cam.transform.forward, out hit))
                {
                   // WorldGenerator.Instance.GetChunk(hit.transform.position).RemoveSquare(gridpos, 3);
                    VoxelGenerator.Instance.RemoveSquare(gridpos, 3);
                }
            }
        }


    }



    private Vector3 GetNearestGridPosition(Vector3 position)
    {
        int x = Mathf.FloorToInt(position.x / gridSize);
        int y = Mathf.FloorToInt(position.y / gridSize);
        int z = Mathf.FloorToInt(position.z / gridSize);

        Vector3 gridPosition = new Vector3(x * gridSize, y * gridSize, z * gridSize);
        return gridPosition;
    }
}
