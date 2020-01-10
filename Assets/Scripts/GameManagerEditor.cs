#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


[CustomEditor(typeof(GameManager))]
public class GameManagerEditor : Editor
{
    private TerrainGenerator2D terrainGenerator2D;
    private TerrainGenerator3D terrainGenerator3D;

    private void Awake()
    {
        terrainGenerator2D = (TerrainGenerator2D)FindObjectOfType(typeof(TerrainGenerator2D));
        terrainGenerator3D = (TerrainGenerator3D)FindObjectOfType(typeof(TerrainGenerator3D));
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);
        if (GUILayout.Button("Generate 2D Terrain") && Application.isPlaying)
        {   
            terrainGenerator2D.GenerateTerrain2D();
        }

        GUILayout.Space(5);
        if (GUILayout.Button("Generate 3D Terrain") && Application.isPlaying)
        {   
            terrainGenerator3D.GenerateTerrain3D();
        }

        GUILayout.Space(10);
        if (GUILayout.Button("Refresh Seed") && Application.isPlaying)
        {   
            GameManager.Instance.RefreshSeed();
        }
        
        SceneView.RepaintAll();
    }
}
#endif