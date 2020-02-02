using Unity.Entities;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("UI")]
    public UnityEngine.UI.Text text;
    public UnityEngine.UI.RawImage noiseImage;

    [Header("Generation")]
    public uint seed = 1;
    private Unity.Mathematics.Random rng;

    // DOTS
    private World world;
    private EntityManager manager;
    private BlobAssetStore blob;
    // private Entity[] cubePrefabEntityArray;
    private GameObjectConversionSettings settings;

    // Instance
    public static GameManager Instance;

    public GameObjectConversionSettings Settings 
    {
        get { return settings; }
    }

    public EntityManager Manager 
    {
        get { return manager; }
    }

    public Unity.Mathematics.Random Rng
    {
        get { return rng; }
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
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            // GameManager.Instance.InitSeed();
            // GameManager.Instance.GenerateTerrain2D();
            // GameManager.Instance.GenerateTerrain3D();
        }
    }

    private void OnDestroy() {
        if (blob != null) blob.Dispose();
    }

    public void RefreshSeed()
    {
        seed = rng.NextUInt(uint.MinValue, uint.MaxValue);
    }

    public void InitSeed()
    {
        rng = new Unity.Mathematics.Random(seed);
        UnityEngine.Random.InitState(unchecked((int)seed));
    }
}
