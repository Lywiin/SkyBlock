using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class GameManager : MonoBehaviour
{
    [Header("Debug")]
    public bool useUnityMathematics = true;

    [Header("UI")]
    public UnityEngine.UI.Text text;
    public UnityEngine.UI.RawImage noiseImage;

    [Header("References")]
    public GameObject[] cubePrefabArray;

    [Header("Parameters")]
    public int3 terrainSize;
    public float terrainHeight;
    public float2 scale;
    [HideInInspector] public Vector2 offset;

    [Header("Filters")]
    public bool roundFilter;
    public AnimationCurve roundFilterCurve = new AnimationCurve(new Keyframe(0f, 1f) ,new Keyframe(0.5f, 0f), new Keyframe(1f, 0f));
    public bool thresholdFilterToggle;
    [Range(0f, 1f)] public float threshold;

    [Header("Generation")]
    public int seed;

    // DOTS
    private World world;
    private EntityManager manager;
    private BlobAssetStore blob;
    private Entity[] cubePrefabEntityArray;
    private GameObjectConversionSettings settings;

    private float[,] noise2DArray;
    private int[,] finalPrefabIndex2DArray;
    private List<int2> tempAvailablePositionList;
    private List<int2> nextTempAvailablePositionList;
    private int[] cubeCount;

    private float maxDistanceFromCenter;

    // UnityEngine math
    private Vector3Int terrainSizeEngine;
    private Vector2 scaleEngine;
    private List<Vector2Int> tempAvailablePositionListEngine;
    private List<Vector2Int> nextTempAvailablePositionListEngine;

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

        tempAvailablePositionList = new List<int2>();
        nextTempAvailablePositionList = new List<int2>();
        tempAvailablePositionListEngine = new List<Vector2Int>();
        nextTempAvailablePositionListEngine = new List<Vector2Int>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            GameManager.Instance.InitSeed();
            GameManager.Instance.GenerateTerrain();
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            useUnityMathematics = !useUnityMathematics;
        }
    }

    private void OnDestroy() {
        if (blob != null) blob.Dispose();
    }


    /************************ TERRAIN ************************/

    // Allocation of all variable changeable at each generation
    private void Initialize()
    {
        // UnityEngine math
        terrainSizeEngine = new Vector3Int(terrainSize.x, terrainSize.y, terrainSize.z);
        scaleEngine = new Vector2(scale.x, scale.y);


        cubePrefabEntityArray = new Entity[cubePrefabArray.Length];
        for (int i = 0; i < cubePrefabEntityArray.Length; i++)
            cubePrefabEntityArray[i] = GameObjectConversionUtility.ConvertGameObjectHierarchy(cubePrefabArray[i], settings);

        if (useUnityMathematics)
        {
            noise2DArray = new float[terrainSize.x, terrainSize.z];
            finalPrefabIndex2DArray = new int[terrainSize.x, terrainSize.z];

            cubeCount = new int[cubePrefabArray.Length];

            maxDistanceFromCenter = Utils.Distance(0f, 0f, terrainSize.x / 2f, terrainSize.z / 2f);
        }
        else
        {
            noise2DArray = new float[terrainSizeEngine.x, terrainSizeEngine.z];
            finalPrefabIndex2DArray = new int[terrainSizeEngine.x, terrainSizeEngine.z];

            cubeCount = new int[cubePrefabArray.Length];

            maxDistanceFromCenter = Utils.Distance(0f, 0f, terrainSizeEngine.x / 2f, terrainSizeEngine.z / 2f);
        }

    }

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
        manager.DestroyEntity(manager.CreateEntityQuery(typeof(Translation))); 

        tempAvailablePositionList.Clear();
        nextTempAvailablePositionList.Clear();
        
        for (int i = 0; i < cubeCount.Length; i++)
            cubeCount[i] = 0;

        // UnityEngine maths
        tempAvailablePositionListEngine.Clear();
        nextTempAvailablePositionListEngine.Clear();
    }

    public void GenerateTerrain() 
    {
        Initialize();
        DestroyTerrain();

        if (useUnityMathematics)
        {
            int2 terrainSize2D = new int2(terrainSize.x, terrainSize.z);

            Debug.Log("<color=yellow> =============================== </color>");
            float timeStart1 = Time.realtimeSinceStartup;
            Generate2DNoise(terrainSize2D);
            float timeEnd1 = Time.realtimeSinceStartup;
            Debug.Log("<color=yellow> TIME ELAPSED (Generate2DNoise): " + (timeEnd1 - timeStart1).ToString("F2") + "s</color>");

            float timeStart2 = Time.realtimeSinceStartup;
            ScaleCubes(terrainSize2D);
            float timeEnd2 = Time.realtimeSinceStartup;
            Debug.Log("<color=yellow> TIME ELAPSED (ScaleCubes): " + (timeEnd2 - timeStart2).ToString("F2") + "s</color>");

            float timeStart3 = Time.realtimeSinceStartup;
            InstantiateCubes();
            float timeEnd3 = Time.realtimeSinceStartup;
            Debug.Log("<color=yellow> TIME ELAPSED (InstantiateCubes): " + (timeEnd3 - timeStart3).ToString("F2") + "s</color>");


            float totalTime = ((timeEnd1 - timeStart1) + (timeEnd2 - timeStart2) + (timeEnd3 - timeStart3));
            Debug.Log("<color=yellow> TIME ELAPSED: " + totalTime.ToString("F2") + "s</color>");
            text.text = totalTime.ToString();

            int totalCount = 0;
            for (int i = 0; i < cubeCount.Length; i++)
            {
                Debug.Log("Cube count " + cubePrefabArray[i].gameObject.name.ToString() + ": " + cubeCount[i]);
                totalCount += cubeCount[i];
            }
            Debug.Log("<color=yellow> TOTAL CUBE COUNT: " + totalCount + "</color>");

            Debug.Log("<color=yellow> =============================== </color>");
        }
        else
        {
            Vector2Int terrainSize2D = new Vector2Int(terrainSize.x, terrainSize.z);

            Debug.Log("<color=yellow> =============================== </color>");
            float timeStart1 = Time.realtimeSinceStartup;
            Generate2DNoiseEngine(terrainSize2D);
            float timeEnd1 = Time.realtimeSinceStartup;
            Debug.Log("<color=yellow> TIME ELAPSED (Generate2DNoise): " + (timeEnd1 - timeStart1).ToString("F2") + "s</color>");

            float timeStart2 = Time.realtimeSinceStartup;
            ScaleCubesEngine(terrainSize2D);
            float timeEnd2 = Time.realtimeSinceStartup;
            Debug.Log("<color=yellow> TIME ELAPSED (ScaleCubes): " + (timeEnd2 - timeStart2).ToString("F2") + "s</color>");

            float timeStart3 = Time.realtimeSinceStartup;
            InstantiateCubesEngine();
            float timeEnd3 = Time.realtimeSinceStartup;
            Debug.Log("<color=yellow> TIME ELAPSED (InstantiateCubes): " + (timeEnd3 - timeStart3).ToString("F2") + "s</color>");


            float totalTime = ((timeEnd1 - timeStart1) + (timeEnd2 - timeStart2) + (timeEnd3 - timeStart3));
            Debug.Log("<color=yellow> TIME ELAPSED: " + totalTime.ToString("F2") + "s</color>");
            text.text = totalTime.ToString();

            int totalCount = 0;
            for (int i = 0; i < cubeCount.Length; i++)
            {
                Debug.Log("Cube count " + cubePrefabArray[i].gameObject.name.ToString() + ": " + cubeCount[i]);
                totalCount += cubeCount[i];
            }
            Debug.Log("<color=yellow> TOTAL CUBE COUNT: " + totalCount + "</color>");

            Debug.Log("<color=yellow> =============================== </color>");
        }
    }

    private void InstantiateCubes()
    {
        // string s = "";
        // for (int x = 0; x < terrainSize.x ; x++)
        // {
        //     for (int z = 0; z < terrainSize.z ; z++)
        //     {
        //         s += finalPrefabIndex2DArray[x, z] + ", ";
        //     }
        //     s += "\n";
        // }
        // Debug.Log(s);

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
                newPos = new float3(x, (int)(noise2DArray[x, z] * terrainHeight), z) + rootPos;

                // manager.SetComponentData(entity, new Translation { Value = newPos + rootPos });
                manager.SetComponentData(entity, new WaveMoveData 
                { 
                    originPosition = newPos,
                    waveHeight = 2f,
                    waveSpeed = 0.5f, 
                });
                indexes[cubePrefabIndex]++;
            }
        }

        for (int i = 0; i < cubeEntitiesArrayArray.Length; i++)
        {
            cubeEntitiesArrayArray[i].Dispose();
        }
    }

    private void InstantiateCubesEngine()
    {
        // string s = "";
        // for (int x = 0; x < terrainSize.x ; x++)
        // {
        //     for (int z = 0; z < terrainSize.z ; z++)
        //     {
        //         s += finalPrefabIndex2DArray[x, z] + ", ";
        //     }
        //     s += "\n";
        // }
        // Debug.Log(s);

        Vector3 newPos = float3.zero;
        Vector3 rootPos = transform.position;
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
                newPos = new Vector3(x, 1f, z);

                manager.SetComponentData(entity, new Translation { Value = newPos + rootPos });
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
                if (noiseValue != 0) tempAvailablePositionList.Add(new int2(x, y));

                finalPrefabIndex2DArray[x, y] = -1; // Use the double for loop to initialize array

                noiseTexture.SetPixel(x, y, new Color(noiseValue, noiseValue, noiseValue)); // Debug
                // if (noiseValue != 0f) cubeCount++;
            }
        }

        noiseTexture.Apply();
        noiseImage.texture = noiseTexture; // Debug
    }

    private void Generate2DNoiseEngine(Vector2Int size)
    {
        Texture2D noiseTexture = new Texture2D(size.x, size.y); // Debug

        for (int x = 0; x < size.x ; x++)
		{
            for (int y = 0; y < size.y ; y++)
            {
                float noiseValue = GetPerlinValue2DEngine(x, y);

                // if (roundFilter) noiseValue = ApplyRound2DNoiseFilter(size, x, y, noiseValue);
                // if (thresholdFilterToggle) noiseValue = ApplyThresholdFilter(noiseValue); 

                noise2DArray[x, y] = noiseValue;
                if (noiseValue != 0) tempAvailablePositionListEngine.Add(new Vector2Int(x, y));

                finalPrefabIndex2DArray[x, y] = -1; // Use the double for loop to initialize array

                noiseTexture.SetPixel(x, y, new Color(noiseValue, noiseValue, noiseValue)); // Debug
                // if (noiseValue != 0f) cubeCount++;
            }
        }

        noiseTexture.Apply();
        noiseImage.texture = noiseTexture; // Debug
    }

    public float GetPerlinValue2D(float x, float y)
    {
        float xCoord = x / terrainSize.x * scale.x + offset.x;
        float yCoord = y / terrainSize.z * scale.y + offset.y;

        return Mathf.PerlinNoise(xCoord, yCoord);
    }

    public float GetPerlinValue2DEngine(float x, float y)
    {
        float xCoord = x / terrainSizeEngine.x * scaleEngine.x + offset.x;
        float yCoord = y / terrainSizeEngine.z * scaleEngine.y + offset.y;

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
        
        while (currentCubePrefabIndex > 0)
        {
            nextTempAvailablePositionList = new List<int2>(tempAvailablePositionList);
            
            while (tempAvailablePositionList.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, tempAvailablePositionList.Count);
                int2 pickedSpawnPosition = tempAvailablePositionList[randomIndex];

                cubeCount[currentCubePrefabIndex]++;

                finalPrefabIndex2DArray[pickedSpawnPosition.x, pickedSpawnPosition.y] = currentCubePrefabIndex;
                RemoveSurroundingCubePosition(ref pickedSpawnPosition, ref currentCubePrefabIndex); // Remove 3x3 square for index == 1
            }

            tempAvailablePositionList = nextTempAvailablePositionList;
            currentCubePrefabIndex--;
        }

        cubeCount[0] = tempAvailablePositionList.Count;
        for (int i = 0; i < tempAvailablePositionList.Count; i++)
        {
            int2 spawnPos = tempAvailablePositionList[i];
            finalPrefabIndex2DArray[spawnPos.x, spawnPos.y] = currentCubePrefabIndex; // 0
        }
    }

    private void ScaleCubesEngine(Vector2Int size)
    {
        int currentCubePrefabIndex = cubePrefabArray.Length - 1;
        
        while (currentCubePrefabIndex > 0)
        {
            nextTempAvailablePositionListEngine = new List<Vector2Int>(tempAvailablePositionListEngine);
            
            while (tempAvailablePositionListEngine.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, tempAvailablePositionListEngine.Count);
                Vector2Int pickedSpawnPosition = tempAvailablePositionListEngine[randomIndex];

                cubeCount[currentCubePrefabIndex]++;

                finalPrefabIndex2DArray[pickedSpawnPosition.x, pickedSpawnPosition.y] = currentCubePrefabIndex;
                RemoveSurroundingCubePositionEngine(ref pickedSpawnPosition, ref currentCubePrefabIndex); // Remove 3x3 square for index == 1
            }

            tempAvailablePositionListEngine = nextTempAvailablePositionListEngine;
            currentCubePrefabIndex--;
        }

        cubeCount[0] = tempAvailablePositionListEngine.Count;
        for (int i = 0; i < tempAvailablePositionListEngine.Count; i++)
        {
            Vector2Int spawnPos = tempAvailablePositionListEngine[i];
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

                tempAvailablePositionList.Remove(new int2(x, y));

                if (x >= xMinH && x <= xMaxH && y >= yMinH && y <= yMaxH)
                {
                    nextTempAvailablePositionList.Remove(new int2(x, y));
                }
            }
    }

    private void RemoveSurroundingCubePositionEngine([ReadOnly] ref Vector2Int pickedPos, [ReadOnly] ref int radius)
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
                if (x < 0 || y < 0 || x >= terrainSizeEngine.x || y >= terrainSizeEngine.z) continue;

                tempAvailablePositionListEngine.Remove(new Vector2Int(x, y));

                if (x >= xMinH && x <= xMaxH && y >= yMinH && y <= yMaxH)
                {
                    nextTempAvailablePositionListEngine.Remove(new Vector2Int(x, y));
                }
            }
    }
}
