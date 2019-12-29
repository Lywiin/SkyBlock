using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
}
