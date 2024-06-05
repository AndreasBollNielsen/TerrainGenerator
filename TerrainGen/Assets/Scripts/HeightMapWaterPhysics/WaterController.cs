using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class WaterController : MonoBehaviour
{
    public Texture2D heightMap;
    public Material material;
    public GameObject debugObject;

    private WaterData waterData;
    private WaterSimulation waterSimulation;
    private WaterMesher waterMeshGenerator;

    void Start()
    {
        
        waterData = new WaterData(100, 100,0.5f);
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
        UpdateDebugMap();
    }

    void UpdateDebugMap()
    {
        // Create a new texture with the same dimensions as the water height map
        Texture2D texture = new Texture2D(waterData.Width, waterData.Height);

        // Populate the texture with grayscale values based on the water levels
        for (int x = 0; x < waterData.Width; x++)
        {
            for (int y = 0; y < waterData.Height; y++)
            {
                float waterHeight = waterData.WaterHeightMap[x, y];
                Color color = new Color(waterHeight, waterHeight, waterHeight); // Use the water height as the grayscale value
                texture.SetPixel(x, y, color);
            }
        }

        // Apply the changes to the texture
        texture.Apply();

        // Assign the texture to the debug object's material
        Renderer renderer = debugObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.mainTexture = texture;
        }
    }
}
