using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

public class TerrainGeneratorSystem : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;

    protected override void OnCreate() {
        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }
    // EndSimulationEntityCommandBufferSystem m_EndSimulationEcbSystem;

    // protected override void OnCreate()
    // {
    //     base.OnCreate();
    //     // Find the ECB system once and store it for later usage
    //     m_EndSimulationEcbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    // }

    // protected override JobHandle OnUpdate(JobHandle inputDeps)
    // {
    //     var ecb = m_EndSimulationEcbSystem.CreateCommandBuffer().ToConcurrent();
    //     Random random = new Random((uint)UnityEngine.Random.Range(0, int.MaxValue));
    //     float deltaTime = Time.DeltaTime;
        
    //     JobHandle jobHandle = Entities
    //     .WithoutBurst()
    //     .ForEach((Entity entity, int entityInQueryIndex, ref TerrainGeneratorData data, in LocalToWorld localToWorld) => 
    //     {
    //         data.secondsToNextSpawn -= deltaTime;

    //         if (data.secondsToNextSpawn >= 0) { return; }
            
    //         data.secondsToNextSpawn += data.secondsBetweenSpawns;

    //         Entity instance = ecb.Instantiate(entityInQueryIndex, data.prefab);
    //         ecb.SetComponent(entityInQueryIndex, instance, new Translation
    //         {
    //             Value = localToWorld.Position + random.NextFloat3Direction() * random.NextFloat() * data.maxDistanceFromSpawner
    //         });
    //     }).Schedule(inputDeps);

    //     m_EndSimulationEcbSystem.AddJobHandleForProducer(jobHandle);
    //     return jobHandle;
    // }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var terrainGeneratorJob = new TerrainGeneratorJob(
            endSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            new Random((uint)UnityEngine.Random.Range(0, int.MaxValue)),
            Time.DeltaTime
        );

        JobHandle jobHandle = terrainGeneratorJob.Schedule(this, inputDeps);

        endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(jobHandle);
        return jobHandle;
    }

    private struct TerrainGeneratorJob : IJobForEachWithEntity<TerrainGeneratorData, LocalToWorld>
    {
        private EntityCommandBuffer.Concurrent ecb;
        private Random random;
        private readonly float deltaTime;

        public TerrainGeneratorJob(EntityCommandBuffer.Concurrent ecb, Random random, float deltaTime) 
        {
            this.ecb = ecb;
            this.random = random;
            this.deltaTime = deltaTime;
        }

        public void Execute(Entity entity, int index, ref TerrainGeneratorData tgData, [ReadOnly] ref LocalToWorld localToWorld)
        {
            // tgData.secondsToNextSpawn -= deltaTime;
            // if (tgData.secondsToNextSpawn >= 0) { return; }
            // tgData.secondsToNextSpawn += tgData.secondsBetweenSpawns;

            // if (tgData.spawnCount >= tgData.maxSpawnCount) { return; }

            if (tgData.nextSpawnPos.x > tgData.terrainSize.x 
            // && tgData.nextSpawnPos.y >= tgData.terrainSize.y)
            || tgData.nextSpawnPos.z > tgData.terrainSize.z) { return; }

            Entity instance = ecb.Instantiate(index, tgData.prefab);
            ecb.SetComponent(index, instance, new Translation
            {
                // Value = localToWorld.Position + random.NextFloat3Direction() * random.NextFloat() * tgData.maxDistanceFromSpawner
                Value = localToWorld.Position + tgData.nextSpawnPos
            });

            if (tgData.nextSpawnPos.x < tgData.terrainSize.x) 
            {
                tgData.nextSpawnPos.x++;
            } else 
            {
                tgData.nextSpawnPos.z++;
                tgData.nextSpawnPos.x = 0;
            }

            // tgData.spawnCount++;
        }
    }
}
