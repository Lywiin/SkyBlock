using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    public GameObject defaultCubePrefab;
    public GameObject debugQuad;

    [Header("Parameters")]
    public int3 terrainSize;
    public float terrainHeight;
    public float2 scale;
    public Vector2 offset;

    [Header("Filters")]
    public bool roundFilter;
    public AnimationCurve roundFilterCurve = new AnimationCurve(new Keyframe(0f, 1f) ,new Keyframe(0.5f, 0f), new Keyframe(1f, 0f));
    public bool thresholdFilterToggle;
    [Range(0f, 1f)] public float threshold;

    [Header("Generation")]
    public int seed;

    // Private
    private World world;
    private EntityManager manager;
    private BlobAssetStore blob;
    private Entity defaultCubeEntityPrefab;
    private GameObjectConversionSettings settings;

    private float[,] noise2DArray;
    private int cubeCount;

    // Cached
    private float maxDistanceFromCenter;

    // Instance
    public static GameManager Instance;




    /************************ MONOBEHAVIOUR ************************/

    private void Awake()
    {
        if (GameManager.Instance) Destroy(this);
        GameManager.Instance = this;

        UnityEngine.Random.InitState(seed);
    }
    
    private void Start()
    {
        // DOTS
        world = World.DefaultGameObjectInjectionWorld;
        manager = world.EntityManager;
        blob = new BlobAssetStore();
        settings = GameObjectConversionSettings.FromWorld(world, blob);
		defaultCubeEntityPrefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(defaultCubePrefab, settings);

        noise2DArray = new float[terrainSize.x, terrainSize.z];

        maxDistanceFromCenter = Utils.Distance(0f, 0f, terrainSize.x / 2f, terrainSize.z / 2f);

        GenerateTerrain();
    }

    private void OnDestroy() {
        if (blob != null) blob.Dispose();
    }


    /************************ TERRAIN ************************/

    public void RefreshSeed()
    {
        seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        UnityEngine.Random.InitState(seed);

        offset.x = UnityEngine.Random.Range(-1000000, 1000000);
        offset.y = UnityEngine.Random.Range(-1000000, 1000000);
    }

    private void DestroyTerrain()
    {
        manager.DestroyEntity(manager.CreateEntityQuery(typeof(CubeTag))); 
    }

    public void GenerateTerrain() 
    {
        DestroyTerrain();

        Generate2DNoise(new int2(terrainSize.x, terrainSize.z));

        InstantiateCubes();
    }

    private void InstantiateCubes()
    {
        // Debug.Log("CUBE COUNT: " + cubeCount);

        float3 newPos = float3.zero;
        float3 rootPos = (float3)transform.position;
        int index = 0;

        NativeArray<Entity> cubesArray = new NativeArray<Entity>(cubeCount, Allocator.TempJob);
        manager.Instantiate(defaultCubeEntityPrefab, cubesArray); 

        for (int x = 0; x < terrainSize.x ; x++)
		{
            for (int z = 0; z < terrainSize.z ; z++)
            {
                if (noise2DArray[x, z] == 0f) continue; // Don't spawn cube if value is 0

                // Set new position
                newPos.x = x;
                newPos.y = (int)(Utils.Remap01(noise2DArray[x, z], threshold, 1f) * terrainHeight);
                newPos.z = z;
                newPos += rootPos;

                manager.SetComponentData(cubesArray[index], new Translation { Value = newPos });
                index++;
            }
        }
        cubesArray.Dispose();
    }

    /************************ PERLIN ************************/

    private void Generate2DNoise(int2 size)
    {
        Texture2D noiseTexture = new Texture2D(size.x, size.y); // Debug
        cubeCount = 0;

        for (int x = 0; x < size.x ; x++)
		{
            for (int y = 0; y < size.y ; y++)
            {
                float noiseValue = GetPerlinValue2D(x, y);

                if (roundFilter) noiseValue = ApplyRound2DNoiseFilter(size, x, y, noiseValue);
                if (thresholdFilterToggle) noiseValue = ApplyThresholdFilter(noiseValue); 

                noise2DArray[x, y] = noiseValue;

                noiseTexture.SetPixel(x, y, new Color(noiseValue, noiseValue, noiseValue)); // Debug
                if (noiseValue != 0f) cubeCount++;
            }
        }

        noiseTexture.Apply();
        debugQuad.GetComponent<Renderer>().material.mainTexture = noiseTexture;// Debug
    }

    public float GetPerlinValue2D(float x, float y)
    {
        float xCoord = x / terrainSize.x * scale.x + offset.x;
        float yCoord = y / terrainSize.z * scale.y + offset.y;

        return Mathf.PerlinNoise(xCoord, yCoord);
    }

    private float ApplyRound2DNoiseFilter(int2 size, int x, int y, float value)
    {
        float distanceFromCenter = Utils.Distance(x, y, size.x / 2, size.y / 2);
        float distanceFromCenterNormalized = distanceFromCenter / maxDistanceFromCenter;
        float attenuationCoef = roundFilterCurve.Evaluate(distanceFromCenterNormalized);
        return value * attenuationCoef;
    }

    private float ApplyThresholdFilter(float value)
    {
        return value < threshold ? 0f : value;
    }
}
