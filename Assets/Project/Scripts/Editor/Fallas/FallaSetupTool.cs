using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class FallaSetupTool
{
    private const string ScriptsRoot = "Assets/Project/Scripts/Fallas";
    private const string ConfigRoot = "Assets/Project/ScriptableObjects/Fallas";
    private const string MaterialRoot = "Assets/Project/Art/Materials/Fallas";
    private const string PrefabRoot = "Assets/Project/Prefabs/Fallas";
    private const string BossPrefabPath =
        "Assets/Project/Prefabs/Bosses/MutantConFallas.prefab";
    private const string ValidationScenePath =
        "Assets/Project/Scenes/FallaValidation.unity";
    private const string GenerationSessionKey = "CazadoresDeFallas.GeneratedThisSession.v4";

    private static void ScheduleSafeGeneration()
    {
        if (SessionState.GetBool(GenerationSessionKey, false))
        {
            return;
        }
        SessionState.SetBool(GenerationSessionKey, true);
        EditorApplication.delayCall += GenerateMissingAssets;
    }

    [MenuItem("Tools/Cazadores de Fallas/Generar o reparar sistema de Fallas")]
    public static void GenerateMissingAssets()
    {
        RobotFallaSetupTool.GenerateOrRepair();
    }

    private static void GenerateLegacyAssets()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += GenerateMissingAssets;
            return;
        }

        try
        {
            EnsureFolder(ConfigRoot);
            EnsureFolder(MaterialRoot);
            EnsureFolder(PrefabRoot);

            Material bodyMaterial = CreateMaterialIfMissing(
                $"{MaterialRoot}/MAT_FallaBody.mat",
                new Color(0.025f, 0.008f, 0.045f, 1f),
                new Color(0.12f, 0.015f, 0.2f, 1f),
                0.82f,
                0.3f
            );
            Material coreMaterial = CreateMaterialIfMissing(
                $"{MaterialRoot}/MAT_FallaCore.mat",
                new Color(0.35f, 0.02f, 0.65f, 1f),
                new Color(2.5f, 0.08f, 4.5f, 1f),
                0.15f,
                0.72f
            );
            Material warningMaterial = CreateMaterialIfMissing(
                $"{MaterialRoot}/MAT_FallaWarning.mat",
                new Color(0.65f, 0.025f, 0.04f, 1f),
                new Color(5f, 0.08f, 0.04f, 1f),
                0.1f,
                0.65f
            );

            FallaConfiguration crawlerConfig = CreateConfigIfMissing(
                "CFG_FallaRastrera", FallaType.Rastrera, 48f, 3.6f, 12f,
                10f, 1.45f, 0.35f, 1.1f, FallaCoreVisibility.TrasDeteccion);
            FallaConfiguration explosiveConfig = CreateConfigIfMissing(
                "CFG_FallaExplosiva", FallaType.Explosiva, 32f, 2.7f, 28f,
                9f, 2.1f, 0.2f, 2f, FallaCoreVisibility.DuranteAtaque);
            FallaConfiguration generatorConfig = CreateConfigIfMissing(
                "CFG_FallaGeneradora", FallaType.Generadora, 120f, 0f, 0f,
                12f, 0.2f, 0.5f, 2f, FallaCoreVisibility.SiempreVisible);

            GameObject basePrefab = CreateFallaPrefabIfMissing(
                $"{PrefabRoot}/FallaBase.prefab", "FallaBase", crawlerConfig,
                bodyMaterial, coreMaterial, false, false, 1f);
            GameObject crawlerPrefab = CreateFallaPrefabIfMissing(
                $"{PrefabRoot}/FallaRastrera.prefab", "FallaRastrera", crawlerConfig,
                bodyMaterial, coreMaterial, false, false, 0.9f);
            GameObject explosivePrefab = CreateFallaPrefabIfMissing(
                $"{PrefabRoot}/FallaExplosiva.prefab", "FallaExplosiva", explosiveConfig,
                warningMaterial, coreMaterial, true, false, 1.05f);
            GameObject generatorPrefab = CreateFallaPrefabIfMissing(
                $"{PrefabRoot}/FallaGeneradora.prefab", "FallaGeneradora", generatorConfig,
                bodyMaterial, coreMaterial, false, true, 1.65f);

            ConfigureGeneratorPrefab(generatorPrefab, crawlerPrefab, explosivePrefab);
            GameObject mutantVariant = CreateMutantVariantIfMissing(
                crawlerPrefab, explosivePrefab, generatorPrefab);
            CreateValidationSceneIfMissing(crawlerPrefab, explosivePrefab,
                generatorPrefab, mutantVariant);
            ConfigureValidationScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateGeneratedAssets();
            Debug.Log(
                $"Sistema de Fallas listo. Scripts: {ScriptsRoot}; prefabs: {PrefabRoot}.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Cazadores de Fallas/Validar assets generados")]
    public static void ValidateGeneratedAssets()
    {
        string[] requiredAssets =
        {
            $"{PrefabRoot}/FallaBase.prefab",
            $"{PrefabRoot}/FallaRastrera.prefab",
            $"{PrefabRoot}/FallaExplosiva.prefab",
            $"{PrefabRoot}/FallaGeneradora.prefab",
            BossPrefabPath,
            ValidationScenePath
        };

        foreach (string path in requiredAssets)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
            {
                throw new InvalidOperationException($"Falta el asset requerido: {path}");
            }
        }

        foreach (string path in requiredAssets)
        {
            if (!path.EndsWith(".prefab", StringComparison.Ordinal))
            {
                continue;
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (path.Contains("Falla") && !path.Contains("Mutant") &&
                prefab.GetComponent<FallaCore>() == null)
            {
                throw new InvalidOperationException($"Prefab sin FallaCore: {path}");
            }
        }

        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"{PrefabRoot}/FallaBase.prefab");
        GameObject crawler = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"{PrefabRoot}/FallaRastrera.prefab");
        GameObject explosive = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"{PrefabRoot}/FallaExplosiva.prefab");
        GameObject generator = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"{PrefabRoot}/FallaGeneradora.prefab");
        GameObject mutant = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);

        ValidateFallaPrefab(basePrefab, typeof(FallaMeleeAttack));
        ValidateFallaPrefab(crawler, typeof(FallaMeleeAttack));
        ValidateFallaPrefab(explosive, typeof(FallaExplosiveAttack));
        ValidateFallaPrefab(generator, typeof(FallaGenerator));

        if (mutant.GetComponent<MutantFallaController>() == null ||
            mutant.GetComponent<MutantStats>() == null)
        {
            throw new InvalidOperationException(
                "MutantConFallas no conserva MutantStats o no contiene su controlador adicional.");
        }
        if (PrefabUtility.GetPrefabAssetType(mutant) != PrefabAssetType.Variant)
        {
            throw new InvalidOperationException(
                "MutantConFallas debe ser una variante del prefab Mutant original.");
        }

        ValidateSceneContents();
        Debug.Log(
            "Validacion de Fallas superada: 4 prefabs, variante Mutant y escena de prueba sin scripts faltantes.");
    }

    private static void ValidateFallaPrefab(GameObject prefab, Type requiredComponent)
    {
        if (prefab == null || prefab.GetComponent<FallaCore>() == null ||
            prefab.GetComponent(requiredComponent) == null)
        {
            throw new InvalidOperationException(
                $"Prefab de Falla incompleto: {prefab?.name ?? "null"}.");
        }
        Rigidbody body = prefab.GetComponent<Rigidbody>();
        if (body == null || !body.isKinematic || prefab.GetComponent<Collider>() == null)
        {
            throw new InvalidOperationException($"Fisica invalida en {prefab.name}.");
        }
        Hurtbox hurtbox = prefab.GetComponentInChildren<Hurtbox>(true);
        Collider hurtCollider = hurtbox != null ? hurtbox.GetComponent<Collider>() : null;
        if (hurtbox == null || hurtbox.gameObject.layer != 10 ||
            hurtCollider == null || !hurtCollider.isTrigger)
        {
            throw new InvalidOperationException($"Hurtbox invalida en {prefab.name}.");
        }
        if (CountMissingScripts(prefab) > 0)
        {
            throw new InvalidOperationException($"Scripts faltantes en {prefab.name}.");
        }
    }

    private static void ValidateSceneContents()
    {
        Scene previousActive = SceneManager.GetActiveScene();
        Scene validation = EditorSceneManager.OpenScene(
            ValidationScenePath, OpenSceneMode.Additive);
        try
        {
            int fallaCount = 0;
            int mutantControllerCount = 0;
            int cazadorCount = 0;
            foreach (GameObject root in validation.GetRootGameObjects())
            {
                fallaCount += root.GetComponentsInChildren<FallaCore>(true).Length;
                mutantControllerCount +=
                    root.GetComponentsInChildren<MutantFallaController>(true).Length;
                cazadorCount += root.GetComponentsInChildren<CazadorStats>(true).Length;
                if (CountMissingScripts(root) > 0)
                {
                    throw new InvalidOperationException(
                        $"Scripts faltantes en la escena de validacion: {root.name}.");
                }
            }
            if (fallaCount < 3 || mutantControllerCount != 1 || cazadorCount != 1)
            {
                throw new InvalidOperationException(
                    $"Escena incompleta (Fallas={fallaCount}, Mutant={mutantControllerCount}, Cazador={cazadorCount}).");
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(validation, true);
            if (previousActive.IsValid() && previousActive.isLoaded)
            {
                SceneManager.SetActiveScene(previousActive);
            }
        }
    }

    private static int CountMissingScripts(GameObject root)
    {
        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
        foreach (Transform child in root.transform)
        {
            count += CountMissingScripts(child.gameObject);
        }
        return count;
    }

    private static FallaConfiguration CreateConfigIfMissing(
        string name,
        FallaType type,
        float health,
        float speed,
        float damage,
        float detectionRange,
        float attackRange,
        float preparation,
        float cooldown,
        FallaCoreVisibility coreVisibility)
    {
        string path = $"{ConfigRoot}/{name}.asset";
        FallaConfiguration config = AssetDatabase.LoadAssetAtPath<FallaConfiguration>(path);
        if (config != null)
        {
            return config;
        }

        config = ScriptableObject.CreateInstance<FallaConfiguration>();
        SetSerialized(config, "tipo", (int)type);
        SetSerialized(config, "vidaMaxima", health);
        SetSerialized(config, "velocidad", speed);
        SetSerialized(config, "dano", damage);
        SetSerialized(config, "rangoDeteccion", detectionRange);
        SetSerialized(config, "rangoAtaque", attackRange);
        SetSerialized(config, "preparacionAtaque", preparation);
        SetSerialized(config, "cooldownAtaque", cooldown);
        SetSerialized(config, "visibilidadNucleo", (int)coreVisibility);
        AssetDatabase.CreateAsset(config, path);
        return config;
    }

    private static Material CreateMaterialIfMissing(
        string path,
        Color baseColor,
        Color emission,
        float metallic,
        float smoothness)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }
        if (shader == null)
        {
            throw new InvalidOperationException("No se encontro un shader Lit compatible.");
        }

        Material material = new Material(shader)
        {
            name = Path.GetFileNameWithoutExtension(path)
        };
        material.SetColor("_BaseColor", baseColor);
        material.SetColor("_Color", baseColor);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emission);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static GameObject CreateFallaPrefabIfMissing(
        string path,
        string objectName,
        FallaConfiguration config,
        Material bodyMaterial,
        Material coreMaterial,
        bool explosive,
        bool generator,
        float size)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing;
        }

        GameObject root = new GameObject(objectName);
        root.layer = 0;
        Rigidbody rigidbody = root.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        CapsuleCollider physicalCollider = root.AddComponent<CapsuleCollider>();
        physicalCollider.radius = 0.55f * size;
        physicalCollider.height = 0.85f * size;
        physicalCollider.center = new Vector3(0f, 0.42f * size, 0f);
        root.AddComponent<AudioSource>().spatialBlend = 1f;

        if (explosive)
        {
            root.AddComponent<FallaExplosiveAttack>();
        }
        else if (!generator)
        {
            root.AddComponent<FallaMeleeAttack>();
        }

        FallaCore core = root.AddComponent<FallaCore>();
        Transform visualRoot = CreateChild(root.transform, "VisualRoot", 0);
        visualRoot.localPosition = new Vector3(0f, 0.35f * size, 0f);
        CreateBlobVisuals(visualRoot, bodyMaterial, size);

        Transform nucleus = CreatePrimitiveChild(
            visualRoot, "Nucleo", PrimitiveType.Sphere, coreMaterial, 0);
        nucleus.localPosition = new Vector3(0f, 0.18f * size, 0.12f * size);
        nucleus.localScale = Vector3.one * 0.3f * size;

        Transform attackOrigin = CreateChild(root.transform, "AttackOrigin", 0);
        attackOrigin.localPosition = new Vector3(0f, 0.42f * size, 0.55f * size);

        Transform hurtboxObject = CreateChild(root.transform, "Hurtbox", 10);
        hurtboxObject.localPosition = new Vector3(0f, 0.42f * size, 0f);
        SphereCollider hurtCollider = hurtboxObject.gameObject.AddComponent<SphereCollider>();
        hurtCollider.radius = 0.58f * size;
        hurtCollider.isTrigger = true;
        Hurtbox hurtbox = hurtboxObject.gameObject.AddComponent<Hurtbox>();
        hurtbox.Configurar(core);

        ParticleSystem ambientParticles = CreateParticles(
            root.transform, "CorrupcionParticles", bodyMaterial.color, true, size);
        ParticleSystem deathParticles = CreateParticles(
            root.transform, "DeathParticles", coreMaterial.color, false, size);
        deathParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        FallaVisualController visualController =
            visualRoot.gameObject.AddComponent<FallaVisualController>();
        SetSerialized(visualController, "visualRoot", visualRoot);
        SetSerialized(core, "configuracion", config);
        SetSerialized(core, "visualRoot", visualRoot);
        SetSerialized(core, "nucleo", nucleus);
        SetSerialized(core, "visualController", visualController);
        SetSerialized(core, "particulasMuerte", deathParticles);

        if (explosive)
        {
            FallaExplosiveAttack attack = root.GetComponent<FallaExplosiveAttack>();
            SetSerialized(attack, "visualRoot", visualRoot);
            SetSerialized(attack, "particulasAdvertencia", ambientParticles);
            SetSerialized(attack, "particulasExplosion", deathParticles);
        }
        else if (!generator)
        {
            FallaMeleeAttack attack = root.GetComponent<FallaMeleeAttack>();
            SetSerialized(attack, "attackOrigin", attackOrigin);
            SetSerialized(attack, "particulasAtaque", ambientParticles);
        }

        if (generator)
        {
            FallaGenerator generatorComponent = root.AddComponent<FallaGenerator>();
            Transform pointA = CreateChild(root.transform, "SpawnPoint_A", 0);
            pointA.localPosition = new Vector3(1.5f, 0f, 0f);
            Transform pointB = CreateChild(root.transform, "SpawnPoint_B", 0);
            pointB.localPosition = new Vector3(-1.5f, 0f, 0f);
            SetSerializedArray(generatorComponent, "puntosAparicion", pointA, pointB);
            SetSerialized(generatorComponent, "particulasGeneracion", ambientParticles);
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void ConfigureGeneratorPrefab(
        GameObject generatorPrefab,
        GameObject crawlerPrefab,
        GameObject explosivePrefab)
    {
        if (generatorPrefab == null)
        {
            return;
        }
        string path = AssetDatabase.GetAssetPath(generatorPrefab);
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            FallaGenerator generator = contents.GetComponent<FallaGenerator>();
            SetSerializedArray(generator, "prefabsPermitidos", crawlerPrefab, explosivePrefab);
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static GameObject CreateMutantVariantIfMissing(
        GameObject crawler,
        GameObject explosive,
        GameObject generator)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
        if (existing != null)
        {
            return existing;
        }
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Prefabs/Bosses/Mutant.prefab");
        if (basePrefab == null)
        {
            throw new InvalidOperationException("No se encontro el prefab original Mutant.");
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        instance.name = "MutantConFallas";
        MutantFallaController controller = instance.AddComponent<MutantFallaController>();
        Transform pointsRoot = CreateChild(instance.transform, "FallaSpawnPoints", 0);
        Transform pointA = CreateChild(pointsRoot, "FallaSpawn_A", 0);
        pointA.localPosition = new Vector3(3f, 0f, 1.5f);
        Transform pointB = CreateChild(pointsRoot, "FallaSpawn_B", 0);
        pointB.localPosition = new Vector3(-3f, 0f, 1.5f);
        Transform pointC = CreateChild(pointsRoot, "FallaSpawn_C", 0);
        pointC.localPosition = new Vector3(0f, 0f, -3f);
        SetSerializedArray(controller, "puntosAparicion", pointA, pointB, pointC);
        ConfigureMutantPhases(controller, crawler, explosive, generator);

        GameObject variant = PrefabUtility.SaveAsPrefabAsset(instance, BossPrefabPath);
        UnityEngine.Object.DestroyImmediate(instance);
        return variant;
    }

    private static void ConfigureMutantPhases(
        MutantFallaController controller,
        GameObject crawler,
        GameObject explosive,
        GameObject generator)
    {
        SerializedObject serialized = new SerializedObject(controller);
        SerializedProperty phases = serialized.FindProperty("fases");
        phases.arraySize = 4;
        ConfigurePhase(phases.GetArrayElementAtIndex(0), 1f, 2, 9f, 1f,
            crawler);
        ConfigurePhase(phases.GetArrayElementAtIndex(1), 0.7f, 3, 7f, 1.1f,
            crawler, explosive);
        ConfigurePhase(phases.GetArrayElementAtIndex(2), 0.4f, 4, 6f, 1.2f,
            crawler, explosive, generator);
        ConfigurePhase(phases.GetArrayElementAtIndex(3), 0.18f, 5, 5f, 1.35f,
            crawler, explosive);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigurePhase(
        SerializedProperty phase,
        float threshold,
        int maxActive,
        float interval,
        float power,
        params GameObject[] prefabs)
    {
        phase.FindPropertyRelative("umbralVida").floatValue = threshold;
        phase.FindPropertyRelative("maximoActivas").intValue = maxActive;
        phase.FindPropertyRelative("intervaloInvocacion").floatValue = interval;
        phase.FindPropertyRelative("multiplicadorPoder").floatValue = power;
        SerializedProperty list = phase.FindPropertyRelative("prefabsPermitidos");
        list.arraySize = prefabs.Length;
        for (int index = 0; index < prefabs.Length; index++)
        {
            list.GetArrayElementAtIndex(index).objectReferenceValue = prefabs[index];
        }
    }

    private static void CreateValidationSceneIfMissing(
        GameObject crawler,
        GameObject explosive,
        GameObject generator,
        GameObject mutantVariant)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ValidationScenePath) != null)
        {
            return;
        }

        Scene previousActive = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.layer = 9;
            ground.transform.localScale = new Vector3(4f, 1f, 4f);

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(0f, 11f, -14f);
            camera.transform.rotation = Quaternion.Euler(32f, 0f, 0f);

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Project/Prefabs/Player/Cazador.prefab");
            GameObject player = playerPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene)
                : null;
            if (player != null)
            {
                player.transform.position = new Vector3(0f, 0.05f, -4f);
            }

            PlacePrefab(crawler, new Vector3(-4f, 0.05f, 2f), scene);
            PlacePrefab(explosive, new Vector3(4f, 0.05f, 2f), scene);
            PlacePrefab(generator, new Vector3(0f, 0.05f, 7f), scene);
            GameObject mutant = PlacePrefab(mutantVariant, new Vector3(0f, 0.05f, 12f), scene);
            if (mutant != null)
            {
                MutantControlMode controlMode = mutant.GetComponent<MutantControlMode>();
                if (controlMode != null)
                {
                    SetSerialized(controlMode, "controlHumanoActivo", false);
                }
                MutantFallaController fallaController = mutant.GetComponent<MutantFallaController>();
                if (fallaController != null && player != null)
                {
                    SetSerialized(fallaController, "objetivo", player.transform);
                }
            }

            EditorSceneManager.SaveScene(scene, ValidationScenePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previousActive.IsValid() && previousActive.isLoaded)
            {
                SceneManager.SetActiveScene(previousActive);
            }
        }
    }

    private static void ConfigureValidationScene()
    {
        Scene previousActive = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.OpenScene(
            ValidationScenePath, OpenSceneMode.Additive);
        try
        {
            CazadorStats player = null;
            MutantFallaController fallaController = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                player ??= root.GetComponentInChildren<CazadorStats>(true);
                fallaController ??=
                    root.GetComponentInChildren<MutantFallaController>(true);
            }

            if (fallaController == null || player == null)
            {
                throw new InvalidOperationException(
                    "No se pudo configurar Cazador/Mutant en FallaValidation.");
            }

            SetSerialized(fallaController, "objetivo", player.transform);
            MutantControlMode controlMode = fallaController.GetComponent<MutantControlMode>();
            if (controlMode != null)
            {
                SetSerialized(controlMode, "controlHumanoActivo", false);
            }
            PlayerInput mutantPlayerInput = fallaController.GetComponent<PlayerInput>();
            if (mutantPlayerInput != null)
            {
                mutantPlayerInput.enabled = false;
            }
            MutantInputReader mutantInput = fallaController.GetComponent<MutantInputReader>();
            if (mutantInput != null)
            {
                mutantInput.enabled = false;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previousActive.IsValid() && previousActive.isLoaded)
            {
                SceneManager.SetActiveScene(previousActive);
            }
        }
    }

    private static GameObject PlacePrefab(GameObject prefab, Vector3 position, Scene scene)
    {
        if (prefab == null)
        {
            return null;
        }
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.transform.position = position;
        return instance;
    }

    private static void CreateBlobVisuals(Transform parent, Material material, float size)
    {
        Transform main = CreatePrimitiveChild(parent, "ManchaPrincipal", PrimitiveType.Sphere,
            material, 0);
        main.localScale = new Vector3(1.35f, 0.48f, 1.1f) * size;

        Vector3[] positions =
        {
            new Vector3(0.48f, 0.02f, 0.22f),
            new Vector3(-0.42f, -0.02f, 0.18f),
            new Vector3(0.1f, 0.01f, -0.48f)
        };
        for (int index = 0; index < positions.Length; index++)
        {
            Transform secondary = CreatePrimitiveChild(parent,
                $"Extension_{index + 1}", PrimitiveType.Sphere, material, 0);
            secondary.localPosition = positions[index] * size;
            secondary.localScale = new Vector3(0.65f, 0.25f, 0.55f) * size;
            secondary.localRotation = Quaternion.Euler(0f, index * 57f, 0f);
        }
    }

    private static ParticleSystem CreateParticles(
        Transform parent,
        string name,
        Color color,
        bool loop,
        float size)
    {
        GameObject particleObject = new GameObject(name);
        particleObject.transform.SetParent(parent, false);
        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = loop;
        main.duration = loop ? 2f : 0.65f;
        main.startLifetime = loop ? 1.2f : 0.55f;
        main.startSpeed = loop ? 0.35f : 2.2f;
        main.startSize = 0.12f * size;
        main.startColor = color;
        main.maxParticles = loop ? 36 : 72;
        main.playOnAwake = loop;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = loop ? 10f : 0f;
        if (!loop)
        {
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });
        }
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.65f * size;
        return particles;
    }

    private static Transform CreatePrimitiveChild(
        Transform parent,
        string name,
        PrimitiveType type,
        Material material,
        int layer)
    {
        GameObject child = GameObject.CreatePrimitive(type);
        child.name = name;
        child.layer = layer;
        child.transform.SetParent(parent, false);
        Collider collider = child.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }
        Renderer renderer = child.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
        return child.transform;
    }

    private static Transform CreateChild(Transform parent, string name, int layer)
    {
        GameObject child = new GameObject(name);
        child.layer = layer;
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static void EnsureFolder(string path)
    {
        string[] segments = path.Split('/');
        string current = segments[0];
        for (int index = 1; index < segments.Length; index++)
        {
            string next = $"{current}/{segments[index]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[index]);
            }
            current = next;
        }
    }

    private static void SetSerialized(UnityEngine.Object target, string propertyName, object value)
    {
        if (target == null)
        {
            return;
        }
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                $"No existe {target.GetType().Name}.{propertyName}");
        }
        switch (value)
        {
            case int intValue:
                property.intValue = intValue;
                break;
            case float floatValue:
                property.floatValue = floatValue;
                break;
            case bool boolValue:
                property.boolValue = boolValue;
                break;
            case UnityEngine.Object objectValue:
                property.objectReferenceValue = objectValue;
                break;
            default:
                throw new ArgumentException($"Tipo no soportado: {value?.GetType().Name}");
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerializedArray(
        UnityEngine.Object target,
        string propertyName,
        params UnityEngine.Object[] values)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        property.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++)
        {
            property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
