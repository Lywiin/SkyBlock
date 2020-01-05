// using System.Collections.Generic;
// using Unity.Entities;
// using UnityEngine;
// using Unity.Mathematics;

// public class TerrainGeneratorAuthoring : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
// {
//     [SerializeField] private GameObject prefab;
//     [SerializeField] private float spawnRate;
//     [SerializeField] private float maxDistanceFromSpawner;
//     [SerializeField] private int maxSpawnCount;
//     [SerializeField] private float3 terrainSize;

//     public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
//     {
//         referencedPrefabs.Add(prefab);
//     }

//     public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
//     {
//         dstManager.AddComponentData(entity, new TerrainGeneratorData
//         {
//             prefab = conversionSystem.GetPrimaryEntity(prefab),
//             maxDistanceFromSpawner = maxDistanceFromSpawner,
//             secondsBetweenSpawns = 1 / spawnRate,
//             secondsToNextSpawn = 0f,
//             // maxSpawnCount = maxSpawnCount,
//             // spawnCount = 0,
//             terrainSize = terrainSize,
//             nextSpawnPos = new float3()
//         });
//     }
// }
