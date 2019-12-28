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

        if (GUILayout.Button("Generate Terrain"))
        {   
            GameManager.Instance.RefreshTerrain();
        }
        
        SceneView.RepaintAll();
    }
}
