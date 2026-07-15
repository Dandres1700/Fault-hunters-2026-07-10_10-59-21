using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ConfigurarPersonajesMapa1Tool
{
    private const string MapScenePath = "Assets/Project/Scenes/MapaMundial.unity";
    private const string ScenesFolder = "Assets/Project/Scenes";
    private const string HunterPrefabPath = "Assets/Project/Prefabs/Player/Cazador.prefab";
    private const string BossPrefabPath =
        "Assets/Project/Prefabs/Bosses/MutantConRobots.prefab";

    [MenuItem("Fault Hunters/Configurar personajes y luces del Mapa 1")]
    public static void ConfigureFromMenu()
    {
        ConfigureAll();
    }

    private static void ConfigureAll()
    {
        ConfigurarMapa1Tool.ConfigurarDesdeMenu();
        RemoveCharactersFromOtherScenes();

        Scene mapScene = SceneManager.GetSceneByPath(MapScenePath);
        bool openedByTool = !mapScene.IsValid() || !mapScene.isLoaded;

        if (openedByTool)
        {
            mapScene = EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Additive);
        }

        RemoveCharacters(mapScene);

        GameObject hunter = InstantiateCharacter(
            HunterPrefabPath,
            mapScene,
            "Cazador_Mapa1",
            10f
        );
        GameObject boss = InstantiateCharacter(
            BossPrefabPath,
            mapScene,
            "Mutant_Mapa1",
            35f
        );

        PositionCharacters(mapScene, hunter, boss);
        FaceEachOther(hunter, boss);
        float hunterHeight = CalculateBounds(hunter).size.y;
        float bossHeight = CalculateBounds(boss).size.y;
        int lightCount = ConfigureObject57Lights(mapScene, Mathf.Max(9f, hunterHeight * 0.8f));

        EditorSceneManager.MarkSceneDirty(mapScene);
        EditorSceneManager.SaveScene(mapScene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"Personajes del Mapa 1 configurados. " +
            $"Cazador: {hunterHeight:F2} m, Mutant: {bossHeight:F2} m, " +
            $"Y Cazador: {hunter.transform.position.y:F2}, " +
            $"Y Mutant: {boss.transform.position.y:F2}, luces Object_57: {lightCount}."
        );

        if (openedByTool)
        {
            EditorSceneManager.CloseScene(mapScene, true);
        }
    }

    private static void RemoveCharactersFromOtherScenes()
    {
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { ScenesFolder });

        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (path == MapScenePath)
            {
                continue;
            }

            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedByTool = !scene.IsValid() || !scene.isLoaded;

            if (openedByTool)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            int removed = RemoveCharacters(scene);

            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (openedByTool)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static int RemoveCharacters(Scene scene)
    {
        HashSet<GameObject> rootsToRemove = new HashSet<GameObject>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (CazadorStats stats in root.GetComponentsInChildren<CazadorStats>(true))
            {
                rootsToRemove.Add(GetInstanceRoot(stats.gameObject));
            }

            foreach (MutantStats stats in root.GetComponentsInChildren<MutantStats>(true))
            {
                rootsToRemove.Add(GetInstanceRoot(stats.gameObject));
            }
        }

        foreach (GameObject root in rootsToRemove)
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        return rootsToRemove.Count;
    }

    private static GameObject GetInstanceRoot(GameObject target)
    {
        GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(target);
        return prefabRoot != null ? prefabRoot : target.transform.root.gameObject;
    }

    private static GameObject InstantiateCharacter(
        string prefabPath,
        Scene scene,
        string instanceName,
        float targetScale)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            throw new InvalidOperationException($"No se encontro el prefab '{prefabPath}'.");
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;

        if (instance == null)
        {
            throw new InvalidOperationException($"No se pudo instanciar '{prefab.name}'.");
        }

        instance.name = instanceName;
        instance.transform.position = Vector3.zero;
        instance.transform.localScale = Vector3.one * targetScale;
        return instance;
    }

    private static void PositionCharacters(Scene scene, GameObject hunter, GameObject boss)
    {
        Bounds hunterBounds = CalculateBounds(hunter);
        Bounds bossBounds = CalculateBounds(boss);
        float separation = Mathf.Max(3f, (hunterBounds.size.x + bossBounds.size.x) * 0.8f);

        hunter.transform.position = new Vector3(-separation, 0f, -separation * 0.5f);
        boss.transform.position = new Vector3(separation, 0f, separation * 0.5f);

        GameObject city = FindRoot(scene, "Mapa1_CiudadAbandonada");

        if (city == null)
        {
            throw new InvalidOperationException("No se encontro la ciudad del Mapa 1.");
        }

        Physics.SyncTransforms();
        float hunterGround = FindSurfaceHeight(city, hunter.transform.position);
        float bossGround = FindSurfaceHeight(city, boss.transform.position);

        hunterBounds = CalculateBounds(hunter);
        bossBounds = CalculateBounds(boss);
        hunter.transform.position += Vector3.up * (hunterGround + 0.05f - hunterBounds.min.y);
        boss.transform.position += Vector3.up * (bossGround + 0.05f - bossBounds.min.y);
    }

    private static float FindSurfaceHeight(GameObject city, Vector3 position)
    {
        Bounds cityBounds = CalculateBounds(city);
        float extraHeight = Mathf.Max(100f, cityBounds.size.y * 0.05f);
        Vector3 origin = new Vector3(
            position.x,
            cityBounds.max.y + extraHeight,
            position.z
        );
        float distance = cityBounds.size.y + extraHeight * 2f;
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        float highestSurface = float.NegativeInfinity;

        foreach (RaycastHit hit in hits)
        {
            if (!hit.collider.transform.IsChildOf(city.transform) || hit.normal.y < 0.25f)
            {
                continue;
            }

            highestSurface = Mathf.Max(highestSurface, hit.point.y);
        }

        if (float.IsNegativeInfinity(highestSurface))
        {
            throw new InvalidOperationException(
                $"No se encontro suelo de la ciudad debajo de ({position.x:F1}, {position.z:F1})."
            );
        }

        return highestSurface;
    }

    private static void FaceEachOther(GameObject hunter, GameObject boss)
    {
        Vector3 hunterDirection = boss.transform.position - hunter.transform.position;
        hunterDirection.y = 0f;
        Vector3 bossDirection = -hunterDirection;

        if (hunterDirection.sqrMagnitude > 0.001f)
        {
            hunter.transform.rotation = Quaternion.LookRotation(hunterDirection.normalized);
            boss.transform.rotation = Quaternion.LookRotation(bossDirection.normalized);
        }
    }

    private static int ConfigureObject57Lights(Scene scene, float lightRange)
    {
        int count = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform == null ||
                    !string.Equals(transform.name, "Object_57", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Transform previous = transform.Find("Luz_Ambiente_Object57");

                if (previous != null)
                {
                    UnityEngine.Object.DestroyImmediate(previous.gameObject);
                }

                GameObject lightObject = new GameObject("Luz_Ambiente_Object57");
                lightObject.transform.SetParent(transform, false);
                lightObject.transform.localPosition = new Vector3(0f, 0.45f, 0f);

                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.48f, 0.2f, 1f);
                light.intensity = 3.6f;
                light.range = lightRange * 1.35f;
                light.shadows = LightShadows.None;
                count++;
            }
        }

        return count;
    }

    private static Bounds CalculateBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            throw new InvalidOperationException($"'{target.name}' no contiene renderers.");
        }

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                return root;
            }
        }

        return null;
    }
}
