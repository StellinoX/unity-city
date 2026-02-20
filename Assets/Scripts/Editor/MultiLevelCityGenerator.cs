using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MultiLevelCityGenerator : EditorWindow
{
    [SerializeField] int mapWidth = 30;
    [SerializeField] int mapHeight = 30;
    [SerializeField] float cellSize = 10f;
    [SerializeField] int minBlockSize = 2;
    [SerializeField] int maxBlockSize = 5;
    [SerializeField] float zoningScale = 0.05f;
    [SerializeField, Range(0f, 1f)] float buildingDensity = 0.95f;
    [SerializeField] float elevationHeight = 5f; // Height of one elevation level
    [SerializeField] int seed = 12345;
    
    [Header("Elevation Settings")]
    [SerializeField] Color foundationColor = new Color(0.44f, 0.65f, 0.43f); // A slight grass green
    [SerializeField, Range(0f, 1f)] float hillFrequency = 0.3f; // Chance to spawn a hill
    [SerializeField] int maxElevation = 1; // 0 = flat, 1 = one level up
    
    // --- Prefabs ---
    // Roads
    [SerializeField] GameObject roadStraight;
    [SerializeField] GameObject roadCorner;
    [SerializeField] GameObject roadT;
    [SerializeField] GameObject roadCross;
    [SerializeField] GameObject roadEnd;
    [SerializeField] GameObject roadSlant; // The slope!

    // Rotations for calibration
    [SerializeField] float rotStraight = 0;
    [SerializeField] float rotCorner = 0;
    [SerializeField] float rotT = 0;
    [SerializeField] float rotCross = 0;
    [SerializeField] float rotEnd = 0;
    [SerializeField] float rotSlant = 0;
    
    [Header("Building Rotations")]
    [SerializeField] float rotResidential = 0;
    [SerializeField] float rotCommercial = 0;
    [SerializeField] float rotIndustrial = 0;
    [SerializeField] float rotProp = 0;

    // Foundations
    [SerializeField] GameObject tileHigh; // Block under elevated buildings
    
    // Props & Buildings
    [SerializeField] GameObject[] residentialBuildings;
    [SerializeField] GameObject[] commercialBuildings;
    [SerializeField] GameObject[] industrialBuildings;
    [SerializeField] GameObject[] trees;
    
    // UI Foldouts
    bool showMapScale = true;
    bool showRoads = false;
    bool showBuildings = true;
    
    // UI state
    Vector2 scrollPos;
    SerializedObject so;

    // Grid data
    enum CellType { Empty, Road, Residential, Commercial, Industrial }
    
    struct CellData
    {
        public CellType type;
        public int elevation;
    }
    
    CellData[,] grid;

    [MenuItem("Tools/Multi-Level City Generator")]
    public static void ShowWindow()
    {
        GetWindow<MultiLevelCityGenerator>("3D City Gen");
    }

    void OnEnable()
    {
        so = new SerializedObject(this);
        if (residentialBuildings == null || residentialBuildings.Length == 0)
        {
            AutoLoadAssets();
        }
    }

    void OnGUI()
    {
        if (so == null) so = new SerializedObject(this);
        so.Update();

        scrollPos = GUILayout.BeginScrollView(scrollPos);

        // --- MAP & ELEVATION SECTION ---
        showMapScale = EditorGUILayout.Foldout(showMapScale, "1. Map Size & Elevation", true, EditorStyles.foldoutHeader);
        if (showMapScale)
        {
            GUILayout.BeginVertical("box");
            mapWidth = EditorGUILayout.IntField("Width (Cells)", mapWidth);
            mapHeight = EditorGUILayout.IntField("Height (Cells)", mapHeight);
            cellSize = EditorGUILayout.FloatField("Cell Size (XZ)", cellSize);
            minBlockSize = EditorGUILayout.IntSlider("Min Block Size", minBlockSize, 2, 8);
            maxBlockSize = EditorGUILayout.IntSlider("Max Block Size", maxBlockSize, 2, 8);
            if (minBlockSize > maxBlockSize) maxBlockSize = minBlockSize;
            zoningScale = EditorGUILayout.Slider("Zoning Scale (0.05=Big, 0.2=Small)", zoningScale, 0.01f, 0.3f);
            buildingDensity = EditorGUILayout.Slider("Building Density", buildingDensity, 0f, 1f);
            elevationHeight = EditorGUILayout.FloatField("Elevation Step (Y)", elevationHeight);
            foundationColor = EditorGUILayout.ColorField("Foundation Color", foundationColor);
            hillFrequency = EditorGUILayout.Slider("Hill Frequency", hillFrequency, 0f, 1f);
            seed = EditorGUILayout.IntField("Random Seed", seed);
            GUILayout.EndVertical();
        }
        GUILayout.Space(5);

        // --- ROADS SECTION ---
        showRoads = EditorGUILayout.Foldout(showRoads, "2. Core Prefabs (Roads & Tiles)", true, EditorStyles.foldoutHeader);
        if (showRoads)
        {
            GUILayout.BeginVertical("box");
            DrawRoadField("Straight", "roadStraight", "rotStraight");
            DrawRoadField("Corner", "roadCorner", "rotCorner");
            DrawRoadField("T-Split", "roadT", "rotT");
            DrawRoadField("Cross", "roadCross", "rotCross");
            DrawRoadField("Dead End", "roadEnd", "rotEnd");
            DrawRoadField("Slant (Slope)", "roadSlant", "rotSlant");
            EditorGUILayout.PropertyField(so.FindProperty("tileHigh"), new GUIContent("Tile High (Foundation)"));
            GUILayout.EndVertical();
        }
        GUILayout.Space(5);

        // --- BUILDINGS SECTION ---
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
        if (GUILayout.Button("Generate Multi-Level City", GUILayout.Height(40)))
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

        foreach (string guid in allGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();

            if (!path.Contains("kenney_city-kit") && !path.Contains("CityKit")) continue; 
            
            // Only load FBX to avoid duplicates if multiple formats exist
            if (path.ToLower().Contains(".obj") || path.ToLower().Contains(".glb") || path.ToLower().Contains(".gltf")) continue;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            if (name == "road-straight" || name == "road_straight") roadStraight = prefab;
            else if (name == "road-bend" || name == "road-corner" || name == "road_corner") roadCorner = prefab;
            else if (name == "road-split" || name == "road-t" || name == "road_tsplit") roadT = prefab;
            else if (name == "road-crossroad" || name == "road-intersection" || name == "road_cross") roadCross = prefab;
            else if (name == "road-end" || name == "road_end") roadEnd = prefab;
            else if (name == "road-slant") roadSlant = prefab;
            else if (name == "tile-high") tileHigh = prefab;
            else if (name.StartsWith("tree")) treesList.Add(prefab);
            
            // Smart Building Sorter by Folder Name
            else if (path.ToLower().Contains("suburban") || path.ToLower().Contains("residential"))
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
            else if (name.Contains("building") || name.Contains("house") || name.Contains("shop") || name.Contains("factory"))
            {
                // Fallback for old folder names
                // Basic letter categorization if folder match fails
                if (name.EndsWith("-h") || name.EndsWith("-i") || name.EndsWith("-j") || name.EndsWith("-k") || name.EndsWith("-l") || name.EndsWith("-m") || name.EndsWith("-n"))
                    { if (!comList.Contains(prefab)) comList.Add(prefab); }
                else if (name.EndsWith("-o") || name.EndsWith("-p") || name.EndsWith("-q") || name.EndsWith("-r") || name.EndsWith("-s") || name.EndsWith("-t") || name.EndsWith("-u"))
                    { if (!indList.Contains(prefab)) indList.Add(prefab); }
                else
                    { if (!resList.Contains(prefab)) resList.Add(prefab); }
            }
        }

        residentialBuildings = resList.ToArray();
        commercialBuildings = comList.ToArray();
        industrialBuildings = indList.ToArray();
        trees = treesList.ToArray();
        
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
        GameObject cityParent = GameObject.Find("MultiLevelCity");
        if (cityParent != null) DestroyImmediate(cityParent);
    }

    // ==========================================
    // MULTI-LEVEL GENERATION LOGIC
    // ==========================================

    void GenerateCity()
    {
        ClearCity();
        Random.InitState(seed);
        GameObject cityParent = new GameObject("MultiLevelCity");
        grid = new CellData[mapWidth, mapHeight];
        
        // 1. Generate Heightmap (Chunky blocks)
        GenerateHeightmap();

        // 2. Generate Basic Road Network (Grid)
        GenerateRoadNetwork();

        // 3. Create Single Base Foundation Piece (The lowest ground level)
        GameObject baseFoundation = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseFoundation.name = "CityBaseFoundation";
        baseFoundation.transform.SetParent(cityParent.transform);
        baseFoundation.transform.position = new Vector3((mapWidth - 1) * cellSize / 2f, -elevationHeight / 2f, (mapHeight - 1) * cellSize / 2f);
        baseFoundation.transform.localScale = new Vector3(mapWidth * cellSize, elevationHeight, mapHeight * cellSize);
        ApplyFoundationColor(baseFoundation);

        // 4. Instantiate Objects
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                CellData cell = grid[x, y];
                Vector3 pos = new Vector3(x * cellSize, cell.elevation * elevationHeight, y * cellSize);

                // Spawn Foundation columns ONLY for elevated cells (e=1 and up)
                if (tileHigh != null && cell.elevation > 0)
                {
                    for (int e = 1; e <= cell.elevation; e++)
                    {
                        Vector3 foundationPos = new Vector3(x * cellSize, (e - 1) * elevationHeight, y * cellSize);
                        GameObject foundObj = InstantiateProp(tileHigh, foundationPos, 0, cityParent.transform, "Foundation");
                        ApplyFoundationColor(foundObj);
                    }
                }

                if (cell.type == CellType.Road)
                {
                    SpawnMultiLevelRoad(x, y, pos, cityParent.transform);
                }
                else if (cell.type == CellType.Residential || cell.type == CellType.Commercial || cell.type == CellType.Industrial)
                {
                    SpawnBuilding(x, y, pos, cityParent.transform, cell.type);
                }
            }
        }
    }

    void GenerateHeightmap()
    {
        float seedOffset = seed * 0.1f;

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                grid[x, y].elevation = 0; // Default flat
                
                // Block-based Zoning Distribution using Perlin Noise
                float noiseVal = Mathf.PerlinNoise(x * zoningScale + seedOffset, y * zoningScale + seedOffset);
                
                // Group the areas smoothly
                if (noiseVal > 0.65f) grid[x, y].type = CellType.Industrial;
                else if (noiseVal > 0.35f) grid[x, y].type = CellType.Commercial;
                else grid[x, y].type = CellType.Residential;
            }
        }

        // Drop "hill bombs"
        int numHills = Mathf.RoundToInt((mapWidth * mapHeight) / 50f * hillFrequency);
        for (int i = 0; i < numHills; i++)
        {
            int cx = Random.Range(3, mapWidth - 3);
            int cy = Random.Range(3, mapHeight - 3);
            int radius = Random.Range(2, 6);

            for (int hx = cx - radius; hx <= cx + radius; hx++)
            {
                for (int hy = cy - radius; hy <= cy + radius; hy++)
                {
                    if (hx >= 0 && hx < mapWidth && hy >= 0 && hy < mapHeight)
                    {
                        // Make a rough circle
                        if (Vector2.Distance(new Vector2(cx, cy), new Vector2(hx, hy)) <= radius)
                        {
                            grid[hx, hy].elevation = maxElevation;
                        }
                    }
                }
            }
        }
    }

    void GenerateRoadNetwork()
    {
        HashSet<int> roadX = new HashSet<int>();
        HashSet<int> roadY = new HashSet<int>();

        int currentX = 0;
        while (currentX < mapWidth)
        {
            roadX.Add(currentX);
            currentX += Random.Range(minBlockSize, maxBlockSize + 1);
        }
        roadX.Add(mapWidth - 1);

        int currentY = 0;
        while (currentY < mapHeight)
        {
            roadY.Add(currentY);
            currentY += Random.Range(minBlockSize, maxBlockSize + 1);
        }
        roadY.Add(mapHeight - 1);

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                bool isRoadX = roadX.Contains(x);
                bool isRoadY = roadY.Contains(y);

                if (isRoadX || isRoadY)
                {
                    grid[x, y].type = CellType.Road;
                }
            }
        }
        
        // Fix Slopes: A road cannot instantly jump elevation. 
        // If a road cell connects elevation 0 to elevation 1, it must become a ramp.
        // We will handle the visualization of the ramp in SpawnMultiLevelRoad.
    }

    void SpawnMultiLevelRoad(int x, int y, Vector3 pos, Transform parent)
    {
        int myElev = grid[x, y].elevation;
        
        CellData nCell = GetCell(x, y + 1);
        CellData sCell = GetCell(x, y - 1);
        CellData eCell = GetCell(x + 1, y);
        CellData wCell = GetCell(x - 1, y);

        bool n = nCell.type == CellType.Road;
        bool s = sCell.type == CellType.Road;
        bool e = eCell.type == CellType.Road;
        bool w = wCell.type == CellType.Road;

        // SLOPE DETECTION
        if (n && nCell.elevation > myElev && roadSlant != null) 
        {
            InstantiateProp(roadSlant, pos, 0 + rotSlant, parent, "Slant_N");
            return;
        }
        if (e && eCell.elevation > myElev && roadSlant != null) 
        {
            InstantiateProp(roadSlant, pos, 90 + rotSlant, parent, "Slant_E");
            return;
        }
        if (s && sCell.elevation > myElev && roadSlant != null) 
        {
            InstantiateProp(roadSlant, pos, 180 + rotSlant, parent, "Slant_S");
            return;
        }
        if (w && wCell.elevation > myElev && roadSlant != null) 
        {
            InstantiateProp(roadSlant, pos, 270 + rotSlant, parent, "Slant_W");
            return;
        }

        int neighbors = (n?1:0) + (s?1:0) + (e?1:0) + (w?1:0);
        GameObject prefab = roadStraight;
        float rot = 0;
        
        if (neighbors == 4) { prefab = roadCross; rot = rotCross; }
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
            else // Corner
            {
                prefab = roadCorner; rot = rotCorner;
                if (n && e) rot += 0;
                else if (e && s) rot += 90;
                else if (s && w) rot += 180;
                else if (w && n) rot += 270;
            }
        }
        else if (neighbors == 1) // Dead End
        {
            prefab = roadEnd; rot = rotEnd;
            if (n) rot += 0;
            else if (e) rot += 90;
            else if (s) rot += 180;
            else if (w) rot += 270;
        }

        InstantiateProp(prefab, pos, rot, parent, "Road");
    }

    void SpawnBuilding(int x, int y, Vector3 pos, Transform parent, CellType type)
    {
        bool nearRoad = GetCell(x,y+1).type == CellType.Road || 
                        GetCell(x,y-1).type == CellType.Road || 
                        GetCell(x+1,y).type == CellType.Road || 
                        GetCell(x-1,y).type == CellType.Road;
        
        GameObject[] sourceArray = residentialBuildings;
        float baseRot = rotResidential;
        if (type == CellType.Commercial) { sourceArray = commercialBuildings; baseRot = rotCommercial; }
        else if (type == CellType.Industrial) { sourceArray = industrialBuildings; baseRot = rotIndustrial; }

        if (Random.value < buildingDensity && sourceArray != null && sourceArray.Length > 0)
        {
            GameObject prefab = sourceArray[Random.Range(0, sourceArray.Length)];
            if (prefab == null) return;
            
            float rot = 0;
            
            // Face the road if near one, otherwise random rotation
            if (GetCell(x,y+1).type == CellType.Road) rot = 0;
            else if (GetCell(x+1,y).type == CellType.Road) rot = 90;
            else if (GetCell(x,y-1).type == CellType.Road) rot = 180;
            else if (GetCell(x-1,y).type == CellType.Road) rot = 270;
            else rot = Random.Range(0, 4) * 90f;

            GameObject obj = InstantiateProp(prefab, pos, rot + baseRot, parent, type.ToString());
            if (obj != null) FitToCell(obj, pos);
        }
        else if (trees != null && trees.Length > 0 && Random.value > 0.5f)
        {
             GameObject prefab = trees[Random.Range(0, trees.Length)];
             if (prefab == null) return;
             
             GameObject tree = InstantiateProp(prefab, pos, Random.Range(0,4)*90f + rotProp, parent, "Tree");
             if (tree != null) FitToCell(tree, pos);
        }
    }

    CellData GetCell(int x, int y)
    {
        if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) 
        {
            return new CellData { type = CellType.Empty, elevation = 0 };
        }
        return grid[x, y];
    }

    GameObject InstantiateProp(GameObject prefab, Vector3 pos, float rotation, Transform parent, string name)
    {
        if (prefab == null) return null;
        GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        obj.transform.position = pos;
        obj.transform.rotation = Quaternion.Euler(0, rotation, 0);
        obj.name = name;
        EnsureCollider(obj);
        return obj;
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

    void FitToCell(GameObject obj, Vector3 targetPos)
    {
        if (obj == null) return;
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            
            // XZ Centering to prevent pivots from pushing the building into the street
            Vector3 offset = targetPos - bounds.center;
            offset.y = 0; // Keep Y as is (ground level)
            
            // Only apply offset if it's not NaN or Infinity
            if (!float.IsNaN(offset.x) && !float.IsNaN(offset.z) && !float.IsInfinity(offset.x) && !float.IsInfinity(offset.z))
            {
                obj.transform.position += offset;
                bounds.center += offset;
            }
            
            float maxDim = Mathf.Max(bounds.size.x, bounds.size.z);
            float maxAllowed = cellSize * 0.95f; // Leave a small 5% margin so they don't touch roads
            
            // Prevent scaling to 0 or infinity
            if (maxDim > maxAllowed && maxDim > 0.1f)
            {
                float scale = maxAllowed / maxDim;
                if (!float.IsNaN(scale) && !float.IsInfinity(scale) && scale > 0)
                {
                    obj.transform.localScale *= scale;
                }
            }
        }
    }

    void ApplyFoundationColor(GameObject obj)
    {
        if (obj == null) return;
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach(Renderer r in renderers)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor("_Color", foundationColor); // Standard Shader
            block.SetColor("_BaseColor", foundationColor); // URP/HDRP
            r.SetPropertyBlock(block);
        }
    }
}
