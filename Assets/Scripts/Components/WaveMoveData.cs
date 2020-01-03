using Unity.Entities;
using Unity.Mathematics;

public struct WaveMoveData : IComponentData
{
    public float3 originPosition;
    public float waveHeight;
    public float waveSpeed;
}
