
using UnityEngine;
using UnityEditor;
using System.IO;

public class PrefabCreator : EditorWindow
{
    string sourcePath = "Assets/kenney_city-kit-suburban_20/Models";
    string destinationPath = "Assets/Prefabs/CityKit";
    bool addMeshCollider = true;
    bool addBoxCollider = false;

    [MenuItem("Tools/Prefab Creator")]
    public static void ShowWindow()
    {
        GetWindow<PrefabCreator>("Prefab Creator");
    }

    void OnGUI()
    {
        GUILayout.Label("Prefab Generator Settings", EditorStyles.boldLabel);
        
        sourcePath = EditorGUILayout.TextField("Source Folder (Relative)", sourcePath);
        destinationPath = EditorGUILayout.TextField("Destination Folder (Relative)", destinationPath);
        
        addMeshCollider = EditorGUILayout.Toggle("Add Mesh Collider", addMeshCollider);
        addBoxCollider = EditorGUILayout.Toggle("Add Box Collider", addBoxCollider);

        if (GUILayout.Button("Generate Prefabs"))
        {
            GeneratePrefabs();
        }
    }

    void GeneratePrefabs()
    {
        // Ensure destination folder exists
        if (!AssetDatabase.IsValidFolder(destinationPath))
        {
            string[] folders = destinationPath.Split('/');
            string currentPath = folders[0]; // "Assets"
            for (int i = 1; i < folders.Length; i++)
            {
                if (!AssetDatabase.IsValidFolder(currentPath + "/" + folders[i]))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath += "/" + folders[i];
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { sourcePath });
        int count = 0;

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            
            if (model == null) continue;

            // Create instance
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            
            // Add Colliders
            if (addMeshCollider)
            {
                foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.gameObject.GetComponent<MeshCollider>() == null)
                        filter.gameObject.AddComponent<MeshCollider>();
                }
            }
            if (addBoxCollider)
            {
                 if (instance.GetComponent<BoxCollider>() == null)
                    instance.AddComponent<BoxCollider>();
            }

            // Save as Prefab
            string fileName = model.name;
            string prefabPath = destinationPath + "/" + fileName + ".prefab";
            
            // Generate unique path if exists
            prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);

            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            DestroyImmediate(instance);
            count++;
        }
        
        Debug.Log($"Generated {count} prefabs in {destinationPath}");
        AssetDatabase.Refresh();
    }
}
