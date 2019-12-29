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
        if (GUILayout.Button("Generate Terrain"))
        {   
            if (Application.isPlaying) GameManager.Instance.RefreshTerrain(false);
        }

        GUILayout.Space(5);
        if (GUILayout.Button("Generate Random Terrain"))
        {   
            if (Application.isPlaying) GameManager.Instance.RefreshTerrain(true);
        }
        
        SceneView.RepaintAll();
    }
}
