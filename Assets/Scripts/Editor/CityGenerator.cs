
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class CityGenerator : EditorWindow
{
    // Settings
    [SerializeField] int width = 20;
    [SerializeField] int height = 20;
    [SerializeField] float cellSize = 10f; // Standard Kenney size
    [SerializeField] int blockFrequency = 4; // Road every N cells
    
    // Auto-fill references
    [SerializeField] string roadPath = "Assets/Prefabs/CityKit/Roads"; // Example path, user can drag
    
    // Prefab References
    [SerializeField] GameObject roadStraight;
    [SerializeField] GameObject roadCorner;
    [SerializeField] GameObject roadT;
    [SerializeField] GameObject roadCross;
    [SerializeField] GameObject[] buildings;
    
    // Grid & Random
    enum CellType { Empty, Road, Building }
    CellType[,] grid;
    Vector2 scrollPos;
    SerializedObject so;
    
    // Calibration
    [SerializeField] int seed = 12345;
    [SerializeField] float rotStraight = 0;
    [SerializeField] float rotCorner = 0;
    [SerializeField] float rotT = 0;
    [SerializeField] float rotCross = 0;
    [SerializeField] float rotBuildingOffset = 0; // New: fix building orientation
    [SerializeField, Range(0f, 1f)] float buildingDensity = 1.0f; // New: empty spots

    [MenuItem("Tools/City Generator")]
    public static void ShowWindow()
    {
        GetWindow<CityGenerator>("City Generator");
    }

    void OnEnable()
    {
        so = new SerializedObject(this);
    }

    void OnGUI()
    {
        if (so == null) so = new SerializedObject(this);
        so.Update();

        scrollPos = GUILayout.BeginScrollView(scrollPos);
        
        GUILayout.Label("City Settings", EditorStyles.boldLabel);
        width = EditorGUILayout.IntField("Width", width);
        height = EditorGUILayout.IntField("Height", height);
        cellSize = EditorGUILayout.FloatField("Cell Size", cellSize);
        blockFrequency = EditorGUILayout.IntSlider("Block Frequency", blockFrequency, 2, 10);
        seed = EditorGUILayout.IntField("Random Seed", seed);
        
        GUILayout.Space(10);
        GUILayout.Label("Road Prefabs & Calibration", EditorStyles.boldLabel);
        
        DrawRoadField("Straight", "roadStraight", "rotStraight");
        DrawRoadField("Corner", "roadCorner", "rotCorner");
        DrawRoadField("T-Split", "roadT", "rotT");
        DrawRoadField("Cross", "roadCross", "rotCross");
        
        GUILayout.Space(10);
        GUILayout.Label("Buildings", EditorStyles.boldLabel);
        
        buildingDensity = EditorGUILayout.Slider("Density", buildingDensity, 0f, 1f);
        rotBuildingOffset = EditorGUILayout.FloatField("Rotation Offset", rotBuildingOffset);
        
        SerializedProperty buildingsProp = so.FindProperty("buildings");
        EditorGUILayout.PropertyField(buildingsProp, true);
        
        GUILayout.Space(20);
        if (GUILayout.Button("Generate City", GUILayout.Height(40)))
        {
            GenerateCity();
        }
        
        if (GUILayout.Button("Clear City"))
        {
            ClearCity();
        }
        
        GUILayout.EndScrollView();
        so.ApplyModifiedProperties();
    }
    
    void DrawRoadField(string label, string prefabProp, string rotProp)
    {
        GUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(so.FindProperty(prefabProp), new GUIContent(label));
        GUILayout.Label("Rot Offset:", GUILayout.Width(70));
        EditorGUILayout.PropertyField(so.FindProperty(rotProp), GUIContent.none, GUILayout.Width(40));
        GUILayout.EndHorizontal();
    }

    void ClearCity()
    {
        GameObject cityParent = GameObject.Find("GeneratedCity");
        if (cityParent != null) DestroyImmediate(cityParent);
    }

    void GenerateCity()
    {
        ClearCity();
        Random.InitState(seed);
        GameObject cityParent = new GameObject("GeneratedCity");
        
        grid = new CellType[width, height];
        
        // 1. Generate Logic Grid
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Create roads on grid lines (border is always road)
                bool isRoadX = (x % blockFrequency == 0);
                bool isRoadY = (y % blockFrequency == 0);
                
                // Add borders
                if (x == 0 || x == width - 1) isRoadX = true;
                if (y == 0 || y == height - 1) isRoadY = true;

                if (isRoadX || isRoadY)
                {
                    grid[x, y] = CellType.Road;
                }
                else
                {
                    grid[x, y] = CellType.Building;
                }
            }
        }
        
        // Prepare building bag
        List<GameObject> buildingBag = new List<GameObject>();
        
    // 2. Instantiate Objects
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 pos = new Vector3(x * cellSize, 0, y * cellSize);
                
                if (grid[x, y] == CellType.Building)
                {
                    // Density check
                    if (Random.value > buildingDensity) continue;

                    if (buildings != null && buildings.Length > 0)
                    {
                        // Refill bag if empty
                        if (buildingBag.Count == 0)
                        {
                            buildingBag.AddRange(buildings);
                            // Shuffle
                            for (int i = 0; i < buildingBag.Count; i++) {
                                GameObject temp = buildingBag[i];
                                int randomIndex = Random.Range(i, buildingBag.Count);
                                buildingBag[i] = buildingBag[randomIndex];
                                buildingBag[randomIndex] = temp;
                            }
                        }
                        
                        // Pick from bag
                        GameObject prefab = buildingBag[0];
                        buildingBag.RemoveAt(0);

                        if (prefab)
                        {
                            GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cityParent.transform);
                            obj.transform.position = pos;
                            
                            // Face closest road logic
                            float rotation = 0;
                            if (IsRoad(x, y + 1)) rotation = 0; // North
                            else if (IsRoad(x + 1, y)) rotation = 90; // East
                            else if (IsRoad(x, y - 1)) rotation = 180; // South
                            else if (IsRoad(x - 1, y)) rotation = 270; // West
                            
                            rotation += rotBuildingOffset;
                            obj.transform.rotation = Quaternion.Euler(0, rotation, 0);
                            
                            EnsureCollider(obj);
                        }
                    }
                }
                else if (grid[x, y] == CellType.Road)
                {
                    SpawnRoad(x, y, pos, cityParent.transform);
                }
            }
        }
    }
    
    void SpawnRoad(int x, int y, Vector3 pos, Transform parent)
    {
        // Check neighbors
        bool n = IsRoad(x, y + 1);
        bool s = IsRoad(x, y - 1);
        bool e = IsRoad(x + 1, y);
        bool w = IsRoad(x - 1, y);
        
        int neighbors = (n?1:0) + (s?1:0) + (e?1:0) + (w?1:0);
        GameObject prefab = roadStraight;
        float baseRotation = 0;
        float offset = 0;
        
        if (neighbors == 4)
        {
            prefab = roadCross;
            offset = rotCross;
        }
        else if (neighbors == 3)
        {
            prefab = roadT;
            offset = rotT;
            if (!n) baseRotation = 270;
            else if (!e) baseRotation = 180;
            else if (!s) baseRotation = 90;
            else if (!w) baseRotation = 0;
        }
        else if (neighbors == 2)
        {
            if (n && s) { prefab = roadStraight; baseRotation = 0; offset = rotStraight; }
            else if (e && w) { prefab = roadStraight; baseRotation = 90; offset = rotStraight; }
            else // Corner
            {
                prefab = roadCorner;
                offset = rotCorner;
                if (n && e) baseRotation = 0;
                else if (e && s) baseRotation = 90;
                else if (s && w) baseRotation = 180;
                else if (w && n) baseRotation = 270;
            }
        }
        else if (neighbors == 1) // Dead End
        {
            prefab = roadStraight;
            offset = rotStraight;
            if (n || s) baseRotation = 0;
            else baseRotation = 90;
        }
        
        if (prefab != null)
        {
            GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            obj.transform.position = pos;
            obj.transform.rotation = Quaternion.Euler(0, baseRotation + offset, 0);
            EnsureCollider(obj);
        }
    }
    
    void EnsureCollider(GameObject obj)
    {
        if (obj.GetComponent<Collider>() == null)
        {
            // Check children
            if (obj.GetComponentInChildren<Collider>() == null)
            {
                // Add MeshCollider
                MeshFilter[] filters = obj.GetComponentsInChildren<MeshFilter>();
                foreach (MeshFilter filter in filters)
                {
                    if (filter.gameObject.GetComponent<Collider>() == null)
                    {
                        filter.gameObject.AddComponent<MeshCollider>();
                    }
                }
            }
        }
    }
    
    bool IsRoad(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        return grid[x, y] == CellType.Road;
    }
}
