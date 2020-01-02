// using Unity.Entities;
// using Unity.Jobs;
// using Unity.Mathematics;
// using Unity.Transforms;
// using Unity.Burst;
// using UnityEngine;

// public class Perlin2DMoveSystem : JobComponentSystem
// {
//     private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;

//     protected override void OnCreate() {
//         endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
//     }

//     [BurstCompile]
//     [RequireComponentTag(typeof(Perlin2DMoveTag))]
//     private struct Move2DJob : IJobForEachWithEntity<Translation>
//     {
//         public EntityCommandBuffer.Concurrent entityCommandBuffer;
//         public float3 scale;
//         public float2 offset;
//         public float2 terrainSize;

//         public void Execute(Entity entity, int index, ref Translation pos)
//         {
//             float noiseValue = GetPerlinValue2D(pos.Value.x, pos.Value.z);
//             // if (noiseValue < 0.5f)
//             // {
//             //     // pos.Value.y = 1500f;
//             //     entityCommandBuffer.DestroyEntity(index, entity);

//             // }
//             // else
//                 pos.Value.y = (int)(noiseValue * scale.y);
//             // float posY = GetPerlinValue2D(pos.Value.x, pos.Value.z) * scale.y;

//             // entityCommandBuffer.SetComponent(index, entity, new Translation
//             // {
//             //     Value = new float3(pos.Value.x, posY, pos.Value.z)
//             // });
//         }

//         public float GetPerlinValue2D(float x, float y)
//         {
//             float xCoord = x / terrainSize.x * scale.x + offset.x;
//             float yCoord = y / terrainSize.y * scale.z + offset.y;

//             return Mathf.PerlinNoise(xCoord, yCoord);
//         }
//     }

//     protected override JobHandle OnUpdate(JobHandle inputDeps)
//     {
//         // GameManager gameManager = GameManager.Instance;

//         // Move2DJob move2DJob = new Move2DJob
//         // {
//         //     entityCommandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
//         //     scale = new float3(gameManager.scale, gameManager.scaleY, gameManager.scale),
//         //     offset = (float2)gameManager.offset,
//         //     terrainSize = new float2(gameManager.terrainSize.x, gameManager.terrainSize.z)
//         // };

//         // JobHandle jobHandle = move2DJob.Schedule(this, inputDeps);

//         // endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(jobHandle);

//         // return jobHandle;
//         return inputDeps;
//     }
// }
