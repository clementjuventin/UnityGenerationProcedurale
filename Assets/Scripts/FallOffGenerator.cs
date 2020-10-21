using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class FallOffGenerator
{
    public static float[,] GenerateFallOfMap(int size)
    {
        float[,] map = new float[size, size];

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                float x = i / (float) size * 2 - 1;
                float y = j / (float) size * 2 - 1;

                float value = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));

                map[i, j] = Evaluate(value);
            }
        }
        return map;
    }

    public static float Evaluate(float value)
    {
        return Mathf.Pow(value, MapGenerator.aCoeffForFallOfMap) / (Mathf.Pow(value, MapGenerator.aCoeffForFallOfMap) + Mathf.Pow(MapGenerator.bCoeffForFallOfMap - MapGenerator.bCoeffForFallOfMap * value, MapGenerator.aCoeffForFallOfMap));
    }
}

