using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterSimulation 
{
    private WaterData waterData;

    public WaterSimulation(WaterData data)
    {
        waterData = data;
    }

    public void InitializeHeightMap(Texture2D heightMap)
    {
        for (int x = 0; x < waterData.Width; x++)
        {
            for (int y = 0; y < waterData.Height; y++)
            {
                Color pixelColor = heightMap.GetPixel(x, y);
                float heightValue = pixelColor.r * 50;
                waterData.TerrainHeightMap[x, y] = heightValue;

                SetWater(x, y, 10);
            }
        }
    }

    public void SetWater(int x, int y, float value)
    {
        waterData.WaterHeightMap[x, y] = value;
    }

    public void SimulateWaterFlow()
    {
        float[,] newWaterHeightMap = new float[waterData.Width, waterData.Height];

        for (int x = 1; x < waterData.Width - 1; x++)
        {
            for (int y = 1; y < waterData.Height - 1; y++)
            {
                float totalHeight = waterData.TerrainHeightMap[x, y] + waterData.WaterHeightMap[x, y];

                float averageHeight = (
                    (waterData.TerrainHeightMap[x + 1, y] + waterData.WaterHeightMap[x + 1, y]) +
                    (waterData.TerrainHeightMap[x - 1, y] + waterData.WaterHeightMap[x - 1, y]) +
                    (waterData.TerrainHeightMap[x, y + 1] + waterData.WaterHeightMap[x, y + 1]) +
                    (waterData.TerrainHeightMap[x, y - 1] + waterData.WaterHeightMap[x, y - 1])
                ) / 4.0f;

                newWaterHeightMap[x, y] = waterData.WaterHeightMap[x, y] + (averageHeight - totalHeight) * 0.25f;
                if (newWaterHeightMap[x, y] < 0) newWaterHeightMap[x, y] = 0;
            }
        }

        for (int x = 0; x < waterData.Width; x++)
        {
            for (int y = 0; y < waterData.Height; y++)
            {
                waterData.WaterHeightMap[x, y] = newWaterHeightMap[x, y];
            }
        }
    }
}
