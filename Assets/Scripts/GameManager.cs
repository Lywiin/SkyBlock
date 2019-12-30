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
    public GameObject[] cubePrefabArray;
    // public GameObject defaultCubePrefab;
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
    // private Entity defaultCubeEntityPrefab;
    private Entity[] cubePrefabEntityArray;
    private GameObjectConversionSettings settings;

    // private float[,] noise2DArray;
    private CubePosition[,] cubePositionArray;
    // private int cubeCount;
    private int[] cubeCount;

    // Cached
    private float maxDistanceFromCenter;
    private List<int2>[] spawnPositionListArray;

    // Instance
    public static GameManager Instance;


    public struct CubePosition
    {
        public bool exist;
        public bool available;
        public float3 pos;
        public int cubePrefabIndex;

        public CubePosition(bool exist, bool available, float3 pos, int cubePrefabIndex)
        {
            this.exist = exist;
            this.available = available;
            this.pos = pos;
            this.cubePrefabIndex = cubePrefabIndex;
        }
    }

    /************************ MONOBEHAVIOUR ************************/

    private void Awake()
    {
        if (GameManager.Instance) Destroy(this);
        GameManager.Instance = this;

        InitSeed();
    }
    
    private void Start()
    {
        // DOTS
        world = World.DefaultGameObjectInjectionWorld;
        manager = world.EntityManager;
        blob = new BlobAssetStore();
        settings = GameObjectConversionSettings.FromWorld(world, blob);

        // cubePrefabEntityArray = new Entity[cubePrefabArray.Length];
        // for (int i = 0; i < cubePrefabEntityArray.Length; i++)
        //     cubePrefabEntityArray[i] = GameObjectConversionUtility.ConvertGameObjectHierarchy(cubePrefabArray[i], settings);
		// // defaultCubeEntityPrefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(defaultCubePrefab, settings);

        // // noise2DArray = new float[terrainSize.x, terrainSize.z];
        // cubePositionArray = new CubePosition[terrainSize.x, terrainSize.z];
        // cubeCount = new int[cubePrefabArray.Length];

        // maxDistanceFromCenter = Utils.Distance(0f, 0f, terrainSize.x / 2f, terrainSize.z / 2f);
        // // availableSpawnPositionList = new List<int2>();
        // spawnPositionListArray = new List<int2>[cubePrefabArray.Length];
        // for (int i = 0; i < spawnPositionListArray.Length; i++)
        //     spawnPositionListArray[i] = new List<int2>();

        // GenerateTerrain();
    }

    private void OnDestroy() {
        if (blob != null) blob.Dispose();
    }


    /************************ TERRAIN ************************/

    public void RefreshSeed()
    {
        seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
    }

    public void InitSeed()
    {
        UnityEngine.Random.InitState(seed);

        offset.x = UnityEngine.Random.Range(-1000000, 1000000);
        offset.y = UnityEngine.Random.Range(-1000000, 1000000);
    }

    private void DestroyTerrain()
    {
        manager.DestroyEntity(manager.CreateEntityQuery(typeof(CubeTag))); 
        
        for (int i = 0; i < cubeCount.Length; i++)
            cubeCount[i] = 0;
    }

    public void GenerateTerrain() 
    {
        cubePrefabEntityArray = new Entity[cubePrefabArray.Length];
        for (int i = 0; i < cubePrefabEntityArray.Length; i++)
            cubePrefabEntityArray[i] = GameObjectConversionUtility.ConvertGameObjectHierarchy(cubePrefabArray[i], settings);

        spawnPositionListArray = new List<int2>[cubePrefabArray.Length];
        for (int i = 0; i < spawnPositionListArray.Length; i++)
            spawnPositionListArray[i] = new List<int2>();

        cubePositionArray = new CubePosition[terrainSize.x, terrainSize.z];
        cubeCount = new int[cubePrefabArray.Length];

        maxDistanceFromCenter = Utils.Distance(0f, 0f, terrainSize.x / 2f, terrainSize.z / 2f);

        int2 terrainSize2D = new int2(terrainSize.x, terrainSize.z);

        DestroyTerrain();
        Generate2DNoise(terrainSize2D);
        ScaleCubes(terrainSize2D);
        InstantiateCubes();
    }

    private void InstantiateCubes()
    {
        // for (int i = 0; i < cubeCount.Length; i++)
        //     Debug.Log("CUBE COUNT " + i + ": " + cubeCount[i]);

        float3 newPos = float3.zero;
        float3 rootPos = (float3)transform.position;
        // int index = 0;
        int[] indexes = new int[cubePrefabArray.Length];

        NativeArray<Entity>[] cubeEntitiesArrayArray = new NativeArray<Entity>[cubeCount.Length];

        for (int i = 0; i < cubeEntitiesArrayArray.Length; i++)
        {
            cubeEntitiesArrayArray[i] = new NativeArray<Entity>(cubeCount[i], Allocator.TempJob);
            manager.Instantiate(cubePrefabEntityArray[i], cubeEntitiesArrayArray[i]);
        }

        // NativeArray<Entity> cubesArray = new NativeArray<Entity>(cubeCount, Allocator.TempJob);
        // manager.Instantiate(defaultCubeEntityPrefab, cubesArray); 

        for (int x = 0; x < terrainSize.x ; x++)
		{
            for (int z = 0; z < terrainSize.z ; z++)
            {
                // if (noise2DArray[x, z] == 0f) continue; // Don't spawn cube if value is 0
                if (!cubePositionArray[x, z].exist) continue;

                // // Set new position
                // newPos.x = x;
                // newPos.y = (int)(Utils.Remap01(noise2DArray[x, z], threshold, 1f) * terrainHeight);
                // newPos.z = z;
                // newPos += rootPos;

                // manager.SetComponentData(cubesArray[index], new Translation { Value = cubePositionArray[x, z].pos + rootPos });

                int cubePrefabIndex = cubePositionArray[x, z].cubePrefabIndex;
                Entity entity = cubeEntitiesArrayArray[cubePrefabIndex][indexes[cubePrefabIndex]];
                manager.SetComponentData(entity, new Translation { Value = cubePositionArray[x, z].pos + rootPos });
                indexes[cubePrefabIndex]++;
            }
        }
        // cubesArray.Dispose();
        for (int i = 0; i < cubeEntitiesArrayArray.Length; i++)
        {
            cubeEntitiesArrayArray[i].Dispose();
        }
    }

    /************************ NOISE ************************/

    private void Generate2DNoise(int2 size)
    {
        Texture2D noiseTexture = new Texture2D(size.x, size.y); // Debug
        // cubeCount = 0;

        for (int x = 0; x < size.x ; x++)
		{
            for (int y = 0; y < size.y ; y++)
            {
                float noiseValue = GetPerlinValue2D(x, y);

                if (roundFilter) noiseValue = ApplyRound2DNoiseFilter(size, x, y, noiseValue);
                if (thresholdFilterToggle) noiseValue = ApplyThresholdFilter(noiseValue); 

                // noise2DArray[x, y] = noiseValue;
                AddCubePosition(x, y, noiseValue);

                noiseTexture.SetPixel(x, y, new Color(noiseValue, noiseValue, noiseValue)); // Debug
                // if (noiseValue != 0f) cubeCount++;
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

    /************************ CUBE POSITION ************************/

    private void ScaleCubes(int2 size)
    {
        int currentCubePrefabIndex = cubePrefabArray.Length - 1;

        FillAvailableSpawnPositionList(size, currentCubePrefabIndex);
        
        while (currentCubePrefabIndex > 0)
        {
            FillAvailableSpawnPositionList(size, currentCubePrefabIndex - 1);
            
            while (spawnPositionListArray[currentCubePrefabIndex].Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, spawnPositionListArray[currentCubePrefabIndex].Count);
                int2 pickedSpawnPosition = spawnPositionListArray[currentCubePrefabIndex][randomIndex];

                SetCubePositionPrefabIndex(ref cubePositionArray[pickedSpawnPosition.x, pickedSpawnPosition.y], currentCubePrefabIndex);
                RemoveSurroundingCubePosition(pickedSpawnPosition.x, pickedSpawnPosition.y, currentCubePrefabIndex); // Remove 3x3 square for index == 1
            }

            currentCubePrefabIndex--;
        }

        cubeCount[0] = spawnPositionListArray[0].Count;

        for (int i = 0; i < cubeCount.Length; i++)
            Debug.Log("CUBE COUNT " + i + ": " + cubeCount[i]);
    }

    // private void ScaleCubes(int2 size)
    // {
    //     int currentCubePrefabIndex = cubePrefabArray.Length - 1;

    //     while (currentCubePrefabIndex > 0)
    //     {
    //         FillAvailableSpawnPositionList(size);

    //         while (availableSpawnPositionList.Count > 0)
    //         {
    //             if (availableSpawnPositionList.Count % 100 == 0) Debug.Log("AVAILABLE SPAWN POSITION LEFT: " + availableSpawnPositionList);
    //             int2 pickedSpawnPosition = availableSpawnPositionList[UnityEngine.Random.Range(0, availableSpawnPositionList.Count)];

    //             SetCubePositionPrefabIndex(ref cubePositionArray[pickedSpawnPosition.x, pickedSpawnPosition.y], currentCubePrefabIndex);
    //             RemoveSurroundingCubePosition(pickedSpawnPosition.x, pickedSpawnPosition.y, currentCubePrefabIndex); // Remove 3x3 square for index == 1
    //         }

    //         currentCubePrefabIndex--;
    //     }

    //     FillAvailableSpawnPositionList(size);
    //     cubeCount[0] = availableSpawnPositionList.Count;

    //     for (int i = 0; i < cubeCount.Length; i++)
    //         Debug.Log("CUBE COUNT " + i + ": " + cubeCount[i]);
    // }

    private void FillAvailableSpawnPositionList(int2 size, int index)
    {
        spawnPositionListArray[index].Clear();

        if (index == spawnPositionListArray.Length - 1)
        {
            for (int x = 0; x < size.x ; x++)
                for (int y = 0; y < size.y ; y++)
                {
                    if (cubePositionArray[x, y].available) spawnPositionListArray[index].Add(new int2(x, y));
                }
        } 
        else
        {
            for (int i = 0; i < spawnPositionListArray[index + 1].Count ; i++)
                spawnPositionListArray[index].Add(spawnPositionListArray[index + 1][i]);
        }

    }

    private void AddCubePosition(int x, int y, float noiseValue)
    {
        if (noiseValue != 0f)
        {
            float3 pos = new float3(x, (int)(Utils.Remap01(noiseValue, threshold, 1f) * terrainHeight), y);
            cubePositionArray[x, y] = new CubePosition(true, true, pos, 0);
            // cubeCount++;
            // cubeCount[0]++; // DEBUG PURPOSE
        } else
        {
            cubePositionArray[x, y] = new CubePosition(false, false, 0f, 0);
        }
    }

    private void ResetCubePosition(int x, int y)
    {
        cubePositionArray[x, y].exist = false;
        cubePositionArray[x, y].available = false;
        cubePositionArray[x, y].pos = 0f;
        cubePositionArray[x, y].cubePrefabIndex = 0;
    }

    private void RemoveSurroundingCubePosition(int centerX, int centerY, int radius)
    {
        int doubleRadius = radius * 2;
        for (int x = centerX - doubleRadius; x <= centerX + doubleRadius ; x++)
            for (int y = centerY - doubleRadius; y <= centerY + doubleRadius ; y++)
            {
                if (x < 0 || y < 0 || x >= terrainSize.x || y >= terrainSize.z) continue;

                spawnPositionListArray[radius].Remove(new int2(x, y));

                if (x >= centerX - radius && x <= centerX + radius && y >= centerY - radius && y <= centerY + radius)
                {
                    spawnPositionListArray[radius - 1].Remove(new int2(x, y));
                    if (x != centerX || y != centerY) ResetCubePosition(x, y);
                }
            }
    }

    private void SetCubePositionPrefabIndex(ref CubePosition cubePosition, int currentCubePrefabIndex)
    {
        cubePosition.exist = true;
        cubePosition.available = false;
        cubePosition.cubePrefabIndex = currentCubePrefabIndex;

        cubeCount[currentCubePrefabIndex]++;
    }
}
