using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using static Unity.VisualScripting.Member;
using static UnityEditor.ShaderData;

public class Raymarcher : MonoBehaviour
{
    public RenderTexture target;
    public RenderTexture targetDepth;
    public ComputeShader computeShader;
    public Material blitMaterial;
    public CustomPassVolume volume;
    public Texture2D heightMap;
    int kernel;
    RaymarchPass pass;
    Camera cam;
    // Start is called before the first frame update
    void Start()
    {
        // 1. Find custom pass volume i scenen
        //var volume = FindObjectOfType<CustomPassVolume>();
        if (volume == null)
        {
            Debug.LogError("Ingen CustomPassVolume fundet i scenen!");
            return;
        }

        // 2. Hent din RaymarchPass fra volume
        pass = volume.customPasses[0] as RaymarchPass;
        if (pass == null)
        {
            Debug.LogError("Custom pass var ikke af typen RaymarchPass!");
            return;
        }

        Vector2 terrainOrigin = new Vector2(0, 0);

        // 3. Lav RenderTextures
        //target = new RenderTexture(Screen.width, Screen.height, 0,
        //    RenderTextureFormat.ARGBFloat);
        //target.enableRandomWrite = true;
        //target.Create();

        //targetDepth = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RFloat);
        //targetDepth.enableRandomWrite = true;
        //targetDepth.Create();

        // 4. Bind rendertexture + material til din custom pass
        //pass.source = target;
        //pass.blitMaterial = blitMaterial;

        // 5. Kernel
        kernel = computeShader.FindKernel("CSMain");


        //set static properties
        computeShader.SetTexture(kernel, "_HeightTex", heightMap);
        computeShader.SetVector("_TerrainSize", new Vector2(2048, 2048));
        computeShader.SetFloat("_HeightScale", 2000.0f);
        computeShader.SetVector("_TerrainOrigin", terrainOrigin);

    

    }

    // Update is called once per frame
    void Update()
    {
        cam = Camera.main;
        var sun_lightdir = RenderSettings.sun.transform.forward;

        Matrix4x4 view = cam.worldToCameraMatrix;

        

        computeShader.SetVector("_CamPos", cam.transform.position);
        computeShader.SetVector("_CamForward", cam.transform.forward);
        computeShader.SetVector("_CamRight", cam.transform.right);
        computeShader.SetVector("_CamUp", cam.transform.up);
        computeShader.SetFloat("_CamFov", cam.fieldOfView);
        computeShader.SetFloat("_CamAspect", (float)cam.pixelWidth / cam.pixelHeight);

        Vector3 lightDir = new Vector3(1, 0, 0).normalized;
        lightDir = sun_lightdir.normalized;
        computeShader.SetVector("_LightDir", lightDir);

        computeShader.SetInt("Width", target.width);
        computeShader.SetInt("Height", target.height);
        computeShader.SetTexture(kernel, "Result", target);
        computeShader.SetTexture(kernel, "ResultDepth", targetDepth);

        computeShader.Dispatch(kernel, target.width / 8, target.height / 8, 1);
    }
}
