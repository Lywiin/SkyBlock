using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class WaveMoveAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    [SerializeField] private float3 originPosition;
    [SerializeField] private float waveHeight;
    [SerializeField] private float waveSpeed;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new WaveMoveData
        {
            originPosition = originPosition,
            waveHeight = waveHeight,
            waveSpeed = waveSpeed
        });
    }
}
