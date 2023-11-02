using System.Collections;
using System.Collections.Generic;
using Unity.Burst.CompilerServices;
using UnityEngine;

public class GridSnapper : MonoBehaviour
{
    public float gridSize = 10f;
    public Mesh graphics;
    public Material ghostMat;
    public GameObject foundationPrefab;

    Vector3 rayOrigin;
    RaycastHit hit;

    // Start is called before the first frame update
    void Start()
    {

    }

    private void Update()
    {
        rayOrigin = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0.0f));


        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(rayOrigin, Camera.main.transform.forward, out hit))
        {
            // Get the closest grid position based on the hit point
            Vector3 gridPosition = GetNearestGridPosition(hit.point);

            //snap to foundation
            if(hit.collider.tag =="foundation")
            {
                float Yoffset = hit.point.y+ hit.collider.bounds.size.y/2;
                gridPosition = new Vector3(gridPosition.x, Yoffset, gridPosition.z);
            }

            // Snap the object to the grid position
            DrawMesh(graphics, gridPosition);

            //place item
            if (Input.GetMouseButtonDown(0))
            {
                var building = Instantiate(foundationPrefab, gridPosition, foundationPrefab.transform.rotation);
                building.GetComponent<BoxCollider>().enabled = true;
                building.transform.Find("SnappingPoints").gameObject.SetActive(true);
            }
        }

    }

    void DrawMesh(Mesh mesh, Vector3 pos)
    {
        Matrix4x4 matrix = new Matrix4x4();
        matrix.SetTRS(pos, Quaternion.identity, new Vector3(5, 1, 5));
        Graphics.DrawMesh(mesh, matrix, ghostMat, 0);
    }

    private Vector3 GetNearestGridPosition(Vector3 position)
    {
        int x = Mathf.RoundToInt(position.x / gridSize);
        int y = Mathf.RoundToInt(position.y / gridSize);
        int z = Mathf.RoundToInt(position.z / gridSize);

        Vector3 gridPosition = new Vector3(x * gridSize, y * gridSize, z * gridSize);
        return gridPosition;
    }
}
