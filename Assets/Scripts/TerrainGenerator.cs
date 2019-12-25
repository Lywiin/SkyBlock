using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{

    public GameObject blockToSpawn;

    void Start()
    {
        for (int x = 0; x < 100; x++) {
            for (int z = 0; z < 100; z++) {
                SpawnBlock(new Vector3(x, 0f, z));
            }
        }
    }

    private void SpawnBlock(Vector3 position) {
        Instantiate(blockToSpawn, position, Quaternion.identity, this.transform);
    }
}
