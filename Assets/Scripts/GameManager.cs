using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("UI")]
    public UnityEngine.UI.Text text;
    public UnityEngine.UI.RawImage noiseImage;

    [Header("References")]
    public GameObject[] cubePrefabArray;

    [Header("Parameters")]
    public int3 terrainSize;
    public float terrainHeight;
    public float2 noiseScale;
    public float waveHeight;
    public float waveSpeed;
    [Range(0f, 1f)] public float splitChance;
    public bool meanNoiseB;
    public int terasseHeight = 1;

    [Header("Filters")]
    public bool roundFilter;
    public AnimationCurve roundFilterCurve = new AnimationCurve(new Keyframe(0f, 1f) ,new Keyframe(0.5f, 0f), new Keyframe(1f, 0f));
    public bool thresholdFilterToggle;
    [Range(0f, 1f)] public float threshold;

    [Header("Generation")]
    public uint seed = 1;
    [HideInInspector] public Unity.Mathematics.Random rng;

    // DOTS
    private World world;
    private EntityManager manager;
    private BlobAssetStore blob;
    private Entity[] cubePrefabEntityArray;
    private GameObjectConversionSettings settings;

    // Perlin
    private float2 offset;
    private float maxDistanceFromCenter;
    
    // Instantiate cubes
    private float[,] noise2DArray;
    private int[,] finalPrefabIndex2DArray;
    private HashSet<int2> tempAvailablePositionHashSet;
    private HashSet<int2> nextTempAvailablePositionHashSet;
    private int[] cubeCount;

    private int smallerCubeSize;
    private int biggerCubeSize;


    // Instance
    public static GameManager Instance;


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

        tempAvailablePositionHashSet = new HashSet<int2>();
        nextTempAvailablePositionHashSet = new HashSet<int2>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            GameManager.Instance.InitSeed();
            GameManager.Instance.GenerateTerrain();
        }
    }

    private void OnDestroy() {
        if (blob != null) blob.Dispose();
    }


    /************************ TERRAIN ************************/

    // Allocation of all variable changeable at each generation
    private void Initialize()
    {
        cubePrefabEntityArray = new Entity[cubePrefabArray.Length];
        for (int i = 0; i < cubePrefabEntityArray.Length; i++)
            cubePrefabEntityArray[i] = GameObjectConversionUtility.ConvertGameObjectHierarchy(cubePrefabArray[i], settings);

        noise2DArray = new float[terrainSize.x, terrainSize.z];
        finalPrefabIndex2DArray = new int[terrainSize.x, terrainSize.z];

        cubeCount = new int[cubePrefabArray.Length];

        maxDistanceFromCenter = Utils.Distance(0f, 0f, terrainSize.x / 2f, terrainSize.z / 2f);

        smallerCubeSize = (int)cubePrefabArray[0].GetComponent<Renderer>().bounds.size.x;
        biggerCubeSize = (int)cubePrefabArray[cubePrefabArray.Length - 1].GetComponent<Renderer>().bounds.size.x;
    }

    public void RefreshSeed()
    {
        // seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        seed = rng.NextUInt(uint.MinValue, uint.MaxValue);

        InitSeed();
    }

    public void InitSeed()
    {
        // UnityEngine.Random.InitState(seed);

        // offset.x = UnityEngine.Random.Range(-1000000, 1000000);
        // offset.y = UnityEngine.Random.Range(-1000000, 1000000);

        rng = new Unity.Mathematics.Random(seed);

        offset.x = rng.NextInt(1, 2000000);
        offset.y = rng.NextInt(1, 2000000);
    }

    private void DestroyTerrain()
    {
        manager.DestroyEntity(manager.CreateEntityQuery(typeof(Translation))); 

        tempAvailablePositionHashSet.Clear();
        nextTempAvailablePositionHashSet.Clear();
        
        for (int i = 0; i < cubeCount.Length; i++)
            cubeCount[i] = 0;
    }

    public void GenerateTerrain() 
    {
        Initialize();
        DestroyTerrain();

        int2 terrainSize2D = new int2(terrainSize.x, terrainSize.z);

        // Debug.Log("<color=yellow> =============================== </color>");
        Utils.StartTimer();
        Generate2DNoise(terrainSize2D);
        float time1 = Utils.EndTimer("Generate2DNoise");

        Utils.StartTimer();
        // ScaleCubes(terrainSize2D);
        // float time2 = Utils.EndTimer("ScaleCubes");
        SetCubePrefabsAtPositions();
        float time2 = Utils.EndTimer("SetCubePrefabsAtPositions");

        Utils.StartTimer();
        InstantiateCubes();
        float time3 = Utils.EndTimer("InstantiateCubes");


        float totalTime = time1 + time2 + time3;
        Debug.Log("<color=yellow> TIME ELAPSED TOTAL: " + totalTime.ToString("F2") + "s</color>");
        text.text = totalTime.ToString();

        int totalCount = 0;
        for (int i = 0; i < cubeCount.Length; i++)
        {
            totalCount += cubeCount[i];
        }
        Debug.Log("<color=yellow> TOTAL CUBE COUNT: " + totalCount + "</color>");

        Debug.Log("<color=orange> =============================== </color>");
    }

    private void InstantiateCubes()
    {
        // string s = "";
        // for (int x = 0; x < terrainSize.x ; x++)
        // {
        //     for (int z = 0; z < terrainSize.z ; z++)
        //     {
        //         s += finalPrefabIndex2DArray[x, z] + "\t";
        //     }
        //     s += "\n";
        // }
        // Debug.Log(s);

        // for (int i = 0; i < cubeCount.Length; i++)
        //     Debug.Log(cubeCount[i]);

        for (int x = 0; x < terrainSize.x ; x++)
            for (int z = 0; z < terrainSize.z ; z++)
                if (finalPrefabIndex2DArray[x, z] != -1) cubeCount[finalPrefabIndex2DArray[x, z]]++;

        float3 newPos = float3.zero;
        float3 rootPos = (float3)transform.position;
        int[] indexes = new int[cubePrefabArray.Length];

        NativeArray<Entity>[] cubeEntitiesArrayArray = new NativeArray<Entity>[cubeCount.Length];

        for (int i = 0; i < cubeEntitiesArrayArray.Length; i++)
        {
            cubeEntitiesArrayArray[i] = new NativeArray<Entity>(cubeCount[i], Allocator.TempJob);
            manager.Instantiate(cubePrefabEntityArray[i], cubeEntitiesArrayArray[i]);
        }

        for (int x = 0; x < terrainSize.x ; x++)
		{
            for (int z = 0; z < terrainSize.z ; z++)
            {
                if (finalPrefabIndex2DArray[x, z] == -1) continue;
                int cubePrefabIndex = finalPrefabIndex2DArray[x, z];

                Entity entity = cubeEntitiesArrayArray[cubePrefabIndex][indexes[cubePrefabIndex]];

                float meanNoise = 0f;
                if (meanNoiseB)
                {
                    float power = Mathf.Pow(2, cubePrefabIndex);
                    for (int x2 = x; x2 < x + power ; x2++)
                        for (int z2 = z; z2 < z + power ; z2++)
                            meanNoise += noise2DArray[x2, z2];
                    meanNoise /= power * power;
                } else 
                {
                    meanNoise = noise2DArray[x, z];
                }

                float yPos = (int)(meanNoise * terrainHeight * smallerCubeSize) / smallerCubeSize * terasseHeight;
                newPos = new float3(x * smallerCubeSize, yPos, z * smallerCubeSize) + rootPos;

                manager.SetComponentData(entity, new WaveMoveData 
                { 
                    originPosition = newPos,
                    waveHeight = waveHeight,
                    waveSpeed = waveSpeed, 
                });
                indexes[cubePrefabIndex]++;
            }
        }

        for (int i = 0; i < cubeEntitiesArrayArray.Length; i++)
        {
            cubeEntitiesArrayArray[i].Dispose();
        }
    }

    /************************ NOISE ************************/

    private void Generate2DNoise(int2 size)
    {
        Texture2D noiseTexture = new Texture2D(size.x, size.y); // Debug

        for (int x = 0; x < size.x ; x++)
		{
            for (int y = 0; y < size.y ; y++)
            {
                float noiseValue = GetPerlinValue2D(x, y);

                if (roundFilter) noiseValue = ApplyRound2DNoiseFilter(size, x, y, noiseValue);
                if (thresholdFilterToggle) noiseValue = ApplyThresholdFilter(noiseValue); 

                noise2DArray[x, y] = noiseValue;
                // if (noiseValue != 0) tempAvailablePositionHashSet.Add(new int2(x, y));

                if (noiseValue != 0) 
                {
                    finalPrefabIndex2DArray[x, y] = 0;
                    // cubeCount[0]++;
                }
                else
                {
                    finalPrefabIndex2DArray[x, y] = -1;
                }

                noiseTexture.SetPixel(x, y, new Color(noiseValue, noiseValue, noiseValue)); // Debug
            }
        }

        noiseTexture.Apply();
        noiseImage.texture = noiseTexture; // Debug
    }

    public float GetPerlinValue2D(float x, float y)
    {
        float xCoord = x / terrainSize.x * noiseScale.x + offset.x;
        float yCoord = y / terrainSize.z * noiseScale.y + offset.y;

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

    private void SetCubePrefabsAtPositions()
    {
        int step = biggerCubeSize / smallerCubeSize;

        for (int x = 0; x < terrainSize.x ; x += step)
		{
            for (int z = 0; z < terrainSize.z ; z += step)
            {
                SetCubePrefabsInChunk(x, z, step);
            }
        }
    }

    private void SetCubePrefabsInChunk(int originX, int originZ, int currentStep)
    {
        if (originX >= terrainSize.x || originZ >= terrainSize.z) return;

        if (finalPrefabIndex2DArray[originX, originZ] == -1)
        {
            for (int x = originX; x < originX + currentStep ; x++)
                for (int z = originZ; z < originZ + currentStep ; z++)
                    finalPrefabIndex2DArray[originX, originZ] = -1;
            return;
        }

        bool split = rng.NextFloat() < splitChance;

// ADD -1 case
        if (split)
        {
            int nextStep = currentStep / 2;

            if (nextStep == 1) return;

            for (int x = originX; x < originX + currentStep ; x += nextStep)
                for (int z = originZ; z < originZ + currentStep ; z += nextStep)
                {
                    if (x + nextStep - 1 >= terrainSize.x || z + nextStep - 1 >= terrainSize.z) continue;
                    SetCubePrefabsInChunk(x, z, nextStep);
                }
        }
        else
        {
            int prefabIndex = (int)Mathf.Log(currentStep, 2);
            finalPrefabIndex2DArray[originX, originZ] = prefabIndex;
            // cubeCount[prefabIndex]++;

            for (int x = originX; x < originX + currentStep ; x++)
                for (int z = originZ; z < originZ + currentStep ; z++)
                {
                    if (x >= terrainSize.x || z >= terrainSize.z) continue;

                    if (finalPrefabIndex2DArray[x, z] != -1)
                    {
                        if (x != originX || z != originZ) finalPrefabIndex2DArray[x, z] = -1;
                        // cubeCount[0]--;
                    }
                }
        }
    }

    private void ScaleCubes(int2 size)
    {
        int currentCubePrefabIndex = cubePrefabArray.Length - 1;

        while (currentCubePrefabIndex > 0)
        {
            // Shuffle tempAvailablePositionList
            // Utils.StartTimer();
            List<int2> tempListToShuffle = tempAvailablePositionHashSet.ToList();
            tempAvailablePositionHashSet = Utils.ShuffleListToHashSet(ref tempListToShuffle);
            // Utils.EndTimer("Shuffle", "red");

            // Copy hashset in preparation for the next round
            nextTempAvailablePositionHashSet = new HashSet<int2>(tempAvailablePositionHashSet);
            
            while (tempAvailablePositionHashSet.Count > 0)
            {
                int2 pickedSpawnPosition = tempAvailablePositionHashSet.ElementAt(0);

                cubeCount[currentCubePrefabIndex]++;

                finalPrefabIndex2DArray[pickedSpawnPosition.x, pickedSpawnPosition.y] = currentCubePrefabIndex;
                RemoveSurroundingCubePosition(ref pickedSpawnPosition, ref currentCubePrefabIndex); // Remove 3x3 square for index == 1
            }

            tempAvailablePositionHashSet = nextTempAvailablePositionHashSet;
            currentCubePrefabIndex--;
        }


        cubeCount[0] = tempAvailablePositionHashSet.Count;
        int2[] finalPositionArray = tempAvailablePositionHashSet.ToArray();
        for (int i = 0; i < tempAvailablePositionHashSet.Count; i++)
        {
            int2 spawnPos = finalPositionArray[i];
            finalPrefabIndex2DArray[spawnPos.x, spawnPos.y] = currentCubePrefabIndex; // 0
        }
    }

    private void RemoveSurroundingCubePosition([ReadOnly] ref int2 pickedPos, [ReadOnly] ref int radius)
    {
        int radiusD = radius * 2;
        int xMinD = pickedPos.x - radiusD;
        int xMaxD = pickedPos.x + radiusD;
        int yMinD = pickedPos.y - radiusD;
        int yMaxD = pickedPos.y + radiusD;

        int xMinH = xMinD + radius;
        int xMaxH = xMaxD - radius;
        int yMinH = yMinD + radius;
        int yMaxH = yMaxD - radius;

        for (int x = xMinD; x <= xMaxD ; x++)
            for (int y = yMinD; y <= yMaxD ; y++)
            {
                if (x < 0 || y < 0 || x >= terrainSize.x || y >= terrainSize.z) continue;

                tempAvailablePositionHashSet.Remove(new int2(x, y));

                if (x >= xMinH && x <= xMaxH && y >= yMinH && y <= yMaxH)
                {
                    nextTempAvailablePositionHashSet.Remove(new int2(x, y));
                }
            }
    }
}
