using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public GameObject defaultCubePrefab;
    public Vector3Int terrainSize;

    public float scale;
    public float scaleY;
    public Vector2 offset;

    private World world;
    private EntityManager manager;
    private BlobAssetStore blob;
    private Entity defaultCubeEntityPrefab;
    private GameObjectConversionSettings settings;

    private float cubeSpacing;

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

        GenerateTerrain();
    }

    private void OnDestroy() {
        if (blob != null) blob.Dispose();
    }

    private void GenerateTerrain() 
    {
        int totalAmount = terrainSize.x * terrainSize.y * terrainSize.z;
        float3 newPos = float3.zero;
        float3 rootPos = (float3)transform.position;
        int index = 0;

        NativeArray<Entity> cubes = new NativeArray<Entity>(totalAmount, Allocator.TempJob);
        manager.Instantiate(defaultCubeEntityPrefab, cubes); 

        for (float x = 0; x < terrainSize.x; x += cubeSpacing)
		{
			for (float y = 0; y < terrainSize.y; y += cubeSpacing)
			{
                for (float z = 0; z < terrainSize.z; z += cubeSpacing)
                {
                    newPos.x = x; newPos.y = 1f; newPos.z = z;

                    manager.SetComponentData(cubes[index], new Translation { Value = rootPos + newPos });

                    index++;
                }
			}
		}
		cubes.Dispose();
    }

    // public float GetPerlinValue2D(float x, float y)
    // {
    //     float xCoord = x / terrainSize.x * scale + offset.x;
    //     float yCoord = y / terrainSize.z * scale + offset.y;

    //     return Mathf.PerlinNoise(xCoord, yCoord);
    // }
}
