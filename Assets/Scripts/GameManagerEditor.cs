#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


[CustomEditor(typeof(GameManager))]
public class GameManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);
        if (GUILayout.Button("Generate Terrain") && Application.isPlaying)
        {   
            GameManager.Instance.InitSeed();
            GameManager.Instance.GenerateTerrain();
        }

        GUILayout.Space(5);
        if (GUILayout.Button("Generate Random Terrain") && Application.isPlaying)
        {   
            GameManager.Instance.RefreshSeed();
            GameManager.Instance.InitSeed();
            GameManager.Instance.GenerateTerrain();
        }
        
        SceneView.RepaintAll();
    }
}
#endif