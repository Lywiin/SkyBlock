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
    public float3 noiseScale;
    public float waveHeight;
    public float waveSpeed;
    public int terrasseHeight = 1;
    public bool terrain2D;
    public bool terrain3D;


    [Header("Filters")]
    public bool roundFilter;
    public AnimationCurve roundFilterCurve = new AnimationCurve(new Keyframe(0f, 1f) , new Keyframe(0.75f, 0.85f, -0.5f, -0.5f), new Keyframe(1f, 0f));
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
    private float3 offset;
    private int3 centerPoint;
    private float maxDistanceFromCenter;
    private float maxDistanceFromCenterPoint;
    
    // Instantiate cubes
    private float[,] noise2DArray;
    private float[,,] noise3DArray;
    private int[,] finalPrefabIndex2DArray;
    private int[,,] finalPrefabIndex3DArray;
    private HashSet<int2> tempAvailablePositionHashSet;
    private HashSet<int2> nextTempAvailablePositionHashSet;
    private HashSet<int3> tempAvailablePositionHashSet3D;
    private HashSet<int3> nextTempAvailablePositionHashSet3D;
    private int[] cubeCount;
    private int[] cubeSize;


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

        tempAvailablePositionHashSet3D = new HashSet<int3>();
        nextTempAvailablePositionHashSet3D = new HashSet<int3>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            GameManager.Instance.InitSeed();
            // GameManager.Instance.GenerateTerrain2D();
            GameManager.Instance.GenerateTerrain3D();
        }
    }

    private void OnDestroy() {
        if (blob != null) blob.Dispose();
    }


    /************************ TERRAIN ************************/

    // Allocation of all variable changeable at each generation
    private void Initialize2D()
    {
        cubePrefabEntityArray = new Entity[cubePrefabArray.Length];
        for (int i = 0; i < cubePrefabEntityArray.Length; i++)
            cubePrefabEntityArray[i] = GameObjectConversionUtility.ConvertGameObjectHierarchy(cubePrefabArray[i], settings);

        noise2DArray = new float[terrainSize.x, terrainSize.z];
        finalPrefabIndex2DArray = new int[terrainSize.x, terrainSize.z];

        cubeCount = new int[cubePrefabArray.Length];

        maxDistanceFromCenter = Utils.Distance(0f, 0f, terrainSize.x / 2f, terrainSize.z / 2f);
    }

    private void Initialize3D()
    {
        cubePrefabEntityArray = new Entity[cubePrefabArray.Length];
        for (int i = 0; i < cubePrefabEntityArray.Length; i++)
            cubePrefabEntityArray[i] = GameObjectConversionUtility.ConvertGameObjectHierarchy(cubePrefabArray[i], settings);

        finalPrefabIndex3DArray = new int[terrainSize.x, terrainSize.y, terrainSize.z];
        noise3DArray = new float[terrainSize.x, terrainSize.y, terrainSize.z];

        cubeCount = new int[cubePrefabArray.Length];
        cubeSize = new int[cubePrefabArray.Length];
        for (int i = 0; i < cubeSize.Length; i++)
            cubeSize[i] = (int)cubePrefabArray[i].GetComponent<Renderer>().bounds.size.x;

        centerPoint = new int3(terrainSize.x / 2, terrainSize.y - 1, terrainSize.z / 2);
        maxDistanceFromCenterPoint = math.distance(int3.zero, centerPoint);

        DestroyTerrain3D();
    }

    public void RefreshSeed()
    {
        seed = rng.NextUInt(uint.MinValue, uint.MaxValue);

        InitSeed();
    }

    public void InitSeed()
    {
        rng = new Unity.Mathematics.Random(seed);
        UnityEngine.Random.InitState(System.Convert.ToInt32(seed));

        offset.x = rng.NextInt(1, 2000000);
        offset.y = rng.NextInt(1, 2000000);
        offset.z = rng.NextInt(1, 2000000);
    }

    private void DestroyTerrain2D()
    {
        manager.DestroyEntity(manager.CreateEntityQuery(typeof(WaveMoveData))); 

        tempAvailablePositionHashSet.Clear();
        nextTempAvailablePositionHashSet.Clear();
        
        for (int i = 0; i < cubeCount.Length; i++)
            cubeCount[i] = 0;
    }

    private void DestroyTerrain3D()
    {
        manager.DestroyEntity(manager.CreateEntityQuery(typeof(WaveMoveData))); 
    }

    public void GenerateTerrain()
    {
        if (terrain2D) GenerateTerrain2D();
        if (terrain3D) GenerateTerrain3D();
    }

    public void GenerateTerrain2D() 
    {
        Initialize2D();
        DestroyTerrain2D();

        int2 terrainSize2D = new int2(terrainSize.x, terrainSize.z);

        // Debug.Log("<color=yellow> =============================== </color>");
        Utils.StartTimer();
        Generate2DNoise(terrainSize2D);
        float time1 = Utils.EndTimer("Generate2DNoise");

        Utils.StartTimer();
        ScaleCubes2D(terrainSize2D);
        float time2 = Utils.EndTimer("ScaleCubes");

        Utils.StartTimer();
        InstantiateCubes2D();
        float time3 = Utils.EndTimer("InstantiateCubes");


        float totalTime = time1 + time2 + time3;
        Debug.Log("<color=yellow> TIME ELAPSED TOTAL: " + totalTime.ToString("F8") + "s</color>");
        text.text = totalTime.ToString();

        int totalCount = 0;
        for (int i = 0; i < cubeCount.Length; i++)
        {
            Debug.Log("<color=yellow> CUBE COUNT " + i + ": " + cubeCount[i] + "</color>");
            totalCount += cubeCount[i];
        }
        Debug.Log("<color=yellow> TOTAL CUBE COUNT: " + totalCount + "</color>");

        Debug.Log("<color=orange> =============================== </color>");
    }

    public void GenerateTerrain3D()
    {
        Initialize3D();

        Utils.StartTimer();
        Generate3DNoise();
        float time1 = Utils.EndTimer("Generate3DNoise");

        Utils.StartTimer();
        ScaleCubes3D();
        float time2 = Utils.EndTimer("ScaleCubes3D");

        Utils.StartTimer();
        InstantiateCubes3D();
        float time3 = Utils.EndTimer("InstantiateCubes3D");

        float totalTime = time1 + time2 + time3;
        Debug.Log("<color=yellow> TIME ELAPSED TOTAL: " + totalTime.ToString("F8") + "s</color>");
        text.text = totalTime.ToString();

        int totalCount = 0;
        for (int i = 0; i < cubeCount.Length; i++)
        {
            // Debug.Log("<color=yellow> CUBE COUNT " + i + ": " + cubeCount[i] + "</color>");
            totalCount += cubeCount[i];
        }
        Debug.Log("<color=yellow> TOTAL CUBE COUNT: " + totalCount + "</color>");
    }

    private void InstantiateCubes2D()
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
                float y = (int)(noise2DArray[x, z] * terrainHeight) / terrasseHeight * terrasseHeight;
                newPos = new float3(x, y, z) + rootPos;

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

    private void InstantiateCubes3D()
    {
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
            for (int y = 0; y < terrainSize.y ; y++)
                for (int z = 0; z < terrainSize.z ; z++)
                {
                    if (finalPrefabIndex3DArray[x, y, z] == -1) continue;
                    int cubePrefabIndex = finalPrefabIndex3DArray[x, y, z];

                    Entity entity = cubeEntitiesArrayArray[cubePrefabIndex][indexes[cubePrefabIndex]];
                    newPos = new float3(x, y, z) + rootPos;

                    manager.SetComponentData(entity, new WaveMoveData 
                    { 
                        originPosition = newPos,
                        waveHeight = waveHeight,
                        waveSpeed = waveSpeed, 
                    });
                    indexes[cubePrefabIndex]++;
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
// Debug.Log(noiseValue);
                if (roundFilter) noiseValue = ApplyRound2DNoiseFilter(size, x, y, noiseValue);
                if (thresholdFilterToggle) noiseValue = ApplyThresholdFilter(noiseValue); 

                noise2DArray[x, y] = noiseValue;
                if (noiseValue != 0) tempAvailablePositionHashSet.Add(new int2(x, y));

                finalPrefabIndex2DArray[x, y] = -1; // Use the double for loop to initialize array

                noiseTexture.SetPixel(x, y, new Color(noiseValue, noiseValue, noiseValue)); // Debug
            }
        }

        noiseTexture.Apply();
        noiseImage.texture = noiseTexture; // Debug
    }

    private float GetPerlinValue2D(float x, float y)
    {
        float xCoord = x / terrainSize.x * noiseScale.x + offset.x;
        float yCoord = y / terrainSize.z * noiseScale.y + offset.y;

        return Mathf.PerlinNoise(xCoord, yCoord);
    }

    private void Generate3DNoise()
    {
        for (int x = 0; x < terrainSize.x ; x++)
            for (int y = 0; y < terrainSize.y ; y++)
                for (int z = 0; z < terrainSize.z ; z++)
                {
                    float noiseValue = GetPerlinValue3D(x, y, z);

                    if (roundFilter) noiseValue = ApplyRound3DNoiseFilter(new int3(x, y, z), noiseValue);
                    
                    noise3DArray[x, y, z] = noiseValue;
                    if (noiseValue > threshold) tempAvailablePositionHashSet3D.Add(new int3(x, y, z));
                    
                    finalPrefabIndex3DArray[x, y, z] = -1;
                }
    }
    
    private float GetPerlinValue3D(float x, float y, float z)
    {
        float xCoord = x / terrainSize.x * noiseScale.x + offset.x;
        float yCoord = y / terrainSize.y * noiseScale.y + offset.y;
        float zCoord = z / terrainSize.z * noiseScale.z + offset.z;

        float xy = Mathf.PerlinNoise(xCoord, yCoord);
        float yz = Mathf.PerlinNoise(yCoord, zCoord);
        float xz = Mathf.PerlinNoise(xCoord, zCoord);
        
        float yx = Mathf.PerlinNoise(yCoord, xCoord);
        float zy = Mathf.PerlinNoise(zCoord, yCoord);
        float zx = Mathf.PerlinNoise(zCoord, xCoord);

        float xyz = xy + yz + xz + yx + zy + zx;
        return xyz / 6f;
    } 

    private float ApplyRound2DNoiseFilter(int2 size, int x, int y, float value)
    {
        float distanceFromCenter = Utils.Distance(x, y, size.x / 2, size.y / 2);
        float distanceFromCenterNormalized = distanceFromCenter / maxDistanceFromCenter;
        float attenuationCoef = roundFilterCurve.Evaluate(distanceFromCenterNormalized);
        return value * attenuationCoef;
    }

    private float ApplyRound3DNoiseFilter(int3 point, float value)
    {
        float distanceFromCenterPoint = math.distance(point, centerPoint);
        float distanceFromCenterPointNormalized = distanceFromCenterPoint / maxDistanceFromCenterPoint;
        float attenuationCoef = roundFilterCurve.Evaluate(distanceFromCenterPointNormalized);
        return value * attenuationCoef;
    }

    private float ApplyThresholdFilter(float value)
    {
        return value < threshold ? 0f : value;
    }

    /************************ CUBE POSITION ************************/

    private void ScaleCubes2D(int2 size)
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
                RemoveSurroundingCubePosition2D(ref pickedSpawnPosition, ref currentCubePrefabIndex); // Remove 3x3 square for index == 1
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

    private void ScaleCubes3D()
    {
        int currentCubePrefabIndex = cubePrefabArray.Length - 1;

        // Shuffle tempAvailablePositionList
        // Utils.StartTimer();
        List<int3> tempListToShuffle = tempAvailablePositionHashSet3D.ToList();
        tempAvailablePositionHashSet3D = Utils.ShuffleListToHashSet(ref tempListToShuffle);
        // Utils.EndTimer("Shuffle", "red");

        while (currentCubePrefabIndex >= 0)
        {
            // Copy hashset in preparation for the next round
            if (currentCubePrefabIndex > 0) nextTempAvailablePositionHashSet3D = new HashSet<int3>(tempAvailablePositionHashSet3D);
            
            while (tempAvailablePositionHashSet3D.Count > 0)
            {
                int3 pickedSpawnPosition = tempAvailablePositionHashSet3D.ElementAt(0);

                cubeCount[currentCubePrefabIndex]++;
                finalPrefabIndex3DArray[pickedSpawnPosition.x, pickedSpawnPosition.y, pickedSpawnPosition.z] = currentCubePrefabIndex;

                RemoveSurroundingCubePosition3D(ref pickedSpawnPosition, ref currentCubePrefabIndex); // Remove 3x3 square for index == 1
            }

            tempAvailablePositionHashSet3D = nextTempAvailablePositionHashSet3D;
            currentCubePrefabIndex--;
        }


        // cubeCount[0] = tempAvailablePositionHashSet3D.Count;
        // int3[] finalPositionArray = tempAvailablePositionHashSet3D.ToArray();
        // for (int i = 0; i < tempAvailablePositionHashSet3D.Count; i++)
        // {
        //     int3 spawnPos = finalPositionArray[i];
        //     finalPrefabIndex3DArray[spawnPos.x, spawnPos.y, spawnPos.z] = currentCubePrefabIndex; // 0
        // }
    }

    private void RemoveSurroundingCubePosition2D([ReadOnly] ref int2 pickedPos, [ReadOnly] ref int index)
    {
        int diameter = index * 2;
        int xMinD = pickedPos.x - diameter;
        int xMaxD = pickedPos.x + diameter;
        int yMinD = pickedPos.y - diameter;
        int yMaxD = pickedPos.y + diameter;

        int xMinH = xMinD + index;
        int xMaxH = xMaxD - index;
        int yMinH = yMinD + index;
        int yMaxH = yMaxD - index;

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

    private void RemoveSurroundingCubePosition3D([ReadOnly] ref int3 pickedPos, [ReadOnly] ref int index)
    {
        int diameter = cubeSize[index] - 1;
        int xMinD = pickedPos.x - diameter;
        int xMaxD = pickedPos.x + diameter;
        int yMinD = pickedPos.y - diameter;
        int yMaxD = pickedPos.y + diameter;
        int zMinD = pickedPos.z - diameter;
        int zMaxD = pickedPos.z + diameter;

        int xMinH = xMinD + diameter / 2;
        int xMaxH = xMaxD - diameter / 2;
        int yMinH = yMinD + diameter / 2;
        int yMaxH = yMaxD - diameter / 2;
        int zMinH = zMinD + diameter / 2;
        int zMaxH = zMaxD - diameter / 2;

        for (int x = xMinD; x <= xMaxD ; x++)
            for (int y = yMinD; y <= yMaxD ; y++)
                for (int z = zMinD; z <= zMaxD ; z++)
                {
                    if (x < 0 || y < 0 || z < 0 || x >= terrainSize.x || y >= terrainSize.y || z >= terrainSize.z) continue;

                    tempAvailablePositionHashSet3D.Remove(new int3(x, y, z));

                    if (index > 0 && x >= xMinH && x <= xMaxH && y >= yMinH && y <= yMaxH && z >= zMinH && z <= zMaxH)
                    {
                        nextTempAvailablePositionHashSet3D.Remove(new int3(x, y, z));
                    }
                }
    }
}
