using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class ConfigurarMapa1Tool
{
    private const string ScenePath = "Assets/Project/Scenes/MapaMundial.unity";
    private const string ModelPath = "Assets/Project/Models/Map1/ciudad_abandonada.glb";
    private const string MaterialPath = "Assets/Project/Art/Materials/Mapa1_PisoNocturno.mat";
    private const string EnvironmentName = "EntornoMapa1";
    private const float TargetMapSize = 250f;

    [MenuItem("Fault Hunters/Configurar Mapa 1 nocturno %#y")]
    public static void ConfigurarDesdeMenu()
    {
        ConfigurarMapa(true);
    }

    private static void ConfigurarMapa(bool regenerar)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedByTool = !scene.IsValid() || !scene.isLoaded;

        if (openedByTool)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        GameObject existingEnvironment = FindRoot(scene, EnvironmentName);

        if (existingEnvironment != null && !regenerar)
        {
            if (openedByTool)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            return;
        }

        if (existingEnvironment != null)
        {
            UnityEngine.Object.DestroyImmediate(existingEnvironment);
        }

        GameObject city = FindCity(scene);
        PrepareCity(city);
        Bounds cityBounds = CalculateBounds(city);
        Transform playableObject = FindChildRecursive(city.transform, "Object_2");
        Bounds bounds = playableObject != null
            ? CalculateBounds(playableObject.gameObject)
            : cityBounds;

        GameObject environment = new GameObject(EnvironmentName);
        SceneManager.MoveGameObjectToScene(environment, scene);

        CreateFloor(environment.transform, bounds);
        CreateLimits(environment.transform, bounds);
        ConfigureNight(scene, cityBounds);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"Mapa 1 configurado a {TargetMapSize:F0} m. Limites de Object_2: " +
            $"{bounds.size.x:F1} x {bounds.size.z:F1} metros."
        );

        if (openedByTool)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static GameObject FindCity(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "ciudad_abandonada" ||
                root.name == "Mapa1_CiudadAbandonada")
            {
                return root;
            }
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);

        if (model == null)
        {
            throw new InvalidOperationException($"No se pudo cargar el modelo '{ModelPath}'.");
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(model, scene) as GameObject;

        if (instance == null)
        {
            throw new InvalidOperationException("No se pudo crear la ciudad en la escena.");
        }

        return instance;
    }

    private static void PrepareCity(GameObject city)
    {
        city.name = "Mapa1_CiudadAbandonada";
        city.transform.position = Vector3.zero;
        city.transform.localScale = Vector3.one;

        Bounds unitBounds = CalculateBounds(city);
        float largestSide = Mathf.Max(unitBounds.size.x, unitBounds.size.z);

        if (!IsFinite(largestSide) || largestSide <= 0.001f)
        {
            throw new InvalidOperationException("La escala original de la ciudad no es valida.");
        }

        city.transform.localScale = Vector3.one * (TargetMapSize / largestSide);

        Bounds initialBounds = CalculateBounds(city);

        Vector3 offset = new Vector3(
            -initialBounds.center.x,
            -initialBounds.min.y,
            -initialBounds.center.z
        );

        if (!IsFinite(offset))
        {
            throw new InvalidOperationException("No se pudo calcular una posicion valida para la ciudad.");
        }

        city.transform.position = offset;

        MeshCollider rootCollider = city.GetComponent<MeshCollider>();

        if (rootCollider != null && rootCollider.sharedMesh == null)
        {
            UnityEngine.Object.DestroyImmediate(rootCollider);
        }

        foreach (MeshFilter meshFilter in city.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null)
            {
                continue;
            }

            MeshCollider collider = meshFilter.GetComponent<MeshCollider>();

            if (collider == null)
            {
                collider = meshFilter.gameObject.AddComponent<MeshCollider>();
            }

            collider.sharedMesh = meshFilter.sharedMesh;
            collider.convex = false;

            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(meshFilter.gameObject);
            flags |= StaticEditorFlags.BatchingStatic |
                     StaticEditorFlags.OccludeeStatic |
                     StaticEditorFlags.OccluderStatic;
            GameObjectUtility.SetStaticEditorFlags(meshFilter.gameObject, flags);
        }
    }

    private static Bounds CalculateBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            throw new InvalidOperationException("La ciudad no contiene mallas visibles.");
        }

        Bounds bounds = default;
        bool foundValidBounds = false;

        foreach (Renderer renderer in renderers)
        {
            Bounds rendererBounds = renderer.bounds;

            if (!IsFinite(rendererBounds.center) || !IsFinite(rendererBounds.size))
            {
                continue;
            }

            if (!foundValidBounds)
            {
                bounds = rendererBounds;
                foundValidBounds = true;
            }
            else
            {
                bounds.Encapsulate(rendererBounds);
            }
        }

        if (!foundValidBounds)
        {
            throw new InvalidOperationException("La ciudad no contiene limites validos.");
        }

        return bounds;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static void CreateFloor(Transform parent, Bounds bounds)
    {
        const float margin = 4f;
        const float thickness = 1f;

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "PisoMapa1";
        floor.transform.SetParent(parent);
        floor.transform.position = new Vector3(bounds.center.x, -thickness * 0.5f, bounds.center.z);
        floor.transform.localScale = new Vector3(
            Mathf.Max(20f, bounds.size.x + margin * 2f),
            thickness,
            Mathf.Max(20f, bounds.size.z + margin * 2f)
        );

        MeshRenderer renderer = floor.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = GetOrCreateGroundMaterial();
        GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.BatchingStatic);
    }

    private static void CreateLimits(Transform parent, Bounds bounds)
    {
        const float margin = 2f;
        const float thickness = 2f;
        float width = Mathf.Max(20f, bounds.size.x + margin * 2f);
        float depth = Mathf.Max(20f, bounds.size.z + margin * 2f);
        float height = Mathf.Clamp(bounds.size.y * 0.35f, 12f, 35f);
        float y = height * 0.5f;

        CreateInvisibleLimit(
            "Limite_Norte",
            parent,
            new Vector3(bounds.center.x, y, bounds.max.z + margin),
            new Vector3(width, height, thickness)
        );
        CreateInvisibleLimit(
            "Limite_Sur",
            parent,
            new Vector3(bounds.center.x, y, bounds.min.z - margin),
            new Vector3(width, height, thickness)
        );
        CreateInvisibleLimit(
            "Limite_Este",
            parent,
            new Vector3(bounds.max.x + margin, y, bounds.center.z),
            new Vector3(thickness, height, depth)
        );
        CreateInvisibleLimit(
            "Limite_Oeste",
            parent,
            new Vector3(bounds.min.x - margin, y, bounds.center.z),
            new Vector3(thickness, height, depth)
        );
    }

    private static void CreateInvisibleLimit(
        string name,
        Transform parent,
        Vector3 position,
        Vector3 scale)
    {
        GameObject limit = GameObject.CreatePrimitive(PrimitiveType.Cube);
        limit.name = name;
        limit.transform.SetParent(parent);
        limit.transform.position = position;
        limit.transform.localScale = scale;
        limit.GetComponent<MeshRenderer>().enabled = false;
        GameObjectUtility.SetStaticEditorFlags(limit, StaticEditorFlags.BatchingStatic);
    }

    private static Material GetOrCreateGroundMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (material != null)
        {
            ConfigureGroundMaterial(material);
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        material = new Material(shader != null ? shader : Shader.Find("Standard"));
        material.name = "Mapa1_PisoNocturno";
        ConfigureGroundMaterial(material);

        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    private static void ConfigureGroundMaterial(Material material)
    {
        material.color = new Color(0.11f, 0.13f, 0.17f, 1f);
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", material.color);
        }
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.16f);
        }
        EditorUtility.SetDirty(material);
    }

    private static void ConfigureNight(Scene scene, Bounds bounds)
    {
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.055f, 0.075f, 0.12f, 1f);
        RenderSettings.fogDensity = 0.0028f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.16f, 0.22f, 0.38f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.12f, 0.16f, 0.25f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.055f, 0.07f, 0.11f, 1f);
        RenderSettings.ambientIntensity = 1.15f;
        RenderSettings.reflectionIntensity = 0.55f;

        Light moon = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Light candidate = root.GetComponentInChildren<Light>(true);

            if (candidate != null && candidate.type == LightType.Directional)
            {
                moon = candidate;
                break;
            }
        }

        if (moon == null)
        {
            GameObject moonObject = new GameObject("Luz Lunar");
            SceneManager.MoveGameObjectToScene(moonObject, scene);
            moon = moonObject.AddComponent<Light>();
            moon.type = LightType.Directional;
        }

        moon.gameObject.name = "Luz Lunar";
        moon.color = new Color(0.32f, 0.48f, 0.78f, 1f);
        moon.intensity = 1.05f;
        moon.shadows = LightShadows.Soft;
        moon.shadowStrength = 0.85f;
        moon.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
        RenderSettings.sun = moon;

        GameObject fillObject = FindRoot(scene, "Luz_Relleno_Mapa1");
        if (fillObject == null)
        {
            fillObject = new GameObject("Luz_Relleno_Mapa1");
            SceneManager.MoveGameObjectToScene(fillObject, scene);
        }
        Light fill = fillObject.GetComponent<Light>();
        if (fill == null)
        {
            fill = fillObject.AddComponent<Light>();
        }
        fill.type = LightType.Directional;
        fill.color = new Color(0.55f, 0.68f, 1f, 1f);
        fill.intensity = 0.42f;
        fill.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.Euler(52f, 145f, 0f);

        Camera camera = Camera.main;

        if (camera != null && camera.gameObject.scene == scene)
        {
            float distance = Mathf.Max(bounds.size.x, bounds.size.z);
            Vector3 target = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.2f, bounds.center.z);
            camera.transform.position = target + new Vector3(-distance * 0.45f, distance * 0.38f, -distance * 0.45f);
            camera.transform.LookAt(target);
            camera.farClipPlane = Mathf.Max(500f, distance * 2.5f);
            camera.backgroundColor = RenderSettings.fogColor;
        }
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

    private static Transform FindChildRecursive(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }
        return null;
    }
}
