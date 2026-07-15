using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class RobotFallaSetupTool
{
    private const string RobotSourcePath =
        "Assets/RobotSphere/Assets/Prefab/robotSphere.prefab";
    private const string RobotMaterialRoot =
        "Assets/Project/Art/Materials/Fallas/Robots";
    private const string PrefabRoot = "Assets/Project/Prefabs/Fallas";
    private const string RobotPrefabRoot = "Assets/Project/Prefabs/Fallas/Robots";
    private const string ConfigRoot = "Assets/Project/ScriptableObjects/Fallas";
    private const string MutantOriginalPath =
        "Assets/Project/Prefabs/Bosses/Mutant.prefab";
    private const string MutantRobotPath =
        "Assets/Project/Prefabs/Bosses/MutantConRobots.prefab";
    private const string ValidationScenePath =
        "Assets/Project/Scenes/FallaValidation.unity";
    private const string SessionKey = "CazadoresDeFallas.RobotMigration.v3";

    [InitializeOnLoadMethod]
    private static void ScheduleMigration()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{RobotPrefabRoot}/RobotFallaRastrera.prefab") != null &&
            AssetDatabase.LoadAssetAtPath<GameObject>(MutantRobotPath) != null)
        {
            return;
        }
        if (SessionState.GetBool(SessionKey, false))
        {
            return;
        }
        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += GenerateOrRepair;
    }

    [MenuItem("Tools/Cazadores de Fallas/Migrar Fallas a RobotSphere")]
    public static void GenerateOrRepair()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += GenerateOrRepair;
            return;
        }

        try
        {
            GameObject robotSource = AssetDatabase.LoadAssetAtPath<GameObject>(RobotSourcePath);
            if (robotSource == null)
            {
                throw new InvalidOperationException(
                    $"No se encontro el prefab RobotSphere: {RobotSourcePath}");
            }

            EnsureFolder(RobotMaterialRoot);
            EnsureFolder(RobotPrefabRoot);
            Material body = CreateRobotMaterial(
                $"{RobotMaterialRoot}/MAT_RobotFallaBody.mat",
                new Color(0.09f, 0.11f, 0.13f, 1f),
                new Color(0.015f, 0.02f, 0.025f, 1f),
                FindSourceTexture("body_diff")
            );
            Material energy = CreateRobotMaterial(
                $"{RobotMaterialRoot}/MAT_RobotFallaEnergy.mat",
                new Color(0.7f, 0.055f, 0.01f, 1f),
                new Color(5f, 0.18f, 0.015f, 1f),
                null
            );
            Material warning = CreateRobotMaterial(
                $"{RobotMaterialRoot}/MAT_RobotFallaWarning.mat",
                new Color(0.22f, 0.015f, 0.005f, 1f),
                new Color(7f, 0.08f, 0.01f, 1f),
                null
            );

            FallaConfiguration crawlerConfig = LoadConfig("CFG_FallaRastrera");
            FallaConfiguration explosiveConfig = LoadConfig("CFG_FallaExplosiva");
            FallaConfiguration generatorConfig = LoadConfig("CFG_FallaGeneradora");

            GameObject robotCrawler = BuildRobotPrefab(
                $"{RobotPrefabRoot}/RobotFallaRastrera.prefab",
                "RobotFallaRastrera", robotSource, crawlerConfig, body, energy,
                RobotFallaMovementAnimation.Roll, false, false, 0.9f);
            GameObject robotExplosive = BuildRobotPrefab(
                $"{RobotPrefabRoot}/RobotFallaExplosiva.prefab",
                "RobotFallaExplosiva", robotSource, explosiveConfig, body, warning,
                RobotFallaMovementAnimation.Roll, true, false, 1f);
            GameObject robotGenerator = BuildRobotPrefab(
                $"{RobotPrefabRoot}/RobotFallaGeneradora.prefab",
                "RobotFallaGeneradora", robotSource, generatorConfig, body, energy,
                RobotFallaMovementAnimation.Walk, false, true, 1.45f);

            // Mantiene las rutas historicas para no romper referencias externas.
            BuildRobotPrefab($"{PrefabRoot}/FallaBase.prefab", "FallaBase", robotSource,
                crawlerConfig, body, energy, RobotFallaMovementAnimation.Roll,
                false, false, 0.9f);
            BuildRobotPrefab($"{PrefabRoot}/FallaRastrera.prefab", "FallaRastrera", robotSource,
                crawlerConfig, body, energy, RobotFallaMovementAnimation.Roll,
                false, false, 0.9f);
            BuildRobotPrefab($"{PrefabRoot}/FallaExplosiva.prefab", "FallaExplosiva", robotSource,
                explosiveConfig, body, warning, RobotFallaMovementAnimation.Roll,
                true, false, 1f);
            GameObject legacyGenerator = BuildRobotPrefab(
                $"{PrefabRoot}/FallaGeneradora.prefab", "FallaGeneradora", robotSource,
                generatorConfig, body, energy, RobotFallaMovementAnimation.Walk,
                false, true, 1.45f);

            ConfigureGenerator(robotGenerator, robotCrawler, robotExplosive);
            ConfigureGenerator(legacyGenerator, robotCrawler, robotExplosive);
            GameObject mutant = BuildMutantRobotVariant(
                robotCrawler, robotExplosive, robotGenerator);
            MigrateValidationScene(robotCrawler, robotExplosive, robotGenerator, mutant);
            RemoveDeprecatedPurpleMaterials();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateMigration();
            Debug.Log(
                "Migracion RobotSphere completada: robots, Mutant CPU y FallaValidation actualizados.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static GameObject BuildRobotPrefab(
        string path,
        string objectName,
        GameObject robotSource,
        FallaConfiguration config,
        Material bodyMaterial,
        Material energyMaterial,
        RobotFallaMovementAnimation movementAnimation,
        bool explosive,
        bool generator,
        float size)
    {
        GameObject root = new GameObject(objectName);
        root.layer = 0;
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        CapsuleCollider physicalCollider = root.AddComponent<CapsuleCollider>();
        physicalCollider.radius = 0.62f * size;
        physicalCollider.height = 1.25f * size;
        physicalCollider.center = new Vector3(0f, 0.62f * size, 0f);
        AudioSource audioSource = root.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;

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
        visualRoot.localPosition = new Vector3(0f, 0.62f * size, 0f);
        visualRoot.localScale = Vector3.one * size;
        GameObject robot = (GameObject)PrefabUtility.InstantiatePrefab(robotSource);
        robot.name = "RobotSphereVisual";
        robot.transform.SetParent(visualRoot, false);
        robot.transform.localPosition = Vector3.zero;
        robot.transform.localRotation = Quaternion.identity;
        RobotFreeAnim legacyInput = robot.GetComponent<RobotFreeAnim>();
        if (legacyInput != null)
        {
            legacyInput.enabled = false;
        }
        Animator animator = robot.GetComponent<Animator>();
        if (animator == null)
        {
            throw new InvalidOperationException("El prefab RobotSphere no contiene Animator.");
        }
        animator.applyRootMotion = false;
        ReplaceRobotMaterials(robot, bodyMaterial, energyMaterial);

        Transform attackOrigin = CreateChild(root.transform, "AttackOrigin", 0);
        attackOrigin.localPosition = new Vector3(0f, 0.62f * size, 0.72f * size);
        Transform hurtboxObject = CreateChild(root.transform, "Hurtbox", 10);
        hurtboxObject.localPosition = new Vector3(0f, 0.62f * size, 0f);
        SphereCollider hurtCollider = hurtboxObject.gameObject.AddComponent<SphereCollider>();
        hurtCollider.radius = 0.68f * size;
        hurtCollider.isTrigger = true;
        Hurtbox hurtbox = hurtboxObject.gameObject.AddComponent<Hurtbox>();
        hurtbox.Configurar(core);

        ParticleSystem ambient = CreateTechParticles(
            root.transform, "EnergyParticles", energyMaterial.color, true, size);
        ParticleSystem death = CreateTechParticles(
            root.transform, "DestructionParticles", energyMaterial.color, false, size);
        death.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        FallaVisualController visual = visualRoot.gameObject.AddComponent<FallaVisualController>();
        RobotFallaAnimationAdapter adapter =
            visualRoot.gameObject.AddComponent<RobotFallaAnimationAdapter>();
        SetSerialized(visual, "visualRoot", visualRoot);
        SetSerialized(visual, "animarFormaProcedural", false);
        SetSerialized(visual, "colorAlerta", new Color(1f, 0.24f, 0.02f, 1f));
        SetSerialized(visual, "colorImpacto", new Color(1f, 0.04f, 0.01f, 1f));
        SetSerialized(adapter, "animator", animator);
        SetSerialized(adapter, "core", core);
        SetSerialized(adapter, "reactionRoot", visualRoot);
        SetSerialized(adapter, "movimiento", (int)movementAnimation);

        SetSerialized(core, "configuracion", config);
        SetSerialized(core, "visualRoot", visualRoot);
        SetSerialized(core, "nucleo", null);
        SetSerialized(core, "visualController", visual);
        SetSerialized(core, "particulasMuerte", death);
        SetSerialized(core, "audioSource", audioSource);

        if (explosive)
        {
            FallaExplosiveAttack attack = root.GetComponent<FallaExplosiveAttack>();
            SetSerialized(attack, "visualRoot", visualRoot);
            SetSerialized(attack, "particulasAdvertencia", ambient);
            SetSerialized(attack, "particulasExplosion", death);
            SetSerialized(attack, "audioSource", audioSource);
        }
        else if (!generator)
        {
            FallaMeleeAttack attack = root.GetComponent<FallaMeleeAttack>();
            SetSerialized(attack, "attackOrigin", attackOrigin);
            SetSerialized(attack, "particulasAtaque", ambient);
            SetSerialized(attack, "audioSource", audioSource);
        }

        if (generator)
        {
            FallaGenerator generatorComponent = root.AddComponent<FallaGenerator>();
            Transform pointA = CreateChild(root.transform, "SpawnPoint_A", 0);
            pointA.localPosition = new Vector3(1.8f, 0f, 0f);
            Transform pointB = CreateChild(root.transform, "SpawnPoint_B", 0);
            pointB.localPosition = new Vector3(-1.8f, 0f, 0f);
            SetSerializedArray(generatorComponent, "puntosAparicion", pointA, pointB);
            SetSerialized(generatorComponent, "particulasGeneracion", ambient);
            SetSerialized(adapter, "generator", generatorComponent);
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void ConfigureGenerator(
        GameObject generatorPrefab,
        GameObject crawler,
        GameObject explosive)
    {
        string path = AssetDatabase.GetAssetPath(generatorPrefab);
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            SetSerializedArray(contents.GetComponent<FallaGenerator>(),
                "prefabsPermitidos", crawler, explosive);
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static GameObject BuildMutantRobotVariant(
        GameObject crawler,
        GameObject explosive,
        GameObject generator)
    {
        GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>(MutantOriginalPath);
        if (original == null)
        {
            throw new InvalidOperationException("No se encontro el prefab Mutant original.");
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(original);
        instance.name = "MutantConRobots";
        MutantEnemyIntentSource ai = instance.AddComponent<MutantEnemyIntentSource>();
        MutantFallaController robotController = instance.AddComponent<MutantFallaController>();

        MutantMotor motor = instance.GetComponent<MutantMotor>();
        MutantCombat combat = instance.GetComponent<MutantCombat>();
        motor.SetIntentSource(ai);
        motor.SetCameraTransform(null);
        combat.SetIntentSource(ai);

        PlayerInput playerInput = instance.GetComponent<PlayerInput>();
        MutantInputReader inputReader = instance.GetComponent<MutantInputReader>();
        MutantControlMode controlMode = instance.GetComponent<MutantControlMode>();
        if (playerInput != null) playerInput.enabled = false;
        if (inputReader != null) inputReader.enabled = false;
        if (controlMode != null)
        {
            SetSerialized(controlMode, "controlHumanoActivo", false);
        }
        foreach (MutantCameraController cameraController in
                 instance.GetComponentsInChildren<MutantCameraController>(true))
        {
            cameraController.enabled = false;
        }
        foreach (Camera camera in instance.GetComponentsInChildren<Camera>(true))
        {
            camera.enabled = false;
        }
        foreach (AudioListener listener in instance.GetComponentsInChildren<AudioListener>(true))
        {
            listener.enabled = false;
        }

        Transform pointsRoot = CreateChild(instance.transform, "RobotSpawnPoints", 0);
        Transform pointA = CreateChild(pointsRoot, "RobotSpawn_A", 0);
        pointA.localPosition = new Vector3(3f, 0f, 1.5f);
        Transform pointB = CreateChild(pointsRoot, "RobotSpawn_B", 0);
        pointB.localPosition = new Vector3(-3f, 0f, 1.5f);
        Transform pointC = CreateChild(pointsRoot, "RobotSpawn_C", 0);
        pointC.localPosition = new Vector3(0f, 0f, -3f);
        SetSerializedArray(robotController, "puntosAparicion", pointA, pointB, pointC);
        ConfigureMutantPhases(robotController, crawler, explosive, generator);

        MutantDeathController death = instance.GetComponent<MutantDeathController>();
        if (death != null)
        {
            SetSerializedArray(death, "controladoresCpu", ai, robotController);
        }

        GameObject variant = PrefabUtility.SaveAsPrefabAsset(instance, MutantRobotPath);
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
        ConfigurePhase(phases.GetArrayElementAtIndex(0), 1f, 2, 9f, 1f, crawler);
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

    private static void MigrateValidationScene(
        GameObject crawler,
        GameObject explosive,
        GameObject generator,
        GameObject mutantPrefab)
    {
        Scene previousActive = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.OpenScene(ValidationScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            CazadorStats player = null;
            List<GameObject> obsoleteEnemies = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                player ??= root.GetComponentInChildren<CazadorStats>(true);
                if (root.GetComponentInChildren<FallaCore>(true) != null ||
                    root.GetComponentInChildren<MutantFallaController>(true) != null ||
                    root.GetComponentInChildren<MutantEnemyIntentSource>(true) != null)
                {
                    obsoleteEnemies.Add(root);
                }
            }
            foreach (GameObject obsolete in obsoleteEnemies)
            {
                UnityEngine.Object.DestroyImmediate(obsolete);
            }
            if (player == null)
            {
                throw new InvalidOperationException("FallaValidation no contiene Cazador.");
            }

            RobotFallaPlayModeProbe existingProbe =
                UnityEngine.Object.FindAnyObjectByType<RobotFallaPlayModeProbe>();
            if (existingProbe == null || existingProbe.gameObject.scene != scene)
            {
                GameObject probeObject = new GameObject("RobotFallaPlayModeProbe");
                existingProbe = probeObject.AddComponent<RobotFallaPlayModeProbe>();
            }
            existingProbe.enabled = false;

            PlacePrefab(crawler, new Vector3(-4f, 0.05f, 2f), scene);
            PlacePrefab(explosive, new Vector3(4f, 0.05f, 2f), scene);
            PlacePrefab(generator, new Vector3(0f, 0.05f, 7f), scene);
            GameObject mutant = PlacePrefab(mutantPrefab, new Vector3(0f, 0.05f, 12f), scene);
            MutantEnemyIntentSource ai = mutant.GetComponent<MutantEnemyIntentSource>();
            MutantFallaController robotController = mutant.GetComponent<MutantFallaController>();
            ai.SetTarget(player);
            SetSerialized(ai, "objetivo", player);
            SetSerialized(robotController, "objetivo", player.transform);

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

    private static void ValidateMigration()
    {
        string[] robotPaths =
        {
            $"{RobotPrefabRoot}/RobotFallaRastrera.prefab",
            $"{RobotPrefabRoot}/RobotFallaExplosiva.prefab",
            $"{RobotPrefabRoot}/RobotFallaGeneradora.prefab"
        };
        foreach (string path in robotPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<FallaCore>() == null ||
                prefab.GetComponentInChildren<RobotFallaAnimationAdapter>(true) == null ||
                prefab.GetComponentInChildren<Animator>(true) == null ||
                FindChildRecursive(prefab.transform, "ManchaPrincipal") != null ||
                FindChildRecursive(prefab.transform, "Nucleo") != null ||
                CountMissingScripts(prefab) > 0)
            {
                throw new InvalidOperationException($"Prefab RobotFalla invalido: {path}");
            }
            RobotFreeAnim legacy = prefab.GetComponentInChildren<RobotFreeAnim>(true);
            if (legacy != null && legacy.enabled)
            {
                throw new InvalidOperationException($"Input legado activo en {path}");
            }
            if (prefab.GetComponentInChildren<Animator>(true).applyRootMotion)
            {
                throw new InvalidOperationException($"Root Motion activo en {path}");
            }
        }

        GameObject mutant = AssetDatabase.LoadAssetAtPath<GameObject>(MutantRobotPath);
        if (mutant == null || mutant.GetComponent<MutantEnemyIntentSource>() == null ||
            mutant.GetComponent<MutantFallaController>() == null ||
            mutant.GetComponent<PlayerInput>().enabled ||
            PrefabUtility.GetPrefabAssetType(mutant) != PrefabAssetType.Variant)
        {
            throw new InvalidOperationException("MutantConRobots no es una variante CPU valida.");
        }

        Scene previousActive = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.OpenScene(ValidationScenePath, OpenSceneMode.Additive);
        try
        {
            int robots = 0;
            int aiMutants = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                robots += root.GetComponentsInChildren<RobotFallaAnimationAdapter>(true).Length;
                aiMutants += root.GetComponentsInChildren<MutantEnemyIntentSource>(true).Length;
                if (CountMissingScripts(root) > 0)
                {
                    throw new InvalidOperationException($"Script faltante en escena: {root.name}");
                }
            }
            if (robots != 3 || aiMutants != 1)
            {
                throw new InvalidOperationException(
                    $"FallaValidation incompleta: robots={robots}, MutantCPU={aiMutants}");
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previousActive.IsValid() && previousActive.isLoaded)
            {
                SceneManager.SetActiveScene(previousActive);
            }
        }
        Debug.Log(
            "Validacion RobotSphere superada: 3 robots, Mutant CPU, sin manchas ni scripts faltantes.");
    }

    private static Material CreateRobotMaterial(
        string path,
        Color baseColor,
        Color emission,
        Texture texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            throw new InvalidOperationException("No se encontro URP/Lit.");
        }
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.name = System.IO.Path.GetFileNameWithoutExtension(path);
        material.SetColor("_BaseColor", baseColor);
        material.SetColor("_EmissionColor", emission);
        material.SetFloat("_Metallic", 0.82f);
        material.SetFloat("_Smoothness", 0.62f);
        material.EnableKeyword("_EMISSION");
        if (texture != null)
        {
            material.SetTexture("_BaseMap", texture);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture FindSourceTexture(string materialName)
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(
            $"Assets/RobotSphere/Assets/Models/Materials/{materialName}.mat");
        return source != null ? source.mainTexture : null;
    }

    private static void ReplaceRobotMaterials(
        GameObject robot,
        Material body,
        Material energy)
    {
        foreach (Renderer renderer in robot.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                string materialName = materials[index] != null
                    ? materials[index].name.ToLowerInvariant()
                    : string.Empty;
                materials[index] = materialName.Contains("eye") || materialName.Contains("visor")
                    ? energy
                    : body;
            }
            renderer.sharedMaterials = materials;
        }
    }

    private static ParticleSystem CreateTechParticles(
        Transform parent,
        string name,
        Color color,
        bool loop,
        float size)
    {
        GameObject particleObject = new GameObject(name);
        particleObject.transform.SetParent(parent, false);
        particleObject.transform.localPosition = new Vector3(0f, 0.65f * size, 0f);
        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = loop;
        main.duration = loop ? 2f : 0.6f;
        main.startLifetime = loop ? 0.65f : 0.45f;
        main.startSpeed = loop ? 0.15f : 3.2f;
        main.startSize = loop ? 0.045f * size : 0.1f * size;
        main.startColor = color;
        main.maxParticles = loop ? 20 : 54;
        main.playOnAwake = loop;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = loop ? 5f : 0f;
        if (!loop)
        {
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 32) });
        }
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.55f * size;
        return particles;
    }

    private static void RemoveDeprecatedPurpleMaterials()
    {
        string[] deprecated =
        {
            "Assets/Project/Art/Materials/Fallas/MAT_FallaBody.mat",
            "Assets/Project/Art/Materials/Fallas/MAT_FallaCore.mat",
            "Assets/Project/Art/Materials/Fallas/MAT_FallaWarning.mat"
        };
        foreach (string path in deprecated)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }

    private static FallaConfiguration LoadConfig(string name)
    {
        FallaConfiguration config = AssetDatabase.LoadAssetAtPath<FallaConfiguration>(
            $"{ConfigRoot}/{name}.asset");
        if (config == null)
        {
            throw new InvalidOperationException($"No se encontro {name}.");
        }
        return config;
    }

    private static GameObject PlacePrefab(GameObject prefab, Vector3 position, Scene scene)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.transform.position = position;
        return instance;
    }

    private static Transform CreateChild(Transform parent, string name, int layer)
    {
        GameObject child = new GameObject(name) { layer = layer };
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
            throw new InvalidOperationException($"No existe {target.GetType().Name}.{propertyName}");
        }
        switch (value)
        {
            case null:
                property.objectReferenceValue = null;
                break;
            case int intValue:
                property.intValue = intValue;
                break;
            case bool boolValue:
                property.boolValue = boolValue;
                break;
            case float floatValue:
                property.floatValue = floatValue;
                break;
            case Color colorValue:
                property.colorValue = colorValue;
                break;
            case UnityEngine.Object objectValue:
                property.objectReferenceValue = objectValue;
                break;
            default:
                throw new ArgumentException($"Tipo no soportado: {value.GetType().Name}");
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

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root.name == childName)
        {
            return root;
        }
        foreach (Transform child in root)
        {
            Transform found = FindChildRecursive(child, childName);
            if (found != null) return found;
        }
        return null;
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
}
