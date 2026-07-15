using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class MapaMundialEnemyIntegrationTool
{
    private const string ScenePath = "Assets/Project/Scenes/MapaMundial.unity";
    private const string MutantPrefabPath =
        "Assets/Project/Prefabs/Bosses/MutantConRobots.prefab";
    private static readonly string[] RobotPrefabPaths =
    {
        "Assets/Project/Prefabs/Fallas/Robots/RobotFallaRastrera.prefab",
        "Assets/Project/Prefabs/Fallas/Robots/RobotFallaExplosiva.prefab",
        "Assets/Project/Prefabs/Fallas/Robots/RobotFallaGeneradora.prefab"
    };

    [MenuItem("Tools/Cazadores de Fallas/Integrar enemigos en Mapa Mundial %#i")]
    public static void Integrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            Debug.LogWarning("Espera a que Unity termine antes de integrar Mapa Mundial.");
            return;
        }

        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.isDirty)
        {
            Debug.LogWarning("Guarda o descarta los cambios de la escena activa antes de integrar.");
            return;
        }

        GameObject mutantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MutantPrefabPath);
        GameObject[] robotPrefabs = RobotPrefabPaths
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .ToArray();
        if (mutantPrefab == null || robotPrefabs.Any(prefab => prefab == null))
        {
            throw new InvalidOperationException(
                "Faltan los prefabs RobotSphere. Ejecuta primero la migracion de Fallas.");
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        CazadorStats[] hunters = FindInScene<CazadorStats>(scene);
        if (hunters.Length != 1)
        {
            throw new InvalidOperationException(
                $"MapaMundial debe contener un Cazador; encontrados: {hunters.Length}.");
        }
        CazadorStats hunter = hunters[0];
        PositionHunterOnObject2(scene, hunter);

        TransformSnapshot mutantPlacement = CaptureMutantPlacement(scene, hunter.transform);
        RemoveExistingEnemies(scene);
        Physics.SyncTransforms();

        GameObject mutant = InstantiatePrefab(mutantPrefab, scene);
        mutant.name = "Mutant_CPU_MapaMundial";
        mutant.transform.SetPositionAndRotation(mutantPlacement.Position, mutantPlacement.Rotation);
        mutant.transform.localScale = mutantPlacement.Scale;
        Physics.SyncTransforms();

        PlaceMutantForPresentation(mutant, hunter, mutantPlacement.Position);
        Physics.SyncTransforms();

        MutantEnemyIntentSource ai = mutant.GetComponent<MutantEnemyIntentSource>();
        MutantFallaController summoner = mutant.GetComponent<MutantFallaController>();
        if (ai == null || summoner == null)
        {
            throw new InvalidOperationException("MutantConRobots no contiene IA o invocador.");
        }
        ai.SetTarget(hunter);
        ConfigureMutantForMap(mutant, hunter, ai, summoner);

        Bounds hunterBounds = CalculateBounds(hunter.gameObject);
        float hunterRadius = Mathf.Max(hunterBounds.extents.x, hunterBounds.extents.z);
        float placementRadius = Mathf.Clamp(hunterRadius * 1.15f, 3f, 9f);
        Vector3[] directions =
        {
            new Vector3(-1f, 0f, 0.75f).normalized,
            new Vector3(1f, 0f, 0.8f).normalized,
            new Vector3(0.15f, 0f, 1f).normalized
        };

        for (int index = 0; index < robotPrefabs.Length; index++)
        {
            GameObject robot = InstantiatePrefab(robotPrefabs[index], scene);
            robot.name = $"{robotPrefabs[index].name}_MapaMundial";
            ScaleRobotForMap(robot, hunterBounds.size.y);
            float distance = index == 0 ? 1.25f :
                index == 1 ? 1.8f : placementRadius + index * 1.4f;
            Vector3 desired = hunter.transform.position + directions[index] * distance;
            PlaceOnSurface(robot, desired, hunter.transform, mutant.transform);
            ConfigureRobotAttackForMap(robot, hunterBounds);
            FallaCore core = robot.GetComponent<FallaCore>();
            core.SetTarget(hunter.transform);
        }

        ConfigureSpawnPoints(mutant, hunter.transform);
        EnsureProbe(scene);

        EnsureSinglePlayerAndPresentationSystems(scene, hunter, mutant);
        ConfigureHunterCamera(scene, hunter);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        ValidateScene(scene);
        Debug.Log(
            "Mapa Mundial integrado: 1 Cazador, Mutant CPU, 3 RobotSphere, " +
            "invocacion configurada y sin controles/camaras enemigos.");
    }

    private static TransformSnapshot CaptureMutantPlacement(Scene scene, Transform hunter)
    {
        MutantStats[] mutants = FindInScene<MutantStats>(scene);
        if (mutants.Length > 0)
        {
            Transform transform = GetInstanceRoot(mutants[0].gameObject).transform;
            return new TransformSnapshot(transform.position, transform.rotation,
                transform.localScale);
        }
        return new TransformSnapshot(
            hunter.position + new Vector3(24f, 0f, 18f),
            Quaternion.LookRotation(hunter.position -
                (hunter.position + new Vector3(24f, 0f, 18f))),
            Vector3.one);
    }

    private static void RemoveExistingEnemies(Scene scene)
    {
        HashSet<GameObject> roots = new HashSet<GameObject>();
        foreach (MutantStats stats in FindInScene<MutantStats>(scene))
        {
            roots.Add(GetInstanceRoot(stats.gameObject));
        }
        foreach (FallaCore core in FindInScene<FallaCore>(scene))
        {
            roots.Add(GetInstanceRoot(core.gameObject));
        }
        foreach (GameObject root in roots)
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ConfigureMutantForMap(
        GameObject mutant,
        CazadorStats hunter,
        MutantEnemyIntentSource ai,
        MutantFallaController summoner)
    {
        Bounds mutantBounds = CalculateBounds(mutant);
        Bounds hunterBounds = CalculateBounds(hunter.gameObject);
        float mutantRadius = Mathf.Max(mutantBounds.extents.x, mutantBounds.extents.z);
        float hunterRadius = Mathf.Max(hunterBounds.extents.x, hunterBounds.extents.z);
        float reach = Mathf.Clamp(mutantRadius * 0.48f + hunterRadius * 0.4f, 2.2f, 24f);
        float initialDistance = HorizontalDistance(mutant.transform.position,
            hunter.transform.position);

        SerializedObject aiData = new SerializedObject(ai);
        SetFloat(aiData, "rangoDeteccion", Mathf.Max(60f, initialDistance * 1.35f));
        SetFloat(aiData, "distanciaDetencion", reach * 0.92f);
        SetFloat(aiData, "rangoAtaque", reach);
        SetFloat(aiData, "radioEvasion", Mathf.Clamp(mutantRadius * 0.18f, 0.8f, 5f));
        SetFloat(aiData, "distanciaEvasion", Mathf.Clamp(mutantRadius * 0.65f, 2f, 12f));
        aiData.FindProperty("objetivo").objectReferenceValue = hunter;
        aiData.ApplyModifiedPropertiesWithoutUndo();

        MutantMotor motor = mutant.GetComponent<MutantMotor>();
        if (motor != null)
        {
            SerializedObject motorData = new SerializedObject(motor);
            SetFloat(motorData, "velocidadCaminar", 10f);
            SetFloat(motorData, "velocidadCorrer", 16f);
            SetFloat(motorData, "aceleracion", 28f);
            SetFloat(motorData, "desaceleracion", 30f);
            motorData.ApplyModifiedPropertiesWithoutUndo();
        }

        MutantAttackHitbox hitbox = mutant.GetComponentInChildren<MutantAttackHitbox>(true);
        if (hitbox != null)
        {
            SerializedObject hitboxData = new SerializedObject(hitbox);
            SerializedProperty origin = hitboxData.FindProperty("origenAtaque");
            if (origin != null)
            {
                origin.objectReferenceValue = mutant.transform;
            }
            SetFloat(hitboxData, "desplazamientoFrontal", reach * 0.5f);
            SetFloat(hitboxData, "radio", Mathf.Clamp(
                Mathf.Max(reach * 0.75f, hunterBounds.extents.y * 0.9f), 3f, 32f));
            hitboxData.ApplyModifiedPropertiesWithoutUndo();
        }

        SerializedObject summonData = new SerializedObject(summoner);
        summonData.FindProperty("objetivo").objectReferenceValue = hunter.transform;
        SetFloat(summonData, "radioAparicion", Mathf.Clamp(mutantRadius * 0.8f, 4f, 16f));
        SetFloat(summonData, "radioLibre", 0.8f);
        SetFloat(summonData, "distanciaMinimaObjetivo", Mathf.Max(3f, hunterRadius * 0.8f));
        summonData.FindProperty("intentosPosicion").intValue = 20;
        summonData.FindProperty("capasSuelo").intValue = ~0;
        summonData.FindProperty("capasBloqueo").intValue = ~((1 << 8) | (1 << 10));
        summonData.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PlaceMutantForPresentation(
        GameObject mutant,
        CazadorStats hunter,
        Vector3 originalPosition)
    {
        Bounds mutantBounds = CalculateBounds(mutant);
        Bounds hunterBounds = CalculateBounds(hunter.gameObject);
        float mutantRadius = Mathf.Max(mutantBounds.extents.x, mutantBounds.extents.z);
        float hunterRadius = Mathf.Max(hunterBounds.extents.x, hunterBounds.extents.z);
        float reach = Mathf.Clamp(mutantRadius * 0.48f + hunterRadius * 0.4f, 2.2f, 24f);
        Vector3 direction = originalPosition - hunter.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Vector3.forward;
        }
        direction.Normalize();
        float distance = Mathf.Clamp(reach * 1.55f, 9f, 38f);
        Vector3 desired = hunter.transform.position + direction * distance;
        PlaceOnSurface(mutant, desired, hunter.transform);
        Vector3 face = hunter.transform.position - mutant.transform.position;
        face.y = 0f;
        if (face.sqrMagnitude > 0.01f)
        {
            mutant.transform.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);
        }
    }

    private static void ConfigureSpawnPoints(GameObject mutant, Transform hunter)
    {
        Transform root = mutant.transform.Find("RobotSpawnPoints");
        if (root == null)
        {
            return;
        }
        float radius = Mathf.Clamp(
            HorizontalDistance(CalculateBounds(mutant).max, CalculateBounds(mutant).min) * 0.22f,
            3f, 14f);
        Vector3 forward = hunter.position - mutant.transform.position;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : mutant.transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3[] offsets = { right * radius, -right * radius, -forward * radius * 0.8f };
        for (int index = 0; index < Mathf.Min(root.childCount, offsets.Length); index++)
        {
            root.GetChild(index).position = mutant.transform.position + offsets[index];
        }
    }

    private static void ConfigureRobotAttackForMap(GameObject robot, Bounds hunterBounds)
    {
        float verticalReach = Mathf.Clamp(hunterBounds.extents.y * 0.9f, 3f, 18f);
        FallaMeleeAttack melee = robot.GetComponent<FallaMeleeAttack>();
        if (melee != null)
        {
            SerializedObject data = new SerializedObject(melee);
            SerializedProperty origin = data.FindProperty("attackOrigin");
            if (origin != null)
            {
                origin.objectReferenceValue = robot.transform;
            }
            SetFloat(data, "radio", verticalReach);
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        FallaExplosiveAttack explosive = robot.GetComponent<FallaExplosiveAttack>();
        if (explosive != null)
        {
            SerializedObject data = new SerializedObject(explosive);
            SetFloat(data, "radioExplosion", verticalReach);
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ScaleRobotForMap(GameObject robot, float hunterHeight)
    {
        Bounds initial = CalculateBounds(robot);
        if (initial.size.y <= 0.01f)
        {
            return;
        }
        float targetHeight = Mathf.Clamp(hunterHeight * 0.5f, 1.8f, 12f);
        float scale = Mathf.Clamp(targetHeight / initial.size.y, 0.75f, 8f);
        robot.transform.localScale = Vector3.one * scale;
        Physics.SyncTransforms();
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

    private static void EnsureSinglePlayerAndPresentationSystems(
        Scene scene,
        CazadorStats hunter,
        GameObject mutant)
    {
        CazadorController hunterController = hunter.GetComponent<CazadorController>();
        if (hunterController != null)
        {
            SerializedObject controllerData = new SerializedObject(hunterController);
            float worldScale = Mathf.Max(
                Mathf.Abs(hunter.transform.lossyScale.x),
                Mathf.Abs(hunter.transform.lossyScale.z));
            SetFloat(controllerData, "radioSuelo", Mathf.Clamp(0.28f * worldScale, 0.3f, 4f));
            SerializedProperty groundLayers = controllerData.FindProperty("capasSuelo");
            if (groundLayers != null)
            {
                groundLayers.intValue = ~0;
            }
            controllerData.ApplyModifiedPropertiesWithoutUndo();
        }
        PlayerInput[] inputs = FindInScene<PlayerInput>(scene);
        foreach (PlayerInput input in inputs)
        {
            bool belongsToHunter = input.transform.IsChildOf(hunter.transform) ||
                                   input.gameObject == hunter.gameObject;
            input.enabled = belongsToHunter;
        }
        foreach (PlayerInput input in mutant.GetComponentsInChildren<PlayerInput>(true))
        {
            input.enabled = false;
        }
        foreach (Camera camera in mutant.GetComponentsInChildren<Camera>(true))
        {
            camera.enabled = false;
        }
        foreach (AudioListener listener in mutant.GetComponentsInChildren<AudioListener>(true))
        {
            listener.enabled = false;
        }
    }

    private static void PositionHunterOnObject2(Scene scene, CazadorStats hunter)
    {
        Transform object2 = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(item => string.Equals(item.name, "Object_2",
                StringComparison.OrdinalIgnoreCase));
        if (object2 == null)
        {
            Debug.LogWarning("No se encontro Object_2; se conserva la posicion del Cazador.");
            return;
        }
        Bounds bounds = CalculateBounds(object2.gameObject);
        Vector3 desired = new Vector3(bounds.center.x, bounds.max.y + 2f, bounds.center.z);
        PlaceOnSurface(hunter.gameObject, desired);
    }

    private static void ConfigureHunterCamera(Scene scene, CazadorStats hunter)
    {
        Camera[] cameras = FindInScene<Camera>(scene);
        Camera camera = cameras.FirstOrDefault(value => value.CompareTag("MainCamera")) ??
                        cameras.FirstOrDefault();
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            camera = cameraObject.AddComponent<Camera>();
        }
        camera.gameObject.name = "Main Camera";
        camera.tag = "MainCamera";
        camera.enabled = true;
        camera.nearClipPlane = 0.2f;
        camera.farClipPlane = 650f;
        camera.fieldOfView = 62f;

        AudioListener selectedListener = camera.GetComponent<AudioListener>() ??
                                         camera.gameObject.AddComponent<AudioListener>();
        selectedListener.enabled = true;
        foreach (Camera other in cameras)
        {
            if (other != camera)
            {
                other.enabled = false;
            }
        }
        foreach (AudioListener other in FindInScene<AudioListener>(scene))
        {
            other.enabled = other == selectedListener;
        }

        Bounds hunterBounds = CalculateBounds(hunter.gameObject);
        Transform target = hunter.transform.Find("CameraTarget_MapaMundial");
        if (target == null)
        {
            GameObject targetObject = new GameObject("CameraTarget_MapaMundial");
            targetObject.transform.SetParent(hunter.transform, true);
            target = targetObject.transform;
        }
        target.position = hunterBounds.center + Vector3.up * hunterBounds.extents.y * 0.15f;

        CazadorInputReader input = hunter.GetComponent<CazadorInputReader>();
        CazadorCameraController controller = camera.GetComponent<CazadorCameraController>() ??
                                             camera.gameObject.AddComponent<CazadorCameraController>();
        SerializedObject cameraData = new SerializedObject(controller);
        cameraData.FindProperty("target").objectReferenceValue = target;
        cameraData.FindProperty("input").objectReferenceValue = input;
        float distance = Mathf.Clamp(hunterBounds.size.y * 1.05f, 8f, 24f);
        SetFloat(cameraData, "distancia", distance);
        SetFloat(cameraData, "sensibilidadRaton", 0.12f);
        SetFloat(cameraData, "radioColision", Mathf.Clamp(hunterBounds.size.y * 0.035f, 0.25f, 0.8f));
        SetFloat(cameraData, "distanciaMinima", 1.5f);
        SerializedProperty collisionLayers = cameraData.FindProperty("capasColision");
        if (collisionLayers != null)
        {
            collisionLayers.intValue = ~((1 << 8) | (1 << 10));
        }
        cameraData.ApplyModifiedPropertiesWithoutUndo();

        camera.transform.position = target.position + new Vector3(0f,
            hunterBounds.extents.y * 0.35f, -distance);
        camera.transform.LookAt(target);

        CazadorController movement = hunter.GetComponent<CazadorController>();
        if (movement != null)
        {
            SerializedObject movementData = new SerializedObject(movement);
            movementData.FindProperty("camaraTransform").objectReferenceValue = camera.transform;
            movementData.ApplyModifiedPropertiesWithoutUndo();
        }
        PlayerInput playerInput = hunter.GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            SerializedObject inputData = new SerializedObject(playerInput);
            SerializedProperty cameraProperty = inputData.FindProperty("m_Camera");
            if (cameraProperty != null)
            {
                cameraProperty.objectReferenceValue = camera;
                inputData.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static void EnsureProbe(Scene scene)
    {
        MapaMundialPlayModeProbe probe = FindInScene<MapaMundialPlayModeProbe>(scene)
            .FirstOrDefault();
        if (probe == null)
        {
            GameObject probeObject = new GameObject("MapaMundialPlayModeProbe");
            SceneManager.MoveGameObjectToScene(probeObject, scene);
            probe = probeObject.AddComponent<MapaMundialPlayModeProbe>();
        }
        probe.enabled = false;
    }

    private static void ValidateScene(Scene scene)
    {
        int hunters = FindInScene<CazadorStats>(scene).Length;
        int mutants = FindInScene<MutantStats>(scene).Length;
        int aiMutants = FindInScene<MutantEnemyIntentSource>(scene).Length;
        int robots = FindInScene<FallaCore>(scene).Length;
        int activeInputs = FindInScene<PlayerInput>(scene).Count(input => input.enabled);
        int activeCameras = FindInScene<Camera>(scene).Count(camera => camera.enabled);
        int listeners = FindInScene<AudioListener>(scene).Count(listener => listener.enabled);
        int eventSystems = FindInScene<EventSystem>(scene).Count(system => system.enabled);
        bool purple = scene.GetRootGameObjects().Any(root =>
            root.GetComponentsInChildren<Transform>(true).Any(transform =>
                transform.name.Contains("ManchaPrincipal") ||
                transform.name.StartsWith("Extension_")));

        if (hunters != 1 || mutants != 1 || aiMutants != 1 || robots < 3 ||
            activeInputs != 1 || activeCameras != 1 || listeners != 1 ||
            eventSystems > 1 || purple)
        {
            throw new InvalidOperationException(
                $"MapaMundial invalido: Cazador={hunters}, Mutant={mutants}, IA={aiMutants}, " +
                $"Robots={robots}, Inputs={activeInputs}, Camaras={activeCameras}, " +
                $"Listeners={listeners}, EventSystems={eventSystems}, Morado={purple}.");
        }
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component =>
        UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include)
            .Where(component => component.gameObject.scene == scene)
            .ToArray();

    private static GameObject InstantiatePrefab(GameObject prefab, Scene scene)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
        {
            throw new InvalidOperationException($"No se pudo instanciar {prefab.name}.");
        }
        return instance;
    }

    private static GameObject GetInstanceRoot(GameObject target)
    {
        GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(target);
        return root != null ? root : target.transform.root.gameObject;
    }

    private static Bounds CalculateBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Collider collider = target.GetComponentInChildren<Collider>(true);
            return collider != null ? collider.bounds : new Bounds(target.transform.position, Vector3.one);
        }
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }
        return bounds;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private static void SetFloat(SerializedObject data, string name, float value)
    {
        SerializedProperty property = data.FindProperty(name);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private readonly struct TransformSnapshot
    {
        public TransformSnapshot(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 Scale { get; }
    }
}
