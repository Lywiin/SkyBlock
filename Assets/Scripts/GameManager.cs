using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject defaultCubePrefab;

    [Header("Parameters")]
    public Vector3Int terrainSize;
    public float3 scale;
    public Vector2 offset;
    [Range(0f, 1f)] public float threshold;


    private World world;
    private EntityManager manager;
    private BlobAssetStore blob;
    private Entity defaultCubeEntityPrefab;
    private GameObjectConversionSettings settings;

    private float cubeSpacing;
    private List<float3> cubePositionArray;

    public static GameManager Instance;

    private void Awake()
    {
        if (GameManager.Instance) Destroy(this);
        GameManager.Instance = this;
    }
    
    private void Start()
    {
        world = World.DefaultGameObjectInjectionWorld;
        manager = world.EntityManager;
        blob = new BlobAssetStore();
        settings = GameObjectConversionSettings.FromWorld(world, blob);
		defaultCubeEntityPrefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(defaultCubePrefab, settings);

        cubeSpacing = defaultCubePrefab.GetComponent<Renderer>().bounds.size.x;
        cubePositionArray = new List<float3>();

        GenerateTerrain();
    }

    private void OnDestroy() {
        if (blob != null) blob.Dispose();
    }

    public void RefreshTerrain()
    {
        DestroyTerrain();
        GenerateTerrain();
    }

    private void DestroyTerrain()
    {
        manager.DestroyEntity(manager.CreateEntityQuery(typeof(Perlin2DMoveTag))); 
    }

    private void GenerateTerrain() 
    {
        float3 newPos = float3.zero;
        float3 rootPos = (float3)transform.position;

        FillPerlinResultArray();
        int cubeCount = cubePositionArray.Count;

        NativeArray<Entity> cubesArray = new NativeArray<Entity>(cubeCount, Allocator.TempJob);
        manager.Instantiate(defaultCubeEntityPrefab, cubesArray); 

        for (int i = 0; i < cubeCount; i++)
        {
            manager.SetComponentData(cubesArray[i], new Translation { Value = rootPos + cubePositionArray[i] });
        }

		cubesArray.Dispose();
    }

    private void FillPerlinResultArray()
    {
        cubePositionArray.Clear();

        for (float x = 0; x < terrainSize.x; x += cubeSpacing)
		{
            for (float z = 0; z < terrainSize.z; z += cubeSpacing)
            {
                float perlinValue = GetPerlinValue2D(x, z);
                if (perlinValue > threshold)
                {
                    perlinValue = Remap01(perlinValue, threshold, 1f);
                    cubePositionArray.Add(new float3(x, perlinValue * scale.y, z));
                }
            }
		}
    }

    public float GetPerlinValue2D(float x, float y)
    {
        float xCoord = x / terrainSize.x * scale.x + offset.x;
        float yCoord = y / terrainSize.z * scale.z + offset.y;

        return Mathf.PerlinNoise(xCoord, yCoord);
    }

    public float Remap01(float value, float from, float to) 
    {
        return (value - from) / (to - from);
    }
}
