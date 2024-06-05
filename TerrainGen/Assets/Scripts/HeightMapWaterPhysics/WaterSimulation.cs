using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
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

            }
        }
                SetWater(10, 10, 10);
    }

    public void SetWater(int x, int y, float value)
    {
        waterData.WaterHeightMap[x, y] = value;
    }

    public void SimulateWaterFlow()
    {
        SetWater(10, 10, 100);
        float[,] newWaterHeightMap = new float[waterData.Width, waterData.Height];

        // Initialize newWaterHeightMap with the current water levels
        for (int x = 0; x < waterData.Width; x++)
        {
            for (int y = 0; y < waterData.Height; y++)
            {
                newWaterHeightMap[x, y] = waterData.WaterHeightMap[x, y];
            }
        }

        for (int x = 0; x < waterData.Width; x++)
        {
            for (int y = 0; y < waterData.Height; y++)
            {
                if (waterData.WaterHeightMap[x, y] > 0)
                {
                    float currentHeight = waterData.TerrainHeightMap[x, y] + waterData.WaterHeightMap[x, y];
                    int bestNeighborX = -1;
                    int bestNeighborY = -1;
                    float maxPotentialFlow = 0;

                    // Check neighboring cells
                    int[,] directions = { { -1, 0 }, { 1, 0 }, { 0, -1 }, { 0, 1 } };

                    for (int i = 0; i < directions.GetLength(0); i++)
                    {
                        int nx = x + directions[i, 0];
                        int ny = y + directions[i, 1];

                        if (nx >= 0 && nx < waterData.Width && ny >= 0 && ny < waterData.Height)
                        {
                            float neighborHeight = waterData.TerrainHeightMap[nx, ny] + waterData.WaterHeightMap[nx, ny];
                            float potentialFlow = currentHeight - neighborHeight;

                            if (potentialFlow > maxPotentialFlow)
                            {
                                maxPotentialFlow = potentialFlow;
                                bestNeighborX = nx;
                                bestNeighborY = ny;
                            }
                        }
                    }

                    // Move water to the best neighbor
                    if (bestNeighborX != -1 && bestNeighborY != -1)
                    {
                        float flowAmount = Mathf.Min(waterData.FlowRate, maxPotentialFlow / 2);
                        newWaterHeightMap[x, y] -= flowAmount;
                        newWaterHeightMap[bestNeighborX, bestNeighborY] += flowAmount;
                    }
                }
            }
        }

        // Update water levels
        for (int x = 0; x < waterData.Width; x++)
        {
            for (int y = 0; y < waterData.Height; y++)
            {
                waterData.WaterHeightMap[x, y] = newWaterHeightMap[x, y];
            }
        }
    }


}


