using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class AdvancedCityGenerator : EditorWindow
{
    [SerializeField] int mapWidth = 40;
    [SerializeField] int mapHeight = 40;
    [SerializeField] float cellSize = 10f;
    [SerializeField] int seed = 12345;
    
    [Header("Generation Settings")]
    [SerializeField] int minZoneSize = 6;
    [SerializeField] int maxZoneSize = 15;
    
    // --- Prefabs ---
    // Roads
    [SerializeField] GameObject roadStraight;
    [SerializeField] GameObject roadCorner;
    [SerializeField] GameObject roadT;
    [SerializeField] GameObject roadCross;
    [SerializeField] GameObject roadEnd;
    
    // Rotations for roads (calibration)
    [SerializeField] float rotStraight = 0;
    [SerializeField] float rotCorner = 0;
    [SerializeField] float rotT = 0;
    [SerializeField] float rotCross = 0;
    [SerializeField] float rotEnd = 0;
    
    [Header("Building Rotations")]
    [SerializeField] float rotResidential = 0;
    [SerializeField] float rotCommercial = 0;
    [SerializeField] float rotIndustrial = 0;
    [SerializeField] float rotProp = 0;

    // Props & Buildings
    [SerializeField] GameObject[] residentialBuildings;
    [SerializeField] GameObject[] commercialBuildings;
    [SerializeField] GameObject[] industrialBuildings;
    [SerializeField] GameObject[] trees;
    [SerializeField] GameObject[] fences;
    [SerializeField] GameObject[] paths;
    
    // UI Foldouts
    bool showSettings = true;
    bool showRoads = false;
    bool showBuildings = true;
    
    // UI state
    Vector2 scrollPos;
    SerializedObject so;

    // Grid data
    enum CellType { Empty, Road, Residential, Commercial, Industrial, Park }
    CellType[,] grid;

    [MenuItem("Tools/Advanced City Generator")]
    public static void ShowWindow()
    {
        GetWindow<AdvancedCityGenerator>("Advanced City Gen");
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

        showSettings = EditorGUILayout.Foldout(showSettings, "1. Settings & Zoning", true, EditorStyles.foldoutHeader);
        if (showSettings)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("Map Size", EditorStyles.boldLabel);
            mapWidth = EditorGUILayout.IntField("Width (Cells)", mapWidth);
            mapHeight = EditorGUILayout.IntField("Height (Cells)", mapHeight);
            cellSize = EditorGUILayout.FloatField("Cell Size", cellSize);
            
            GUILayout.Space(5);
            GUILayout.Label("Zoning Settings", EditorStyles.boldLabel);
            minZoneSize = EditorGUILayout.IntSlider("Min Block Size", minZoneSize, 3, 10);
            maxZoneSize = EditorGUILayout.IntSlider("Max Block Size", maxZoneSize, 5, 20);
            seed = EditorGUILayout.IntField("Random Seed", seed);
            GUILayout.EndVertical();
        }
        GUILayout.Space(5);

        showRoads = EditorGUILayout.Foldout(showRoads, "2. Core Prefabs (Roads)", true, EditorStyles.foldoutHeader);
        if (showRoads)
        {
            GUILayout.BeginVertical("box");
            DrawRoadField("Straight", "roadStraight", "rotStraight");
            DrawRoadField("Corner", "roadCorner", "rotCorner");
            DrawRoadField("T-Split", "roadT", "rotT");
            DrawRoadField("Cross", "roadCross", "rotCross");
            DrawRoadField("Dead End", "roadEnd", "rotEnd");
            GUILayout.EndVertical();
        }
        GUILayout.Space(5);

        showBuildings = EditorGUILayout.Foldout(showBuildings, "3. Buildings & Props", true, EditorStyles.foldoutHeader);
        if (showBuildings)
        {
            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(so.FindProperty("residentialBuildings"), new GUIContent("Residential (Suburban)"), true);
            rotResidential = EditorGUILayout.FloatField("Rot Offset", rotResidential, GUILayout.Width(150));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(so.FindProperty("commercialBuildings"), new GUIContent("Commercial (Shops)"), true);
            rotCommercial = EditorGUILayout.FloatField("Rot Offset", rotCommercial, GUILayout.Width(150));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(so.FindProperty("industrialBuildings"), new GUIContent("Industrial (Factories)"), true);
            rotIndustrial = EditorGUILayout.FloatField("Rot Offset", rotIndustrial, GUILayout.Width(150));
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            EditorGUILayout.PropertyField(so.FindProperty("trees"), true);
            EditorGUILayout.PropertyField(so.FindProperty("fences"), true);
            EditorGUILayout.PropertyField(so.FindProperty("paths"), true);
            rotProp = EditorGUILayout.FloatField("Prop Rot Offset", rotProp);
            GUILayout.EndVertical();
        }

        GUILayout.Space(20);
        
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("Auto-Load Kenney Assets", GUILayout.Height(30)))
        {
            AutoLoadAssets();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(5);
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Generate Advanced City", GUILayout.Height(40)))
        {
            GenerateCity();
        }
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("Clear City"))
        {
            ClearCity();
        }

        GUILayout.EndScrollView();
        so.ApplyModifiedProperties();
    }

    void AutoLoadAssets()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        string[] modelGuids = AssetDatabase.FindAssets("t:Model");
        
        List<string> allGuids = new List<string>();
        allGuids.AddRange(prefabGuids);
        allGuids.AddRange(modelGuids);
        
        List<GameObject> resList = new List<GameObject>();
        List<GameObject> comList = new List<GameObject>();
        List<GameObject> indList = new List<GameObject>();
        List<GameObject> treesList = new List<GameObject>();
        List<GameObject> fencelist = new List<GameObject>();
        List<GameObject> pathList = new List<GameObject>();

        foreach (string guid in allGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();

            if (!path.Contains("kenney_city-kit") && !path.Contains("CityKit")) continue; 

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            if (name == "road-straight" || name == "road_straight") roadStraight = prefab;
            else if (name == "road-bend" || name == "road-corner" || name == "road_corner") roadCorner = prefab;
            else if (name == "road-split" || name == "road-t" || name == "road_tsplit") roadT = prefab;
            else if (name == "road-crossroad" || name == "road-intersection" || name == "road_cross") roadCross = prefab;
            else if (name == "road-end" || name == "road_end") roadEnd = prefab;
            else if (name.StartsWith("tree")) treesList.Add(prefab);
            else if (name.StartsWith("fence") && !name.Contains("barrier")) fencelist.Add(prefab);
            else if (name.StartsWith("path-") || name.StartsWith("driveway-") || name.StartsWith("path_")) pathList.Add(prefab);
            
            // Smart Building Sorter by Folder Name
            else if (name.StartsWith("building") || name.Contains("house") || name.Contains("shop") || name.Contains("factory"))
            {
                if (path.ToLower().Contains("suburban") || path.ToLower().Contains("residential"))
                {
                    if (!resList.Contains(prefab)) resList.Add(prefab);
                }
                else if (path.ToLower().Contains("commercial"))
                {
                    if (!comList.Contains(prefab)) comList.Add(prefab);
                }
                else if (path.ToLower().Contains("industrial"))
                {
                    if (!indList.Contains(prefab)) indList.Add(prefab);
                }
                else // Fallback
                {
                    if (name.EndsWith("-h") || name.EndsWith("-i") || name.EndsWith("-j") || name.EndsWith("-k") || name.EndsWith("-l") || name.EndsWith("-m") || name.EndsWith("-n"))
                        { if (!comList.Contains(prefab)) comList.Add(prefab); }
                    else if (name.EndsWith("-o") || name.EndsWith("-p") || name.EndsWith("-q") || name.EndsWith("-r") || name.EndsWith("-s") || name.EndsWith("-t") || name.EndsWith("-u"))
                        { if (!indList.Contains(prefab)) indList.Add(prefab); }
                    else
                        { if (!resList.Contains(prefab)) resList.Add(prefab); }
                }
            }
        }

        residentialBuildings = resList.ToArray();
        commercialBuildings = comList.ToArray();
        industrialBuildings = indList.ToArray();
        trees = treesList.ToArray();
        fences = fencelist.ToArray();
        paths = pathList.ToArray();

        Debug.Log($"Auto-loaded {residentialBuildings.Length} Res (Suburban), {commercialBuildings.Length} Com, {industrialBuildings.Length} Ind, {trees.Length} Trees.");
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
        GameObject cityParent = GameObject.Find("AdvancedGeneratedCity");
        if (cityParent != null) DestroyImmediate(cityParent);
    }

    // ==========================================
    // GENERATION LOGIC
    // ==========================================

    class BSPNode
    {
        public int x, y, w, h;
        public BSPNode leftChild, rightChild;
        public CellType zoneType;

        public BSPNode(int x, int y, int w, int h)
        {
            this.x = x; this.y = y; this.w = w; this.h = h;
            zoneType = CellType.Empty;
        }

        public bool Split(int minSize, int maxSize)
        {
            if (leftChild != null || rightChild != null) return false; // Already split
            
            // Randomly pick split direction
            bool splitH = Random.value > 0.5f;

            // Force split logic based on dimensions
            if (w > h && w / h >= 1.25) splitH = false; // Too wide, split vertically
            else if (h > w && h / w >= 1.25) splitH = true; // Too tall, split horizontally
            
            int max = (splitH ? h : w) - minSize;
            if (max <= minSize) return false; // Too small to split

            int splitPos = Random.Range(minSize, max);

            if (splitH)
            {
                leftChild = new BSPNode(x, y, w, splitPos);
                rightChild = new BSPNode(x, y + splitPos, w, h - splitPos);
            }
            else
            {
                leftChild = new BSPNode(x, y, splitPos, h);
                rightChild = new BSPNode(x + splitPos, y, w - splitPos, h);
            }

            return true;
        }
    }

    void GenerateCity()
    {
        ClearCity();
        Random.InitState(seed);
        GameObject cityParent = new GameObject("AdvancedGeneratedCity");
        grid = new CellType[mapWidth, mapHeight]; // Init all to Empty

        // 1. BSP Tree Generation
        List<BSPNode> leaves = new List<BSPNode>();
        BSPNode root = new BSPNode(0, 0, mapWidth, mapHeight);
        
        Queue<BSPNode> nodesToSplit = new Queue<BSPNode>();
        nodesToSplit.Enqueue(root);

        while (nodesToSplit.Count > 0)
        {
            BSPNode node = nodesToSplit.Dequeue();
            
            // Should we split?
            bool trySplit = true;
            if (node.w <= maxZoneSize && node.h <= maxZoneSize)
            {
                if (Random.value > 0.7f) trySplit = false; // Chance to stop early for varied sizes
            }

            if (trySplit && node.Split(minZoneSize, maxZoneSize))
            {
                nodesToSplit.Enqueue(node.leftChild);
                nodesToSplit.Enqueue(node.rightChild);
            }
            else
            {
                // This is a leaf
                leaves.Add(node);
                
                // Assign Zone Type
                float rand = Random.value;
                if (rand < 0.45f) node.zoneType = CellType.Residential;
                else if (rand < 0.7f) node.zoneType = CellType.Commercial;
                else if (rand < 0.85f) node.zoneType = CellType.Industrial;
                else if (rand < 0.95f) node.zoneType = CellType.Park;
                else node.zoneType = CellType.Empty; 
            }
        }

        // 2. Map Leaves to Grid (Apply roads at boundaries)
        foreach (BSPNode leaf in leaves)
        {
            for (int ix = leaf.x; ix < leaf.x + leaf.w; ix++)
            {
                for (int iy = leaf.y; iy < leaf.y + leaf.h; iy++)
                {
                    // Boundary check for roads
                    if (ix == leaf.x || ix == leaf.x + leaf.w - 1 || 
                        iy == leaf.y || iy == leaf.y + leaf.h - 1)
                    {
                        grid[ix, iy] = CellType.Road;
                    }
                    else
                    {
                        grid[ix, iy] = leaf.zoneType;
                    }
                }
            }
        }

        // 3. Process the Grid and Instantiate
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                Vector3 pos = new Vector3(x * cellSize, 0, y * cellSize);

                if (grid[x, y] == CellType.Road)
                {
                    SpawnRoad(x, y, pos, cityParent.transform);
                }
                else if (grid[x, y] == CellType.Residential || grid[x, y] == CellType.Commercial || grid[x, y] == CellType.Industrial)
                {
                    SpawnBuilding(x, y, pos, cityParent.transform, grid[x, y]);
                }
                else if (grid[x, y] == CellType.Park)
                {
                    SpawnPark(x, y, pos, cityParent.transform);
                }
            }
        }
    }

    void SpawnRoad(int x, int y, Vector3 pos, Transform parent)
    {
        bool n = IsRoad(x, y + 1);
        bool s = IsRoad(x, y - 1);
        bool e = IsRoad(x + 1, y);
        bool w = IsRoad(x - 1, y);
        
        int neighbors = (n?1:0) + (s?1:0) + (e?1:0) + (w?1:0);
        GameObject prefab = roadStraight;
        float rot = 0;
        
        if (neighbors == 4)
        {
            prefab = roadCross; rot = rotCross;
        }
        else if (neighbors == 3)
        {
            prefab = roadT; rot = rotT;
            if (!n) rot += 270;
            else if (!e) rot += 180;
            else if (!s) rot += 90;
            else if (!w) rot += 0;
        }
        else if (neighbors == 2)
        {
            if (n && s) { prefab = roadStraight; rot = 0 + rotStraight; }
            else if (e && w) { prefab = roadStraight; rot = 90 + rotStraight; }
            else
            {
                prefab = roadCorner; rot = rotCorner;
                if (n && e) rot += 0;
                else if (e && s) rot += 90;
                else if (s && w) rot += 180;
                else if (w && n) rot += 270;
            }
        }
        else if (neighbors == 1)
        {
            prefab = roadEnd; rot = rotEnd;
            if (n) rot += 0;
            else if (e) rot += 90;
            else if (s) rot += 180;
            else if (w) rot += 270;
        }

        InstantiateProp(prefab, pos, rot, parent);
    }

    void SpawnBuilding(int x, int y, Vector3 pos, Transform parent, CellType type)
    {
        bool n = IsRoad(x, y + 1);
        bool s = IsRoad(x, y - 1);
        bool e = IsRoad(x + 1, y);
        bool w = IsRoad(x - 1, y);
        bool nearRoad = n || s || e || w;
        
        GameObject[] sourceArray = residentialBuildings;
        float baseRot = rotResidential;
        if (type == CellType.Commercial) { sourceArray = commercialBuildings; baseRot = rotCommercial; }
        else if (type == CellType.Industrial) { sourceArray = industrialBuildings; baseRot = rotIndustrial; }
        
        if (nearRoad && Random.value > 0.3f && sourceArray != null && sourceArray.Length > 0)
        {
            GameObject prefab = sourceArray[Random.Range(0, sourceArray.Length)];
            
            float rot = 0;
            Vector3 pushBack = Vector3.zero; // Push house back slightly for front yard/driveway
            
            if (n) { rot = 0; pushBack = new Vector3(0, 0, -cellSize * 0.2f); }
            else if (e) { rot = 90; pushBack = new Vector3(-cellSize * 0.2f, 0, 0); }
            else if (s) { rot = 180; pushBack = new Vector3(0, 0, cellSize * 0.2f); }
            else if (w) { rot = 270; pushBack = new Vector3(cellSize * 0.2f, 0, 0); }

            InstantiateProp(prefab, pos + pushBack, rot + baseRot, parent);

            // Optional Driveway
            if (Random.value > 0.5f && paths != null && paths.Length > 0)
            {
                // Place driveway closer to the road
                InstantiateProp(paths[Random.Range(0, paths.Length)], pos, rot + rotProp, parent);
            }
        }
        else if (trees != null && trees.Length > 0 && Random.value > 0.8f) // Backyards
        {
             InstantiateProp(trees[Random.Range(0, trees.Length)], pos, Random.Range(0,4)*90f + rotProp, parent);
        }
    }

    void SpawnPark(int x, int y, Vector3 pos, Transform parent)
    {
        if (Random.value > 0.6f && trees != null && trees.Length > 0)
        {
            // cluster trees somewhat randomly
            float offset_x = Random.Range(-cellSize*0.3f, cellSize*0.3f);
            float offset_z = Random.Range(-cellSize*0.3f, cellSize*0.3f);
            Vector3 jigglePos = pos + new Vector3(offset_x, 0, offset_z);
            
            InstantiateProp(trees[Random.Range(0, trees.Length)], jigglePos, Random.Range(0, 360f) + rotProp, parent);
        }
        else if (Random.value > 0.8f && paths != null && paths.Length > 0)
        {
             InstantiateProp(paths[Random.Range(0, paths.Length)], pos, Random.Range(0,4)*90f + rotProp, parent);
        }
    }

    void InstantiateProp(GameObject prefab, Vector3 pos, float rotation, Transform parent)
    {
        if (prefab == null) return;
        GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        obj.transform.position = pos;
        obj.transform.rotation = Quaternion.Euler(0, rotation, 0);
        
        EnsureCollider(obj);
    }

    void EnsureCollider(GameObject obj)
    {
        if (obj.GetComponent<Collider>() == null && obj.GetComponentInChildren<Collider>() == null)
        {
            MeshFilter[] filters = obj.GetComponentsInChildren<MeshFilter>();
            foreach (MeshFilter filter in filters)
            {
                if (filter.gameObject.GetComponent<Collider>() == null)
                    filter.gameObject.AddComponent<MeshCollider>();
            }
        }
    }

    bool IsRoad(int x, int y)
    {
        if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return false;
        return grid[x, y] == CellType.Road;
    }
}
