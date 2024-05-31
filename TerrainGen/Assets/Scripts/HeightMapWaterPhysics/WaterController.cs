using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterController : MonoBehaviour
{
    public Texture2D heightMap;
    public Material material;
    //public GameObject waterMeshObject;

    private WaterData waterData;
    private WaterSimulation waterSimulation;
    private WaterMesher waterMeshGenerator;

    void Start()
    {
        
        waterData = new WaterData(100, 100);
        waterSimulation = new WaterSimulation(waterData);
        waterMeshGenerator = GetComponent<WaterMesher>();
       
        waterSimulation.InitializeHeightMap(heightMap);
       
        waterMeshGenerator.Initialize(waterData);


        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
      
    }

    void Update()
    {
        // Simulate water flow and update the water mesh each frame
        waterSimulation.SimulateWaterFlow();
        waterMeshGenerator.UpdateWaterMesh();
    }
}
