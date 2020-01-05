using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;

public class WaveMoveSystem : JobComponentSystem
{
    // private struct WaveMoveJob : IJobForEach<WaveMoveData, Translation>
    // {
    //     private Translation originPosition;
    //     private float waveHeight;
    //     private float waveSpeed;

    //     public WaveMoveJob(Translation originPosition, float waveHeight, float waveSpeed) 
    //     {
    //         this.originPosition = originPosition;
    //         this.waveHeight = waveHeight;
    //         this.waveSpeed = waveSpeed;
    //     }

    //     public void Execute(ref WaveMoveData waveMoveData, ref Translation translation)
    //     {
    //         throw new System.NotImplementedException();
    //     }
    // }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        float elapsedTime = (float)Time.ElapsedTime;

        JobHandle waveMoveJobHandle = Entities.ForEach((ref Translation trans, in WaveMoveData data) =>
		{
			trans.Value.x = data.originPosition.x;
            trans.Value.y = data.originPosition.y + math.sin(elapsedTime * data.waveSpeed) * data.waveHeight;
			trans.Value.z = data.originPosition.z;
		}).Schedule(inputDeps);

        return waveMoveJobHandle;
    }
}
