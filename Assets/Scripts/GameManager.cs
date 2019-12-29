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
    public Vector3Int terrainSize;
    public float terrainHeight;
    public float2 scale;
    public Vector2 offset;
    [Range(0f, 1f)] public float threshold;

    [Header("Filters")]
    public bool roundFilter;
    public AnimationCurve roundFilterCurve = new AnimationCurve(new Keyframe(0f, 1f) ,new Keyframe(0.5f, 0f), new Keyframe(1f, 0f));

    // Private
    private World world;
    private EntityManager manager;
    private BlobAssetStore blob;
    private Entity defaultCubeEntityPrefab;
    private GameObjectConversionSettings settings;

    private float cubeSpacing;
    private List<float3> cubePositionArray;
    private float[,] noise2DArray;

    // Cached
    private float maxDistanceFromCenter;

    // Instance
    public static GameManager Instance;




    /************************ MONOBEHAVIOUR ************************/

    private void Awake()
    {
        if (GameManager.Instance) Destroy(this);
        GameManager.Instance = this;
    }
    
    private void Start()
    {
        // DOTS
        world = World.DefaultGameObjectInjectionWorld;
        manager = world.EntityManager;
        blob = new BlobAssetStore();
        settings = GameObjectConversionSettings.FromWorld(world, blob);
		defaultCubeEntityPrefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(defaultCubePrefab, settings);

        cubeSpacing = defaultCubePrefab.GetComponent<Renderer>().bounds.size.x;
        cubePositionArray = new List<float3>();
        noise2DArray = new float[terrainSize.x, terrainSize.z];

        maxDistanceFromCenter = Utils.Distance(0f, 0f, terrainSize.x / 2f, terrainSize.z / 2f);

        GenerateTerrain();
    }

    private void OnDestroy() {
        if (blob != null) blob.Dispose();
    }


    /************************ TERRAIN ************************/

    public void RefreshTerrain(bool random)
    {
        DestroyTerrain();
        
        if (random)
        {
            offset.x = UnityEngine.Random.Range(-1000000, 1000000);
            offset.y = UnityEngine.Random.Range(-1000000, 1000000);
        }

        GenerateTerrain();
    }

    private void DestroyTerrain()
    {
        manager.DestroyEntity(manager.CreateEntityQuery(typeof(Perlin2DMoveTag))); 
    }

    private void GenerateTerrain() 
    {
        Generate2DNoise(new int2(terrainSize.x, terrainSize.z));
        FillCubePositionArray();

        float3 newPos = float3.zero;
        float3 rootPos = (float3)transform.position;
        int cubeCount = cubePositionArray.Count;

        NativeArray<Entity> cubesArray = new NativeArray<Entity>(cubeCount, Allocator.TempJob);
        manager.Instantiate(defaultCubeEntityPrefab, cubesArray); 

        for (int i = 0; i < cubeCount; i++)
        {
            manager.SetComponentData(cubesArray[i], new Translation { Value = rootPos + cubePositionArray[i] });
        }

		cubesArray.Dispose();
    }

    /************************ PERLIN ************************/

    private void Generate2DNoise(int2 size)
    {
        Texture2D noiseTexture = new Texture2D(size.x, size.y); // Debug

        for (int x = 0; x < size.x ; x++)
		{
            for (int y = 0; y < size.y ; y++)
            {
                float noiseValue = GetPerlinValue2D(x, y);
                if (roundFilter) noiseValue = ApplyRound2DNoiseFilter(size, x, y, noiseValue);
                noise2DArray[x, y] = noiseValue;

                noiseTexture.SetPixel(x, y, new Color(noiseValue, noiseValue, noiseValue)); // Debug
            }
        }

        noiseTexture.Apply();
        debugQuad.GetComponent<Renderer>().material.mainTexture = noiseTexture;// Debug
    }

    private float ApplyRound2DNoiseFilter(int2 size, int x, int y, float value)
    {
        float distanceFromCenter = Utils.Distance(x, y, size.x / 2, size.y / 2);
        // float distanceFromCenterNormalized = Remap(distanceFromCenter, 0f, maxDistanceFromCenter, 0f, 1f);
        float distanceFromCenterNormalized = distanceFromCenter / maxDistanceFromCenter;
        float attenuationCoef = roundFilterCurve.Evaluate(distanceFromCenterNormalized);
        // Debug.Log(x + " " + y + ": " + distanceFromCenterNormalized);
        return value * attenuationCoef;
    }

    private void FillCubePositionArray()
    {
        cubePositionArray.Clear();

        for (int x = 0; x < terrainSize.x; x++)
		{
            for (int z = 0; z < terrainSize.z; z++)
            {
                float3 newPos = new float3(x, noise2DArray[x, z], z);
                if (newPos.y > threshold)
                {
                    newPos.y = (int)(Utils.Remap01(newPos.y, threshold, 1f) * terrainHeight);
                    cubePositionArray.Add(newPos * cubeSpacing);
                }
            }
		}
    }

    public float GetPerlinValue2D(float x, float y)
    {
        float xCoord = x / terrainSize.x * scale.x + offset.x;
        float yCoord = y / terrainSize.z * scale.y + offset.y;

        return Mathf.PerlinNoise(xCoord, yCoord);
    }
}
