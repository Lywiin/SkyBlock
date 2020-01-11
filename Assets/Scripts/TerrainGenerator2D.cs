using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

public class TerrainGenerator2D : MonoBehaviour
{
    [Header("References")]
    public GameObject[] cubePrefabArray;

    [Header("Parameters")]
    public int2 terrainSize;
    public float terrainHeight;
    public float2 noiseScale;
    public float waveHeight;
    public float waveSpeed;
    public int terrasseHeight = 1;

    [Header("Filters")]
    public bool roundFilter;
    public AnimationCurve roundFilterCurve = new AnimationCurve(new Keyframe(0f, 1f) , new Keyframe(0.5f, 0f), new Keyframe(1f, 0f));
    public bool thresholdFilterToggle;
    [Range(0f, 1f)] public float threshold;

    // Perlin
    private float3 offset;
    private int3 centerPoint;
    private float maxDistanceFromCenter;
    
    // Instantiate cubes
    private Entity[] cubePrefabEntityArray;
    private float[,] noise2DArray;
    private int[,] finalPrefabIndex2DArray;
    private HashSet<int2> tempAvailablePositionHashSet;
    private HashSet<int2> nextTempAvailablePositionHashSet;
    private int[] cubeCount;

    // GameManager reference
    EntityManager manager;
    GameObjectConversionSettings settings;
    UnityEngine.UI.RawImage noiseImage;
    Unity.Mathematics.Random rng;


    /************************ MONOBEHAVIOUR ************************/
    
    private void Start()
    {
        tempAvailablePositionHashSet = new HashSet<int2>();
        nextTempAvailablePositionHashSet = new HashSet<int2>();
    }

    // private void Update()
    // {
    //     if (Input.GetKeyDown(KeyCode.R))
    //     {
    //         GameManager.Instance.InitSeed();
    //         GameManager.Instance.GenerateTerrain2D();
    //     }
    // }


    /************************ TERRAIN ************************/

    public void GenerateTerrain2D() 
    {
        Debug.Log("<color=red> ========= GENERATE TERRAIN 2D =========</color>");

        Initialize2D();
        DestroyTerrain2D();

        Utils.StartTimer();
        Generate2DNoise();
        float time1 = Utils.EndTimer("Generate2DNoise");

        Utils.StartTimer();
        ScaleCubes2D();
        float time2 = Utils.EndTimer("ScaleCubes");

        Utils.StartTimer();
        InstantiateCubes2D();
        float time3 = Utils.EndTimer("InstantiateCubes");

        float totalTime = time1 + time2 + time3;
        Debug.Log("<color=yellow> TIME ELAPSED TOTAL: " + totalTime.ToString("F8") + "s</color>");
        GameManager.Instance.text.text = totalTime.ToString();

        int totalCount = 0;
        for (int i = 0; i < cubeCount.Length; i++)
        {
            Debug.Log("<color=yellow> CUBE COUNT " + i + ": " + cubeCount[i] + "</color>");
            totalCount += cubeCount[i];
        }
        Debug.Log("<color=yellow> TOTAL CUBE COUNT: " + totalCount + "</color>");
    }

    // Allocation of all variable changeable at each generation
    private void Initialize2D()
    {
        manager = GameManager.Instance.Manager;
        settings = GameManager.Instance.Settings;
        noiseImage = GameManager.Instance.noiseImage;
        rng = GameManager.Instance.Rng;

        cubePrefabEntityArray = new Entity[cubePrefabArray.Length];
        for (int i = 0; i < cubePrefabEntityArray.Length; i++)
            cubePrefabEntityArray[i] = GameObjectConversionUtility.ConvertGameObjectHierarchy(cubePrefabArray[i], settings);

        noise2DArray = new float[terrainSize.x, terrainSize.y];
        finalPrefabIndex2DArray = new int[terrainSize.x, terrainSize.y];

        cubeCount = new int[cubePrefabArray.Length];

        maxDistanceFromCenter = Utils.Distance(0f, 0f, terrainSize.x / 2f, terrainSize.y / 2f);

        offset.x = rng.NextInt(1, 2000000);
        offset.y = rng.NextInt(1, 2000000);
    }

    private void DestroyTerrain2D()
    {
        manager.DestroyEntity(manager.CreateEntityQuery(typeof(WaveMoveData))); 

        tempAvailablePositionHashSet.Clear();
        nextTempAvailablePositionHashSet.Clear();
        
        for (int i = 0; i < cubeCount.Length; i++)
            cubeCount[i] = 0;
    }


    /************************ NOISE ************************/

    private void Generate2DNoise()
    {
        Texture2D noiseTexture = new Texture2D(terrainSize.x, terrainSize.y); // Debug

        for (int x = 0; x < terrainSize.x ; x++)
		{
            for (int y = 0; y < terrainSize.y ; y++)
            {
                float noiseValue = GetPerlinValue2D(x, y);

                if (roundFilter) noiseValue = ApplyRound2DNoiseFilter(x, y, noiseValue);
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
        float yCoord = y / terrainSize.y * noiseScale.y + offset.y;

        return Mathf.PerlinNoise(xCoord, yCoord);
    }

    private float ApplyRound2DNoiseFilter(int x, int y, float value)
    {
        float distanceFromCenter = Utils.Distance(x, y, terrainSize.x / 2, terrainSize.y / 2);
        float distanceFromCenterNormalized = distanceFromCenter / maxDistanceFromCenter;
        float attenuationCoef = roundFilterCurve.Evaluate(distanceFromCenterNormalized);
        return value * attenuationCoef;
    }

    private float ApplyThresholdFilter(float value)
    {
        return value < threshold ? 0f : value;
    }


    /************************ INSTANTIATE ************************/

    private void InstantiateCubes2D()
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
		{
            for (int z = 0; z < terrainSize.y ; z++)
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


    /************************ CUBE POSITION ************************/

    private void ScaleCubes2D()
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
                if (x < 0 || y < 0 || x >= terrainSize.x || y >= terrainSize.y) continue;

                tempAvailablePositionHashSet.Remove(new int2(x, y));

                if (x >= xMinH && x <= xMaxH && y >= yMinH && y <= yMaxH)
                {
                    nextTempAvailablePositionHashSet.Remove(new int2(x, y));
                }
            }
    }
}
