
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class AssetPalette : EditorWindow
{
    string prefabPath = "Assets/Prefabs/CityKit";
    Vector2 scrollPos;
    Texture2D[] prefabPreviews;
    GameObject[] prefabs;
    int selectedIndex = -1;
    bool paintMode = false;
    float gridSize = 1f;

    [MenuItem("Tools/Asset Palette")]
    public static void ShowWindow()
    {
        GetWindow<AssetPalette>("Asset Palette");
    }

    void OnGUI()
    {
        GUILayout.Label("Asset Palette", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        prefabPath = EditorGUILayout.TextField("Prefab Path (Relative)", prefabPath);
        if (GUILayout.Button("Load Assets"))
        {
            LoadAssets();
        }
        GUILayout.EndHorizontal();

        if (prefabs == null || prefabs.Length == 0)
        {
            GUILayout.Label("No assets loaded.");
            return;
        }

        GUILayout.Label($"Loaded {prefabs.Length} Prefabs");
        
        // Settings
        GUILayout.Space(10);
        paintMode = GUILayout.Toggle(paintMode, "Paint Mode", "Button", GUILayout.Height(30));
        gridSize = EditorGUILayout.FloatField("Grid Size", gridSize);
        
        if (paintMode)
        {
            EditorGUILayout.HelpBox("Hold Ctrl+Click in Scene View to paint.", MessageType.Info);
        }

        GUILayout.Space(10);
        
        // Grid of previews
        float width = position.width - 20;
        int filesPerRow = Mathf.FloorToInt(width / 70); // 64 + padding
        if (filesPerRow < 1) filesPerRow = 1;

        scrollPos = GUILayout.BeginScrollView(scrollPos);
        int rows = Mathf.CeilToInt((float)prefabs.Length / filesPerRow);

        for (int i = 0; i < rows; i++)
        {
            GUILayout.BeginHorizontal();
            for (int j = 0; j < filesPerRow; j++)
            {
                int index = i * filesPerRow + j;
                if (index < prefabs.Length)
                {
                    GUIStyle style = new GUIStyle(GUI.skin.button);
                    if (index == selectedIndex)
                    {
                        style.normal.background = Texture2D.whiteTexture; // Highlight simplistic
                        GUI.backgroundColor = Color.green;
                    }
                    else
                    {
                        GUI.backgroundColor = Color.white;
                    }

                    if (GUILayout.Button(prefabPreviews[index], style, GUILayout.Width(64), GUILayout.Height(64)))
                    {
                        selectedIndex = index;
                        // Select in project view too
                        Selection.activeObject = prefabs[index];
                    }
                    GUI.backgroundColor = Color.white;
                }
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }
    
    void LoadAssets()
    {
        if (!AssetDatabase.IsValidFolder(prefabPath))
        {
            Debug.LogError("Foldes does not exist: " + prefabPath);
            return;
        }
        
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabPath });
        List<GameObject> loaded = new List<GameObject>();
        List<Texture2D> previews = new List<Texture2D>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (obj != null)
            {
                loaded.Add(obj);
                // Get preview
                Texture2D preview = AssetPreview.GetAssetPreview(obj);
                if (preview == null) preview = AssetPreview.GetMiniThumbnail(obj);
                previews.Add(preview);
            }
        }
        
        prefabs = loaded.ToArray();
        prefabPreviews = previews.ToArray();
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
        if (!paintMode || selectedIndex == -1 || selectedIndex >= prefabs.Length) return;

        Event e = Event.current;
        
        // Only paint on Ctrl + Click
        if (e.control && e.type == EventType.MouseDown && e.button == 0)
        {
            // Raycast against scene
            // HandleUtility.GUIPointToWorldRay(e.mousePosition);
            
            // Create a plane at 0 or raycast against existing colliders
            // Simple plane at Y=0 for now or raycast
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            RaycastHit hit;
            Vector3 spawnPos = Vector3.zero;

            if (Physics.Raycast(ray, out hit))
            {
                spawnPos = hit.point;
            }
            else
            {
                // Fallback to ground plane interaction
                Plane hPlane = new Plane(Vector3.up, Vector3.zero);
                float distance = 0; 
                if (hPlane.Raycast(ray, out distance)){
                    spawnPos = ray.GetPoint(distance);
                }
            }

            // Snap
            if (gridSize > 0)
            {
                spawnPos.x = Mathf.Round(spawnPos.x / gridSize) * gridSize;
                spawnPos.y = Mathf.Round(spawnPos.y / gridSize) * gridSize;
                spawnPos.z = Mathf.Round(spawnPos.z / gridSize) * gridSize;
            }

            GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[selectedIndex]);
            obj.transform.position = spawnPos;
            
            Undo.RegisterCreatedObjectUndo(obj, "Paint " + obj.name);
            e.Use(); // Consume event
        }
        
        // Force repaint to show potential cursor changes or improvements
        // But avoiding too many updates.
         if (paintMode) {
            // Optional: Draw preview mesh at mouse position...
             HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }
    }
}
