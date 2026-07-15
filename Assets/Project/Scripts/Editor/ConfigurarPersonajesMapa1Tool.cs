using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class ConfigurarPersonajesMapa1Tool
{
    private const string MapScenePath = "Assets/Project/Scenes/MapaMundial.unity";
    private const string ScenesFolder = "Assets/Project/Scenes";
    private const string HunterPrefabPath = "Assets/Project/Prefabs/Player/Cazador.prefab";
    private const string BossPrefabPath =
        "Assets/Project/Prefabs/Bosses/MutantConRobots.prefab";
    private static readonly string[] RobotPrefabPaths =
    {
        "Assets/Project/Prefabs/Fallas/Robots/RobotFallaRastrera.prefab",
        "Assets/Project/Prefabs/Fallas/Robots/RobotFallaExplosiva.prefab",
        "Assets/Project/Prefabs/Fallas/Robots/RobotFallaGeneradora.prefab"
    };
    private const float RobotScaleMultiplier = 0.3f;

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

        EnsureBootstrap(hunter);
        ConfigureMutantCPU(boss, hunter);
        List<GameObject> robots = PlaceRobots(mapScene, hunter, boss);
        ConfigureHunterCamera(mapScene, hunter);
        EnsureSinglePlayerInput(mapScene, hunter);

        float hunterHeight = CalculateBounds(hunter).size.y;
        float bossHeight = CalculateBounds(boss).size.y;
        int lightCount = ConfigureObject57Lights(mapScene, Mathf.Max(9f, hunterHeight * 0.8f));

        EditorSceneManager.MarkSceneDirty(mapScene);
        EditorSceneManager.SaveScene(mapScene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"Personajes del Mapa 1 configurados. " +
            $"Cazador: {hunterHeight:F2} m, Mutant: {bossHeight:F2} m, " +
            $"Robots: {robots.Count}, " +
            $"Y Cazador: {hunter.transform.position.y:F2}, " +
            $"Y Mutant: {boss.transform.position.y:F2}, luces Object_57: {lightCount}."
        );

        if (openedByTool)
        {
            EditorSceneManager.CloseScene(mapScene, true);
        }
    }

    private static void ConfigureMutantCPU(GameObject boss, GameObject hunter)
    {
        MutantControlMode controlMode = boss.GetComponent<MutantControlMode>();
        if (controlMode != null)
        {
            SerializedObject data = new SerializedObject(controlMode);
            data.FindProperty("controlHumanoActivo").boolValue = false;
            data.FindProperty("desactivarOtrosPlayerInput").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach (PlayerInput input in boss.GetComponentsInChildren<PlayerInput>(true))
        {
            input.enabled = false;
        }

        foreach (MutantInputReader reader in boss.GetComponentsInChildren<MutantInputReader>(true))
        {
            reader.enabled = false;
        }

        foreach (Camera camera in boss.GetComponentsInChildren<Camera>(true))
        {
            camera.enabled = false;
            camera.gameObject.SetActive(false);
        }

        foreach (AudioListener listener in boss.GetComponentsInChildren<AudioListener>(true))
        {
            listener.enabled = false;
        }

        MutantCameraController camController =
            boss.GetComponentInChildren<MutantCameraController>(true);
        if (camController != null)
        {
            camController.enabled = false;
        }

        MutantEnemyIntentSource ai = boss.GetComponent<MutantEnemyIntentSource>();
        if (ai != null)
        {
            CazadorStats cazadorStats = hunter.GetComponent<CazadorStats>();
            if (cazadorStats != null)
            {
                SerializedObject aiData = new SerializedObject(ai);
                aiData.FindProperty("objetivo").objectReferenceValue = cazadorStats;
                aiData.ApplyModifiedPropertiesWithoutUndo();
                ai.SetTarget(cazadorStats);
            }
        }
    }

    private static List<GameObject> PlaceRobots(
        Scene scene,
        GameObject hunter,
        GameObject boss)
    {
        List<GameObject> robots = new List<GameObject>();
        Bounds hunterBounds = CalculateBounds(hunter);
        float hunterHeight = hunterBounds.size.y;
        Vector3 bossPos = boss.transform.position;
        Vector3 hunterPos = hunter.transform.position;
        Vector3 forwardToHunter = (hunterPos - bossPos).normalized;
        forwardToHunter.y = 0f;
        if (forwardToHunter.sqrMagnitude < 0.01f)
        {
            forwardToHunter = Vector3.forward;
        }
        Vector3 right = Vector3.Cross(Vector3.up, forwardToHunter).normalized;

        Vector3[] offsets =
        {
            -right * 4f + forwardToHunter * 2f,
            right * 4f + forwardToHunter * 2f,
            -forwardToHunter * 3f
        };

        for (int i = 0; i < RobotPrefabPaths.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefabPaths[i]);
            if (prefab == null)
            {
                Debug.LogWarning($"No se encontro el prefab de robot: {RobotPrefabPaths[i]}");
                continue;
            }

            GameObject robot = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (robot == null)
            {
                continue;
            }

            robot.name = $"{prefab.name}_MapaMundial";
            robot.transform.localScale = Vector3.one * RobotScaleMultiplier;

            Vector3 desired = bossPos + offsets[i];
            PlaceOnSurface(robot, desired, hunter.transform, boss.transform);

            FallaCore core = robot.GetComponent<FallaCore>();
            if (core != null)
            {
                CazadorStats cazadorStats = hunter.GetComponent<CazadorStats>();
                if (cazadorStats != null)
                {
                    core.SetTarget(cazadorStats.transform);
                }
            }

            robots.Add(robot);
        }

        return robots;
    }

    private static void PlaceOnSurface(
        GameObject target,
        Vector3 desired,
        params Transform[] ignoredRoots)
    {
        Physics.SyncTransforms();
        RaycastHit[] hits = Physics.RaycastAll(
            desired + Vector3.up * 180f,
            Vector3.down,
            360f,
            ~0,
            QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        Vector3 surface = desired;
        foreach (RaycastHit hit in hits)
        {
            if (ignoredRoots.Any(root => root != null && hit.transform.IsChildOf(root)) ||
                hit.transform.IsChildOf(target.transform))
            {
                continue;
            }
            surface = hit.point;
            break;
        }
        target.transform.position = surface;
        Physics.SyncTransforms();
        Bounds bounds = CalculateBounds(target);
        target.transform.position += Vector3.up * (surface.y + 0.05f - bounds.min.y);
    }

    private static void ConfigureHunterCamera(Scene scene, GameObject hunter)
    {
        Camera[] cameras = FindInScene<Camera>(scene);
        Camera mainCamera = null;

        foreach (Camera c in cameras)
        {
            if (c != null && c.gameObject.activeInHierarchy && c.CompareTag("MainCamera"))
            {
                mainCamera = c;
                break;
            }
        }

        if (mainCamera == null)
        {
            foreach (Camera c in cameras)
            {
                if (c != null && c.gameObject.activeInHierarchy)
                {
                    mainCamera = c;
                    break;
                }
            }
        }

        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            mainCamera = cameraObject.AddComponent<Camera>();
        }

        mainCamera.gameObject.SetActive(true);
        mainCamera.gameObject.name = "Main Camera";
        mainCamera.gameObject.tag = "MainCamera";
        mainCamera.enabled = true;
        mainCamera.nearClipPlane = 0.2f;
        mainCamera.farClipPlane = 650f;
        mainCamera.fieldOfView = 62f;

        foreach (AudioListener existing in mainCamera.GetComponentsInChildren<AudioListener>(true))
        {
            existing.enabled = true;
        }
        if (mainCamera.GetComponent<AudioListener>() == null)
        {
            mainCamera.gameObject.AddComponent<AudioListener>().enabled = true;
        }

        foreach (Camera other in cameras)
        {
            if (other != null && other != mainCamera)
            {
                other.enabled = false;
            }
        }

        foreach (AudioListener other in FindInScene<AudioListener>(scene))
        {
            if (other != null && other.transform.parent != mainCamera.transform)
            {
                other.enabled = false;
            }
        }

        Bounds hunterBounds = CalculateBounds(hunter);
        Transform target = hunter.transform.Find("CameraTarget_MapaMundial");
        if (target == null)
        {
            GameObject targetObject = new GameObject("CameraTarget_MapaMundial");
            SceneManager.MoveGameObjectToScene(targetObject, scene);
            targetObject.transform.SetParent(hunter.transform, true);
            target = targetObject.transform;
        }
        target.localPosition = Vector3.up * hunterBounds.extents.y * 0.15f;

        CazadorInputReader input = hunter.GetComponent<CazadorInputReader>();
        if (input == null)
        {
            Debug.LogError("ConfigureHunterCamera: Cazador no tiene CazadorInputReader.", hunter);
        }

        CazadorCameraController controller =
            mainCamera.GetComponent<CazadorCameraController>();
        if (controller == null)
        {
            controller = mainCamera.gameObject.AddComponent<CazadorCameraController>();
        }

        SerializedObject cameraData = new SerializedObject(controller);
        cameraData.FindProperty("target").objectReferenceValue = target;
        cameraData.FindProperty("input").objectReferenceValue = input;
        float distance = Mathf.Clamp(hunterBounds.size.y * 1.05f, 8f, 24f);
        SetFloat(cameraData, "distancia", distance);
        SetFloat(cameraData, "sensibilidadRaton", 0.12f);
        SetFloat(cameraData, "sensibilidadMando", 150f);
        SetFloat(cameraData, "pitchInicial", 15f);
        SetFloat(cameraData, "radioColision",
            Mathf.Clamp(hunterBounds.size.y * 0.035f, 0.25f, 0.8f));
        SetFloat(cameraData, "distanciaMinima", 1.5f);
        SetFloat(cameraData, "margenColision", 0.08f);
        SerializedProperty collisionLayers =
            cameraData.FindProperty("capasColision");
        if (collisionLayers != null)
        {
            collisionLayers.intValue = ~((1 << 8) | (1 << 10));
        }
        cameraData.ApplyModifiedPropertiesWithoutUndo();

        mainCamera.transform.position = target.position +
            new Vector3(0f, hunterBounds.extents.y * 0.35f, -distance);
        mainCamera.transform.LookAt(target);

        CazadorController movement = hunter.GetComponent<CazadorController>();
        if (movement != null)
        {
            SerializedObject movementData = new SerializedObject(movement);
            movementData.FindProperty("camaraTransform").objectReferenceValue =
                mainCamera.transform;
            movementData.ApplyModifiedPropertiesWithoutUndo();
        }

        PlayerInput playerInput = hunter.GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            playerInput.enabled = true;
            SerializedObject inputData = new SerializedObject(playerInput);
            SerializedProperty cameraProperty =
                inputData.FindProperty("m_Camera");
            if (cameraProperty != null)
            {
                cameraProperty.objectReferenceValue = mainCamera;
            }
            inputData.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogError(
                "ConfigureHunterCamera: Cazador no tiene PlayerInput. " +
                "Agrega un componente PlayerInput con PlayerController.inputactions.", hunter);
        }

        CazadorInputReader cazadorInput = hunter.GetComponent<CazadorInputReader>();
        if (cazadorInput != null)
        {
            cazadorInput.enabled = true;
        }
    }

    private static void EnsureSinglePlayerInput(Scene scene, GameObject hunter)
    {
        PlayerInput[] allInputs = FindInScene<PlayerInput>(scene);
        foreach (PlayerInput input in allInputs)
        {
            bool belongsToHunter = input.transform.IsChildOf(hunter.transform) ||
                                   input.gameObject == hunter.gameObject;
            input.enabled = belongsToHunter;
        }

        CazadorInputReader cazadorInput = hunter.GetComponent<CazadorInputReader>();
        if (cazadorInput != null)
        {
            cazadorInput.enabled = true;
        }

        foreach (Camera camera in FindInScene<Camera>(scene))
        {
            if (camera != null)
            {
                bool isMain = camera.CompareTag("MainCamera");
                camera.enabled = isMain;
                if (camera.gameObject.activeInHierarchy)
                {
                    camera.gameObject.SetActive(true);
                }
            }
        }

        foreach (AudioListener listener in FindInScene<AudioListener>(scene))
        {
            if (listener != null)
            {
                bool onMainCamera = listener.GetComponentInParent<Camera>() != null &&
                                    listener.GetComponentInParent<Camera>().CompareTag("MainCamera");
                listener.enabled = onMainCamera;
            }
        }
    }

    private static void EnsureBootstrap(GameObject hunter)
    {
        CazadorCameraBootstrap bootstrap =
            hunter.GetComponent<CazadorCameraBootstrap>();
        if (bootstrap == null)
        {
            bootstrap = hunter.AddComponent<CazadorCameraBootstrap>();
        }
    }

    private static void SetFloat(SerializedObject data, string name, float value)
    {
        SerializedProperty property = data.FindProperty(name);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component =>
        UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include)
            .Where(c => c.gameObject.scene == scene)
            .ToArray();

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
