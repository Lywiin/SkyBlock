using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

public class RotateSystem : JobComponentSystem
{
    [BurstCompile]
    // protected override void OnUpdate()
    // {
    //     Entities.ForEach((ref Rotate rotate, ref RotationEulerXYZ euler) => {
    //         euler.Value.y += rotate.radiansPerSeconds * Time.DeltaTime;
    //     });
    // }

    private struct RotateJob : IJobForEach<RotationEulerXYZ, Rotate>
    {
        public float deltaTime;

        public void Execute(ref RotationEulerXYZ euler, ref Rotate rotate)
        {
            euler.Value.y += rotate.radiansPerSeconds * deltaTime;
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var job = new RotateJob { deltaTime = Time.DeltaTime };
        return job.Schedule(this, inputDeps);
    }
}
