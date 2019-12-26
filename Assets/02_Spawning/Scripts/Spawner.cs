using Unity.Entities;

public struct Spawner : IComponentData
{
    public Entity prefab;
    public float maxDistanceFromSpawner;
    public float secondsBetweenSpawns;
    public float secondsToNextSpawn;
}