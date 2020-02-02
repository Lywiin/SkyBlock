using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;

[BurstCompile(CompileSynchronously = true)]
public struct NoiseGeneratorJobParallel : IJobParallelFor
{
    [ReadOnly] public int3 terrainSize;
    [ReadOnly] public float3 terrainScale;
    [ReadOnly] public float3 terrainOffset;
    [ReadOnly] public float threshold;
    [ReadOnly] public float3 centerPos;
    [ReadOnly] public float maxDistance;

    [WriteOnly] public NativeMultiHashMap<int, int3>.ParallelWriter index2DHashMap; // Replace with <int int>

    public void Execute(int index)
    {
        float x = index / (terrainSize.y * terrainSize.z);
        float y = (index / terrainSize.z) % terrainSize.y;
        float z = index % terrainSize.z;

        float3 currentPos = new float3(x, y, z);
        float3 scaledPos = currentPos / terrainSize * terrainScale + terrainOffset;

        float noiseValue = noise.cnoise(scaledPos);
        noiseValue = math.unlerp(-1,1, noiseValue);

        if (noiseValue >= threshold) 
        {
            float distance = math.distance(currentPos, centerPos);
            float distanceNormalized = distance / maxDistance;
            
            if (distanceNormalized < 0.5f)
            {
                index2DHashMap.Add((int)(x * terrainSize.x + z), new int3((int)x, (int)y, (int)z));
            }
        }
    }
}

[BurstCompile]
public struct NoiseMergeJobParallel : IJobParallelFor
{
    [ReadOnly] public int3 terrainSize;
    [ReadOnly] public NativeMultiHashMap<int, int3> index2DHashMap;

    [WriteOnly] public NativeHashMap<int3, bool>.ParallelWriter topPositionHashMap;
    [WriteOnly] public NativeHashMap<int3, bool>.ParallelWriter positionHashMap;

    public void Execute(int index)
    {
        if (index2DHashMap.ContainsKey(index)) 
        {
            int3 oldPos = 0;
            NativeMultiHashMapIterator<int> it; // while on this ???
            if(index2DHashMap.TryGetFirstValue(index, out oldPos, out it))
            {
                int y = terrainSize.y - 1;

                for (int i = 0; i < index2DHashMap.CountValuesForKey(index); i++)
                {
                    if (i == 0) 
                        topPositionHashMap.TryAdd(new int3(oldPos.x, y, oldPos.z), true);
                    else 
                        positionHashMap.TryAdd(new int3(oldPos.x, y, oldPos.z), true);

                    y--;
                }

            }
        }
    }
}

// [BurstCompile]
// public struct NoiseTrimJobParallel : IJobParallelFor
// {
//     [ReadOnly] public int3 terrainSize;
//     [ReadOnly] public float2 centerPos;
//     [ReadOnly] public float maxDistance;

//     public NativeArray<int> yCountArray;

//     public void Execute(int index)
//     {
//         float x = index / terrainSize.z;
//         float z = index % terrainSize.z;

//         float2 currentPos = new float2(x, z);
//         float distance = math.distance(currentPos, centerPos);
//         float distanceNormalized = distance / maxDistance;

//         yCountArray[index] = (int)(yCountArray[index] * (1 - distanceNormalized));
//     }
// }

// [BurstCompile]
// public struct NoiseRoundFilterJobParallel : IJobParallelFor
// {
//     [ReadOnly] public int3 terrainSize;
//     [ReadOnly] public float3 centerPos;
//     [ReadOnly] public float maxDistance;

//     public NativeArray<float> noiseArray;
//     [ReadOnly] public float arrayMin;
//     [ReadOnly] public float arrayMax;

//     public void Execute(int index)
//     {
//         float x = index / (terrainSize.y * terrainSize.z);
//         float y = index / terrainSize.z % terrainSize.y;
//         float z = index % terrainSize.z;

//         float3 currentPos = new float3(x, y, z);
//         float distance = math.distance(currentPos, centerPos);
//         float distanceNormalized = distance / maxDistance;
//         // float remapedNoise = math.remap(arrayMin, 0f, arrayMax, 1f, noiseArray[index]);
//         // if (distanceNormalized > 0.5f) {
//             // distanceNormalized = math.unlerp(0.5f, 1f, distanceNormalized);
//             float noiseNormalized = math.unlerp(arrayMin, arrayMax, noiseArray[index]);
//             noiseArray[index] = noiseNormalized * (1 - distanceNormalized);
//             // noiseArray[index] = 0f;
//         // }
//     }
// }

// [BurstCompile]
// public struct NoiseThresholdJobParallel : IJobParallelFor
// {
//     [ReadOnly] public float threshold;
//     [ReadOnly] public int3 terrainSize;

//     [ReadOnly] public NativeArray<float> noiseArray;
//     [ReadOnly] public float arrayMin;
//     [ReadOnly] public float arrayMax;
    
//     [WriteOnly] public NativeMultiHashMap<int3, float>.ParallelWriter positionHashSet;

//     public void Execute(int index)
//     {
//         int x = index / (terrainSize.y * terrainSize.z);
//         int y = index / terrainSize.z % terrainSize.y;
//         int z = index % terrainSize.z;

//         float noiseNormalized = math.unlerp(arrayMin, arrayMax, noiseArray[index]);
//         // float noiseNormalized = noiseArray[index];

//         if (noiseNormalized > threshold) positionHashSet.Add(new int3(x, y, z), noiseNormalized);
//     }
// }
