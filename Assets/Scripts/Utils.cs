using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public static class Utils
{
    public static float Remap01(float value, float from, float to) 
    {
        return (value - from) / (to - from);
    }

    public static float Remap(float value, float from1, float to1, float from2, float to2) 
    {
        return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
    }

    public static float Distance(float x1, float y1, float x2, float y2)
    {
        return ((x1-x2)*(x1-x2)+(y1-y2)*(y1-y2));
    }

    public static HashSet<T> ShuffleListToHashSet<T>(ref List<T> inputList)
    {
        HashSet<T> suffledHashset = new HashSet<T>();
        Unity.Mathematics.Random rng = GameManager.Instance.Rng;

        int randomIndex = 0;
        while (inputList.Count > 0)
        {
            randomIndex = rng.NextInt(0, inputList.Count);
            suffledHashset.Add(inputList[randomIndex]);
            inputList.RemoveAt(randomIndex);
        }

        return suffledHashset;
    }

    public static void ShuffleListToHashSet<T>(ref List<T> inputList, ref HashSet<T>[] outHashsetArray)
    {
        Unity.Mathematics.Random rng = GameManager.Instance.Rng;

        int randomIndex = 0;
        while (inputList.Count > 0)
        {
            randomIndex = rng.NextInt(0, inputList.Count);

            for (int i = 0; i < outHashsetArray.Length; i++)
                outHashsetArray[i].Add(inputList[randomIndex]);

            inputList.RemoveAt(randomIndex);
        }
    }

    private static Stack<float> startTime = new Stack<float>();

    public static void StartTimer()
    {
        startTime.Push(Time.realtimeSinceStartup);
    }

    public static float EndTimer(string name = "", string color = "yellow")
    {
        float totalTime = Time.realtimeSinceStartup - (float)startTime.Pop();
        Debug.Log("<color=" + color + "> ========= TIME ELAPSED " + name + ": " + (totalTime).ToString("F8") + "s</color>");
        return totalTime;
    }
}
