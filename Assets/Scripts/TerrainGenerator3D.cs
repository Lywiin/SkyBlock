using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;

public class TerrainGenerator3D : MonoBehaviour
{
    [Header("References")]
    public GameObject[] cubePrefabArray;

    [Header("Parameters")]
    public int3 terrainSize;
    public float3 noiseScale;
    public float waveHeight;
    public float waveSpeed;

    [Header("Filters")]
    public bool roundFilter;
    public AnimationCurve roundFilterCurve = new AnimationCurve(new Keyframe(0f, 1f) , new Keyframe(0.75f, 0.85f, -0.5f, -0.5f), new Keyframe(1f, 0f));
    public bool thresholdFilterToggle;
    [Range(0f, 1f)] public float threshold;

    // Perlin
    private float3 offset;
    private int3 centerPoint;
    private float maxDistanceFromCenterPoint;
    
    // Instantiate cubes
    private Entity[] cubePrefabEntityArray;
    private float[,,] noise3DArray;
    private int[,,] finalPrefabIndex3DArray;
    private HashSet<int3> tempAvailablePositionHashSet3D;
    private HashSet<int3> nextTempAvailablePositionHashSet3D;
    private int[] cubeCount;
    private int[] cubeSize;

    // GameManager reference
    EntityManager manager;
    GameObjectConversionSettings settings;
    UnityEngine.UI.RawImage noiseImage;
    Unity.Mathematics.Random rng;


    /************************ MONOBEHAVIOUR ************************/

    private void Start()
    {
        tempAvailablePositionHashSet3D = new HashSet<int3>();
        nextTempAvailablePositionHashSet3D = new HashSet<int3>();
    }

    // private void Update()
    // {
    //     if (Input.GetKeyDown(KeyCode.R))
    //     {
    //         GameManager.Instance.InitSeed();
    //         GameManager.Instance.GenerateTerrain3D();
    //     }
    // }


    /************************ TERRAIN ************************/

    private void Initialize3D()
    {
        manager = GameManager.Instance.Manager;
        settings = GameManager.Instance.Settings;
        noiseImage = GameManager.Instance.noiseImage;
        rng = GameManager.Instance.Rng;
        
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

        offset.x = rng.NextInt(1, 2000000);
        offset.y = rng.NextInt(1, 2000000);
        offset.z = rng.NextInt(1, 2000000);
    }

    private void DestroyTerrain3D()
    {
        manager.DestroyEntity(manager.CreateEntityQuery(typeof(WaveMoveData))); 

        tempAvailablePositionHashSet3D.Clear();
        nextTempAvailablePositionHashSet3D.Clear();
    }

    public void GenerateTerrain3D()
    {
        Debug.Log("<color=red> ========= GENERATE TERRAIN 2D =========</color>");
        
        Initialize3D();
        DestroyTerrain3D();

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
        GameManager.Instance.text.text = totalTime.ToString();

        int totalCount = 0;
        for (int i = 0; i < cubeCount.Length; i++)
        {
            // Debug.Log("<color=yellow> CUBE COUNT " + i + ": " + cubeCount[i] + "</color>");
            totalCount += cubeCount[i];
        }
        Debug.Log("<color=yellow> TOTAL CUBE COUNT: " + totalCount + "</color>");
    }


    /************************ NOISE ************************/

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

    private float ApplyRound3DNoiseFilter(int3 point, float value)
    {
        float distanceFromCenterPoint = math.distance(point, centerPoint);
        float distanceFromCenterPointNormalized = distanceFromCenterPoint / maxDistanceFromCenterPoint;
        float attenuationCoef = roundFilterCurve.Evaluate(distanceFromCenterPointNormalized);
        return value * attenuationCoef;
    }


    /************************ INSTANTIATE ************************/

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


    /************************ CUBE POSITION ************************/

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
