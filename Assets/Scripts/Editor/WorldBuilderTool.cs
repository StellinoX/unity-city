
using UnityEngine;
using UnityEditor;

public class WorldBuilderTool : EditorWindow
{
    float gridSize = 1.0f;
    bool enableAutoSnap = false;

    [MenuItem("Tools/World Builder")]
    public static void ShowWindow()
    {
        GetWindow<WorldBuilderTool>("World Builder");
    }

    void OnGUI()
    {
        GUILayout.Label("Grid Snapping", EditorStyles.boldLabel);
        gridSize = EditorGUILayout.FloatField("Grid Size", gridSize);
        enableAutoSnap = EditorGUILayout.Toggle("Auto Snap on Move", enableAutoSnap);

        if (GUILayout.Button("Snap Selected"))
        {
            SnapSelected();
        }
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (enableAutoSnap && Event.current.type == EventType.MouseUp && Event.current.button == 0)
        {
            SnapSelected();
        }
    }

    void SnapSelected()
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            Vector3 pos = obj.transform.position;
            pos.x = Mathf.Round(pos.x / gridSize) * gridSize;
            pos.y = Mathf.Round(pos.y / gridSize) * gridSize; // Optional vertical snapping
            pos.z = Mathf.Round(pos.z / gridSize) * gridSize;
            obj.transform.position = pos;
        }
    }
}
