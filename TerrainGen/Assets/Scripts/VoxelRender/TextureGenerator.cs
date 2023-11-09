using UnityEngine;
using UnityEditor;

public class TextureGenerator : MonoBehaviour
{
    public int textureWidth = 64;
    public int textureHeight = 64;
    public int textureDepth = 64;
    public TextureFormat textureFormat = TextureFormat.RGBA32;
    public string savePath = "Assets/Textures/GeneratedTexture.asset";
    public float Frequency = 5.0f;
    private Texture3D generatedTexture;

    private void Start()
    {
        // Create the 3D texture
        generatedTexture = new Texture3D(textureWidth, textureHeight, textureDepth, textureFormat, false);
        generatedTexture.wrapMode = TextureWrapMode.Clamp;

        // Generate the texture data
        Color[] colors = new Color[textureWidth * textureHeight * textureDepth];
        for (int z = 0; z < textureDepth; z++)
        {
            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    // Calculate the normalized value based on position
                    float value = CalculateNormalizedValue(x, y, z);
                    // Set the color at the corresponding position
                    colors[x + y * textureWidth + z * textureWidth * textureHeight] = new Color(value, value, value, 1f);
                }
            }
        }

        // Set the texture data
        generatedTexture.SetPixels(colors);
        generatedTexture.Apply();

        // Save the texture as an asset
      //  AssetDatabase.CreateAsset(generatedTexture, savePath);
      //  AssetDatabase.SaveAssets();

        // Assign the texture to a material or renderer for visualization
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.SetTexture("_MainTex", generatedTexture);
        }
    }

    private float CalculateNormalizedValue(int x, int y, int z)
    {
        // Custom function to calculate the normalized value based on the position
        // Modify this function to generate the desired texture pattern
        float normalizedX = (float)x / textureWidth;
        float normalizedY = (float)y / textureHeight;
        float normalizedZ = (float)z / textureDepth;

        float value = Mathf.PerlinNoise(normalizedX * Frequency, normalizedY * Frequency + normalizedZ * Frequency);
        value = Mathf.Round(value);
        //if (x < 5 && y < 5 && z < 5)
        //{
        //    Debug.Log(value);
        //}
        return value;
    }
}
