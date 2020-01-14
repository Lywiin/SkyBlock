using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;

[BurstCompile]
public struct NoiseGeneratorJobParallel : IJobParallelFor
{
    [ReadOnly] public int3 terrainSize;
    [ReadOnly] public float3 terrainScale;
    [ReadOnly] public float3 terrainOffset;

    [WriteOnly]
    public NativeArray<float> noiseArray;

    public void Execute(int index)
    {
        float x = index / (terrainSize.y * terrainSize.z);
        float y = index / terrainSize.z % terrainSize.y;
        float z = index % terrainSize.z;

        float3 pos = new float3(x, y, z) / terrainSize * terrainScale + terrainOffset;

        float noiseValue = noise.cnoise(pos);
        noiseArray[index] = math.unlerp(-1,1, noiseValue);
    }
}

[BurstCompile]
public struct NoiseRoundFilterJobParallel : IJobParallelFor
{
    [ReadOnly] public int3 terrainSize;
    [ReadOnly] public float3 centerPos;
    [ReadOnly] public float maxDistance;

    public NativeArray<float> noiseArray;
    [ReadOnly] public float arrayMin;
    [ReadOnly] public float arrayMax;

    public void Execute(int index)
    {
        float x = index / (terrainSize.y * terrainSize.z);
        float y = index / terrainSize.z % terrainSize.y;
        float z = index % terrainSize.z;

        float3 currentPos = new float3(x, y, z);
        float distance = math.distance(currentPos, centerPos);
        float distanceNormalized = distance / maxDistance;
        // float remapedNoise = math.remap(arrayMin, 0f, arrayMax, 1f, noiseArray[index]);
        // if (distanceNormalized > 0.5f) {
            // distanceNormalized = math.unlerp(0.5f, 1f, distanceNormalized);
            float noiseNormalized = math.unlerp(arrayMin, arrayMax, noiseArray[index]);
            noiseArray[index] = noiseNormalized * (1 - distanceNormalized);
            // noiseArray[index] = 0f;
        // }
    }
}

[BurstCompile]
public struct NoiseThresholdJobParallel : IJobParallelFor
{
    [ReadOnly] public float threshold;
    [ReadOnly] public int3 terrainSize;

    [ReadOnly] public NativeArray<float> noiseArray;
    [ReadOnly] public float arrayMin;
    [ReadOnly] public float arrayMax;
    
    [WriteOnly] public NativeMultiHashMap<int3, float>.ParallelWriter positionHashSet;

    public void Execute(int index)
    {
        int x = index / (terrainSize.y * terrainSize.z);
        int y = index / terrainSize.z % terrainSize.y;
        int z = index % terrainSize.z;

        float noiseNormalized = math.unlerp(arrayMin, arrayMax, noiseArray[index]);
        // float noiseNormalized = noiseArray[index];

        if (noiseNormalized > threshold) positionHashSet.Add(new int3(x, y, z), noiseNormalized);
    }
}
