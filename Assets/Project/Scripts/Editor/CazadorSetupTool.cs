using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class CazadorSetupTool
{
    private const string ModelPath =
        "Assets/Project/Art/Models/Player/Ch15_nonPBR.fbx";
    private const string InputActionsPath =
        "Assets/Project/Settings/Input/PlayerController.inputactions";
    private const string PrefabPath =
        "Assets/Project/Prefabs/Player/Cazador.prefab";
    private const string AnimatorPath =
        "Assets/Project/Art/Animations/Cazador.controller";
    private const string SampleScenePath =
        "Assets/Project/Scenes/SampleScene.unity";

    private readonly struct AnimationDefinition
    {
        public AnimationDefinition(string name, string fileName, bool loop)
        {
            Name = name;
            FileName = fileName;
            Loop = loop;
        }

        public string Name { get; }
        public string FileName { get; }
        public bool Loop { get; }
        public string Path => $"Assets/Project/Art/Animations/cazador/{FileName}.fbx";
    }

    private static readonly AnimationDefinition[] AnimationDefinitions =
    {
        new AnimationDefinition("Idle", "Idle", true),
        new AnimationDefinition("Walk", "Walk", true),
        new AnimationDefinition("Run", "Run", true),
        new AnimationDefinition("Jump", "Jump", false),
        new AnimationDefinition("Fall", "Fall", true),
        new AnimationDefinition("Land", "Land", false),
        new AnimationDefinition("CrouchIdle", "CrouchIdle", true),
        new AnimationDefinition("CrouchWalk", "CrouchWalk", true),
        new AnimationDefinition("Attack", "Attack", false),
        new AnimationDefinition(
            "Death",
            "cazador@Sword And Shield Death",
            false
        )
    };

    [MenuItem("Tools/Fault Hunters/Configurar Cazador")]
    public static void ConfigurarProyecto()
    {
        try
        {
            int playerLayer = EnsureLayer("Player");
            int groundLayer = EnsureLayer("Ground");
            int damageableLayer = EnsureLayer("Damageable");

            EnsureHumanoidAvatar();
            AnimatorController animatorController = CreateAnimatorContract();
            GameObject prefab = CreatePlayerPrefab(
                playerLayer,
                groundLayer,
                damageableLayer,
                animatorController
            );
            ConfigureSampleScene(prefab, playerLayer, groundLayer);
            ReportImportedAnimationClips();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Fault Hunters: Cazador configurado con PlayerController.inputactions, prefab y SampleScene."
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    [MenuItem("Tools/Fault Hunters/Configurar Animaciones Cazador")]
    public static void ConfigurarAnimacionesCazador()
    {
        EnsureHumanoidAvatar();
        Avatar avatar = LoadMainAvatar();
        Dictionary<string, AnimationClip> clips =
            new Dictionary<string, AnimationClip>(StringComparer.Ordinal);

        foreach (AnimationDefinition definition in AnimationDefinitions)
        {
            clips.Add(
                definition.Name,
                ConfigureAnimationImporter(definition, avatar)
            );
        }

        ConfigureAnimatorController(clips);
        EnsurePrefabAnimatorConfiguration(avatar, clips["Attack"], clips["Death"]);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidarAnimacionesCazador();
        Debug.Log("Animaciones del Cazador configuradas correctamente.");
    }

    [MenuItem("Tools/Fault Hunters/Validar Animaciones Cazador")]
    public static void ValidarAnimacionesCazador()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            AnimatorPath
        );
        if (controller == null)
        {
            throw new InvalidOperationException("No se encontro Cazador.controller.");
        }

        foreach (AnimationDefinition definition in AnimationDefinitions)
        {
            AnimationClip clip = LoadAnimationClip(definition.Path, definition.Name);
            if (clip == null)
            {
                throw new InvalidOperationException(
                    $"No se encontro el clip '{definition.Name}' en {definition.Path}."
                );
            }

            if (clip.isLooping != definition.Loop)
            {
                throw new InvalidOperationException(
                    $"Loop incorrecto en '{definition.Name}': esperado={definition.Loop}."
                );
            }

            if (!clip.humanMotion)
            {
                throw new InvalidOperationException(
                    $"El clip '{definition.Name}' no esta importado como Humanoid."
                );
            }
        }

        ValidateAnimatorStructure(controller);
        ValidatePrefabAnimator(controller);
        Debug.Log(
            "Validacion de animaciones correcta: clips, loops, Humanoid, estados, " +
            "Blend Trees, Avatar, controller y Root Motion."
        );
    }

    [MenuItem("Tools/Fault Hunters/Validar Cazador")]
    public static void ValidarConfiguracion()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException($"Falta el prefab: {PrefabPath}");
        }

        RequireComponent<CharacterController>(prefab);
        PlayerInput playerInput = RequireComponent<PlayerInput>(prefab);
        RequireComponent<CazadorInputReader>(prefab);
        RequireComponent<CazadorStateController>(prefab);
        RequireComponent<CazadorController>(prefab);
        RequireComponent<CazadorAnimationController>(prefab);
        RequireComponent<CazadorCombat>(prefab);
        RequireComponent<CazadorStats>(prefab);

        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.avatar == null ||
            animator.runtimeAnimatorController == null)
        {
            throw new InvalidOperationException(
                "Cazador.prefab no contiene Animator, Avatar o Animator Controller asignado."
            );
        }

        string assignedInputPath = AssetDatabase.GetAssetPath(playerInput.actions);
        if (!string.Equals(assignedInputPath, InputActionsPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"PlayerInput usa '{assignedInputPath}' en vez de PlayerController.inputactions."
            );
        }

        ValidateAction(playerInput.actions, "Move", "<Keyboard>/w", "<Gamepad>/leftStick");
        ValidateAction(playerInput.actions, "Look", "<Pointer>/delta", "<Gamepad>/rightStick");
        ValidateAction(playerInput.actions, "Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
        ValidateAction(playerInput.actions, "Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
        ValidateAction(playerInput.actions, "Crouch", "<Keyboard>/c", "<Gamepad>/buttonEast");
        ValidateAction(playerInput.actions, "Attack", "<Mouse>/leftButton", "<Gamepad>/buttonWest");

        string[] requiredChildren = { "Visual", "CameraTarget", "GroundCheck", "AttackOrigin" };
        foreach (string childName in requiredChildren)
        {
            if (prefab.transform.Find(childName) == null)
            {
                throw new InvalidOperationException(
                    $"Cazador.prefab no contiene el hijo obligatorio '{childName}'."
                );
            }
        }

        Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        if (GameObject.Find("Cazador") == null || GameObject.Find("Ground") == null)
        {
            throw new InvalidOperationException("SampleScene no contiene Cazador y Ground.");
        }

        Camera camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
        if (camera == null || camera.GetComponent<CazadorCameraController>() == null)
        {
            throw new InvalidOperationException(
                "SampleScene no contiene una camara con CazadorCameraController."
            );
        }

        if (!scene.isLoaded)
        {
            throw new InvalidOperationException("SampleScene no pudo cargarse.");
        }

        Debug.Log(
            "Validacion Cazador correcta: prefab, PlayerController.inputactions, bindings, jerarquia y SampleScene."
        );
    }

    private static Avatar LoadMainAvatar()
    {
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
            .OfType<Avatar>()
            .FirstOrDefault();
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            throw new InvalidOperationException(
                "Ch15_nonPBR.fbx no expone un Avatar Humanoid valido."
            );
        }

        return avatar;
    }

    private static AnimationClip ConfigureAnimationImporter(
        AnimationDefinition definition,
        Avatar avatar
    )
    {
        ModelImporter importer = AssetImporter.GetAtPath(definition.Path) as ModelImporter;
        if (importer == null)
        {
            throw new InvalidOperationException(
                $"No se encontro el FBX de animacion: {definition.Path}"
            );
        }

        bool requiresOwnSourceAvatar = definition.Name == "Death";
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = requiresOwnSourceAvatar
            ? ModelImporterAvatarSetup.CreateFromThisModel
            : ModelImporterAvatarSetup.CopyFromOther;
        importer.sourceAvatar = requiresOwnSourceAvatar ? null : avatar;

        ModelImporterClipAnimation[] importedClips = importer.clipAnimations;
        if (importedClips == null || importedClips.Length == 0)
        {
            importedClips = importer.defaultClipAnimations;
        }

        if (importedClips == null || importedClips.Length == 0)
        {
            throw new InvalidOperationException(
                $"{definition.Path} no contiene AnimationClips importables."
            );
        }

        ModelImporterClipAnimation clip = importedClips[0];
        clip.name = definition.Name;
        clip.loopTime = definition.Loop;
        clip.loopPose = definition.Loop;
        clip.lockRootRotation = true;
        clip.lockRootHeightY = true;
        clip.lockRootPositionXZ = true;
        clip.keepOriginalOrientation = true;
        clip.keepOriginalPositionY = true;
        clip.keepOriginalPositionXZ = true;
        importedClips[0] = clip;
        importer.clipAnimations = importedClips;
        importer.SaveAndReimport();

        AnimationClip configuredClip = LoadAnimationClip(definition.Path, definition.Name);
        if (configuredClip == null)
        {
            throw new InvalidOperationException(
                $"Unity no pudo importar el clip '{definition.Name}'."
            );
        }

        return configuredClip;
    }

    private static AnimationClip LoadAnimationClip(string assetPath, string clipName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip =>
                !clip.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                string.Equals(clip.name, clipName, StringComparison.Ordinal));
    }

    private static void ConfigureAnimatorController(
        IReadOnlyDictionary<string, AnimationClip> clips
    )
    {
        AnimatorController controller = CreateAnimatorContract();
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        AnimatorState locomotion = GetOrCreateState(stateMachine, "Locomotion");
        AnimatorState crouch = GetOrCreateState(stateMachine, "Crouch Locomotion");
        AnimatorState jump = GetOrCreateState(stateMachine, "Jump");
        AnimatorState fall = GetOrCreateState(stateMachine, "Fall");
        AnimatorState land = GetOrCreateState(stateMachine, "Land");
        AnimatorState attack = GetOrCreateState(stateMachine, "Attack");
        AnimatorState death = GetOrCreateState(stateMachine, "Death");

        BlendTree locomotionTree = GetOrCreateBlendTree(
            controller,
            locomotion,
            "Locomotion Blend Tree"
        );
        locomotionTree.blendType = BlendTreeType.Simple1D;
        locomotionTree.blendParameter = "Velocidad";
        locomotionTree.useAutomaticThresholds = false;
        locomotionTree.children = Array.Empty<ChildMotion>();
        locomotionTree.AddChild(clips["Idle"], 0f);
        locomotionTree.AddChild(clips["Walk"], 0.6f);
        locomotionTree.AddChild(clips["Run"], 1f);

        BlendTree crouchTree = GetOrCreateBlendTree(
            controller,
            crouch,
            "Crouch Blend Tree"
        );
        crouchTree.blendType = BlendTreeType.Simple1D;
        crouchTree.blendParameter = "Velocidad";
        crouchTree.useAutomaticThresholds = false;
        crouchTree.children = Array.Empty<ChildMotion>();
        crouchTree.AddChild(clips["CrouchIdle"], 0f);
        crouchTree.AddChild(clips["CrouchWalk"], 0.3f);

        jump.motion = clips["Jump"];
        fall.motion = clips["Fall"];
        land.motion = clips["Land"];
        attack.motion = clips["Attack"];
        death.motion = clips["Death"];
        stateMachine.defaultState = locomotion;

        ClearTransitions(stateMachine);

        AnimatorStateTransition anyToDeath = stateMachine.AddAnyStateTransition(death);
        ConfigureImmediateTransition(anyToDeath, 0.08f);
        anyToDeath.canTransitionToSelf = false;
        anyToDeath.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");

        AddConditionTransition(
            locomotion,
            crouch,
            "Agachado",
            AnimatorConditionMode.If,
            0f,
            0.12f
        );
        AddConditionTransition(
            crouch,
            locomotion,
            "Agachado",
            AnimatorConditionMode.IfNot,
            0f,
            0.12f
        );

        AnimatorStateTransition jumpToFall = jump.AddTransition(fall);
        ConfigureImmediateTransition(jumpToFall, 0.08f);
        jumpToFall.AddCondition(
            AnimatorConditionMode.Less,
            0f,
            "VelocidadVertical"
        );

        AnimatorStateTransition fallToLand = fall.AddTransition(land);
        ConfigureImmediateTransition(fallToLand, 0.06f);
        fallToLand.AddCondition(AnimatorConditionMode.If, 0f, "EnSuelo");

        AddExitTimeTransition(land, locomotion, 0.9f, 0.1f);
        AddExitTimeTransition(attack, locomotion, 0.92f, 0.08f);

        AddAnyStateTrigger(stateMachine, attack, "Ataque", 0.05f);
        AddAnyStateTrigger(stateMachine, jump, "Salto", 0.05f);
        AddAnyStateTrigger(stateMachine, land, "Aterrizar", 0.05f);

        AnimatorStateTransition anyToFall = stateMachine.AddAnyStateTransition(fall);
        ConfigureImmediateTransition(anyToFall, 0.08f);
        anyToFall.canTransitionToSelf = false;
        anyToFall.AddCondition(AnimatorConditionMode.IfNot, 0f, "EnSuelo");
        anyToFall.AddCondition(
            AnimatorConditionMode.Less,
            -1f,
            "VelocidadVertical"
        );

        EditorUtility.SetDirty(locomotionTree);
        EditorUtility.SetDirty(crouchTree);
        EditorUtility.SetDirty(stateMachine);
        EditorUtility.SetDirty(controller);
    }

    private static AnimatorState GetOrCreateState(
        AnimatorStateMachine stateMachine,
        string stateName
    )
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (string.Equals(childState.state.name, stateName, StringComparison.Ordinal))
            {
                return childState.state;
            }
        }

        return stateMachine.AddState(stateName);
    }

    private static BlendTree GetOrCreateBlendTree(
        AnimatorController controller,
        AnimatorState state,
        string treeName
    )
    {
        if (state.motion is BlendTree existingTree)
        {
            existingTree.name = treeName;
            return existingTree;
        }

        BlendTree tree = new BlendTree { name = treeName };
        AssetDatabase.AddObjectToAsset(tree, controller);
        state.motion = tree;
        return tree;
    }

    private static void ClearTransitions(AnimatorStateMachine stateMachine)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            foreach (AnimatorStateTransition transition in childState.state.transitions.ToArray())
            {
                childState.state.RemoveTransition(transition);
            }
        }

        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.ToArray())
        {
            stateMachine.RemoveAnyStateTransition(transition);
        }
    }

    private static void AddConditionTransition(
        AnimatorState source,
        AnimatorState destination,
        string parameter,
        AnimatorConditionMode conditionMode,
        float threshold,
        float duration
    )
    {
        AnimatorStateTransition transition = source.AddTransition(destination);
        ConfigureImmediateTransition(transition, duration);
        transition.AddCondition(conditionMode, threshold, parameter);
    }

    private static void AddAnyStateTrigger(
        AnimatorStateMachine stateMachine,
        AnimatorState destination,
        string trigger,
        float duration
    )
    {
        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(destination);
        ConfigureImmediateTransition(transition, duration);
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    private static void ConfigureImmediateTransition(
        AnimatorStateTransition transition,
        float duration
    )
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
    }

    private static void AddExitTimeTransition(
        AnimatorState source,
        AnimatorState destination,
        float exitTime,
        float duration
    )
    {
        AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.hasFixedDuration = true;
        transition.duration = duration;
    }

    private static void EnsurePrefabAnimatorConfiguration(
        Avatar avatar,
        AnimationClip attackClip,
        AnimationClip deathClip
    )
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Animator animator = prefabRoot.GetComponentInChildren<Animator>(true);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                AnimatorPath
            );
            if (animator == null || controller == null)
            {
                throw new InvalidOperationException(
                    "No se pudo resolver Animator o Cazador.controller en el prefab."
                );
            }

            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            CazadorCombat combat = prefabRoot.GetComponent<CazadorCombat>();
            if (combat != null)
            {
                SerializedObject serializedCombat = new SerializedObject(combat);
                SerializedProperty combo = serializedCombat.FindProperty("combo");
                if (combo != null && combo.arraySize > 0)
                {
                    SerializedProperty firstAttack = combo.GetArrayElementAtIndex(0);
                    firstAttack.FindPropertyRelative("duracionAnimacion").floatValue =
                        attackClip.length;
                    serializedCombat.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            CazadorDeathController deathController =
                prefabRoot.GetComponent<CazadorDeathController>();
            if (deathController != null)
            {
                SerializedObject serializedDeath = new SerializedObject(deathController);
                serializedDeath.FindProperty("tiempoVisible").floatValue =
                    deathClip.length + 1f;
                serializedDeath.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ValidateAnimatorStructure(AnimatorController controller)
    {
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        string[] requiredStates =
        {
            "Locomotion",
            "Crouch Locomotion",
            "Jump",
            "Fall",
            "Land",
            "Attack",
            "Death"
        };

        foreach (string stateName in requiredStates)
        {
            AnimatorState state = stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(candidate => candidate.name == stateName);
            if (state == null || state.motion == null)
            {
                throw new InvalidOperationException(
                    $"El estado '{stateName}' falta o no tiene Motion."
                );
            }
        }

        AnimatorState locomotion = GetOrCreateState(stateMachine, "Locomotion");
        AnimatorState crouch = GetOrCreateState(stateMachine, "Crouch Locomotion");
        if (!(locomotion.motion is BlendTree locomotionTree) ||
            locomotionTree.children.Length != 3)
        {
            throw new InvalidOperationException(
                "Locomotion debe contener Idle, Walk y Run."
            );
        }

        if (!(crouch.motion is BlendTree crouchTree) || crouchTree.children.Length != 2)
        {
            throw new InvalidOperationException(
                "Crouch Locomotion debe contener CrouchIdle y CrouchWalk."
            );
        }
    }

    private static void ValidatePrefabAnimator(AnimatorController controller)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Animator animator = prefab != null
            ? prefab.GetComponentInChildren<Animator>(true)
            : null;
        if (animator == null || animator.avatar == null || !animator.avatar.isValid ||
            animator.runtimeAnimatorController != controller || animator.applyRootMotion)
        {
            throw new InvalidOperationException(
                "El prefab no conserva Animator, Avatar, controller o Root Motion correcto."
            );
        }
    }

    private static AnimatorController CreateAnimatorContract()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            AnimatorPath
        );
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);
        }

        EnsureParameter(controller, "Velocidad", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "MovimientoX", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "MovimientoY", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "EnSuelo", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "Agachado", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "Corriendo", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "VelocidadVertical", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "Dasheando", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "Salto", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Aterrizar", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Ataque", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Death", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "IsDead", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        if (!stateMachine.states.Any(state => state.state.name == "Locomotion"))
        {
            AnimatorState locomotion = stateMachine.AddState("Locomotion");
            stateMachine.defaultState = locomotion;
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void EnsureHumanoidAvatar()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"No se pudo abrir ModelImporter para {ModelPath}.");
        }

        if (importer.animationType != ModelImporterAnimationType.Human ||
            importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
        }

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
            .OfType<Avatar>()
            .FirstOrDefault();
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            throw new InvalidOperationException(
                "Unity no pudo generar un Avatar Humanoid valido para Ch15_nonPBR.fbx."
            );
        }
    }

    private static void EnsureParameter(
        AnimatorController controller,
        string name,
        AnimatorControllerParameterType type
    )
    {
        if (controller.parameters.Any(parameter => parameter.name == name))
        {
            return;
        }

        controller.AddParameter(name, type);
    }

    private static T RequireComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            throw new InvalidOperationException(
                $"{target.name} no contiene el componente obligatorio {typeof(T).Name}."
            );
        }

        return component;
    }

    private static void ValidateAction(
        InputActionAsset asset,
        string actionName,
        params string[] requiredPaths
    )
    {
        InputAction action = asset.FindAction($"Player/{actionName}", false);
        if (action == null)
        {
            throw new InvalidOperationException($"Falta Player/{actionName}.");
        }

        foreach (string path in requiredPaths)
        {
            if (!action.bindings.Any(binding =>
                    string.Equals(binding.path, path, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Player/{actionName} no contiene el binding obligatorio '{path}'."
                );
            }
        }
    }

    private static GameObject CreatePlayerPrefab(
        int playerLayer,
        int groundLayer,
        int damageableLayer,
        RuntimeAnimatorController animatorController
    )
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            InputActionsPath
        );
        if (model == null)
        {
            throw new InvalidOperationException($"No se encontro el modelo: {ModelPath}");
        }

        if (inputActions == null)
        {
            throw new InvalidOperationException(
                $"No se encontro PlayerController.inputactions: {InputActionsPath}"
            );
        }

        GameObject root = new GameObject("Cazador");
        root.layer = playerLayer;

        GameObject visual = new GameObject("Visual");
        visual.layer = playerLayer;
        visual.transform.SetParent(root.transform, false);

        GameObject modelInstance = PrefabUtility.InstantiatePrefab(model) as GameObject;
        if (modelInstance == null)
        {
            UnityEngine.Object.DestroyImmediate(root);
            throw new InvalidOperationException("Unity no pudo instanciar Ch15_nonPBR.fbx.");
        }

        modelInstance.name = model.name;
        modelInstance.transform.SetParent(visual.transform, false);
        SetLayerRecursively(modelInstance, playerLayer);

        Bounds bounds = CalculateBounds(modelInstance);
        modelInstance.transform.localPosition -= Vector3.up * bounds.min.y;
        bounds = CalculateBounds(modelInstance);

        float height = Mathf.Max(1f, bounds.size.y);
        float width = Mathf.Max(bounds.size.x, bounds.size.z);
        float radius = Mathf.Clamp(width * 0.32f, 0.2f, height * 0.45f);

        CharacterController characterController = root.AddComponent<CharacterController>();
        characterController.height = height;
        characterController.radius = radius;
        characterController.center = new Vector3(0f, height * 0.5f, 0f);
        characterController.skinWidth = Mathf.Max(0.02f, radius * 0.1f);
        characterController.stepOffset = Mathf.Min(height * 0.2f, 0.4f);
        characterController.slopeLimit = 50f;
        characterController.minMoveDistance = 0f;

        PlayerInput playerInput = root.AddComponent<PlayerInput>();
        playerInput.actions = inputActions;
        playerInput.defaultActionMap = "Player";
        playerInput.neverAutoSwitchControlSchemes = false;
        playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

        CazadorStats stats = root.AddComponent<CazadorStats>();
        CazadorStateController state = root.AddComponent<CazadorStateController>();
        CazadorInputReader input = root.AddComponent<CazadorInputReader>();
        CazadorAnimationController animation = root.AddComponent<CazadorAnimationController>();
        CazadorController controller = root.AddComponent<CazadorController>();
        CazadorCombat combat = root.AddComponent<CazadorCombat>();

        Transform cameraTarget = CreateChild(root.transform, "CameraTarget");
        cameraTarget.localPosition = Vector3.up * height * 0.82f;
        Transform groundCheck = CreateChild(root.transform, "GroundCheck");
        groundCheck.localPosition = Vector3.up * (radius * 0.35f);
        Transform attackOrigin = CreateChild(root.transform, "AttackOrigin");
        attackOrigin.localPosition = new Vector3(0f, height * 0.55f, radius * 0.6f);
        HitboxAtaque hitbox = attackOrigin.gameObject.AddComponent<HitboxAtaque>();

        Animator animator = modelInstance.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            animator = visual.AddComponent<Animator>();
        }

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
            .OfType<Avatar>()
            .FirstOrDefault();
        animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.runtimeAnimatorController = animatorController;

        SetReference(animation, "animator", animator);
        SetReference(animation, "controller", controller);
        SetReference(animation, "state", state);
        SetReference(controller, "groundCheck", groundCheck);
        SetReference(controller, "input", input);
        SetReference(controller, "state", state);
        SetReference(controller, "stats", stats);
        SetReference(controller, "animationController", animation);
        SetLayerMask(controller, "capasSuelo", 1 << groundLayer);
        SetLayerMask(controller, "capasObstruccion", ~(1 << playerLayer));
        SetReference(combat, "input", input);
        SetReference(combat, "state", state);
        SetReference(combat, "stats", stats);
        SetReference(combat, "animationController", animation);
        SetReference(combat, "hitboxAtaque", hitbox);
        SetReference(hitbox, "origenAtaque", attackOrigin);
        SetReference(hitbox, "raizPropietario", root.transform);
        SetLayerMask(hitbox, "capasGolpeables", 1 << damageableLayer);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    private static void ConfigureSampleScene(
        GameObject prefab,
        int playerLayer,
        int groundLayer
    )
    {
        Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

        GameObject oldPlayer = GameObject.Find("Cazador");
        if (oldPlayer != null)
        {
            UnityEngine.Object.DestroyImmediate(oldPlayer);
        }

        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(5f, 1f, 5f);
        }

        ground.layer = groundLayer;
        ground.transform.position = Vector3.zero;

        GameObject player = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (player == null)
        {
            throw new InvalidOperationException("No se pudo instanciar Cazador.prefab.");
        }

        player.transform.position = new Vector3(0f, 0.05f, 0f);
        player.layer = playerLayer;

        Camera mainCamera = UnityEngine.Object.FindAnyObjectByType<Camera>();
        if (mainCamera == null)
        {
            throw new InvalidOperationException("SampleScene no contiene una camara.");
        }

        mainCamera.tag = "MainCamera";
        CazadorCameraController cameraController =
            mainCamera.GetComponent<CazadorCameraController>() ??
            mainCamera.gameObject.AddComponent<CazadorCameraController>();
        CazadorInputReader input = player.GetComponent<CazadorInputReader>();
        Transform target = player.transform.Find("CameraTarget");
        SetReference(cameraController, "target", target);
        SetReference(cameraController, "input", input);
        SetLayerMask(cameraController, "capasColision", ~(1 << playerLayer));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0)
        {
            return existing;
        }

        UnityEngine.Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath(
            "ProjectSettings/TagManager.asset"
        );
        if (tagManagerAssets.Length == 0)
        {
            throw new InvalidOperationException("No se pudo abrir TagManager.asset.");
        }

        SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int index = 8; index < layers.arraySize; index++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            if (!string.IsNullOrEmpty(layer.stringValue))
            {
                continue;
            }

            layer.stringValue = layerName;
            tagManager.ApplyModifiedProperties();
            return index;
        }

        throw new InvalidOperationException($"No hay una capa libre para '{layerName}'.");
    }

    private static Transform CreateChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.layer = parent.gameObject.layer;
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static Bounds CalculateBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(target.transform.position + Vector3.up, new Vector3(1f, 2f, 1f));
        }

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        return bounds;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static void SetReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            throw new MissingFieldException(target.GetType().Name, propertyName);
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLayerMask(
        UnityEngine.Object target,
        string propertyName,
        int value
    )
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            throw new MissingFieldException(target.GetType().Name, propertyName);
        }

        property.intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ReportImportedAnimationClips()
    {
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
            .OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
            .ToArray();

        if (clips.Length == 0)
        {
            Debug.LogWarning("Ch15_nonPBR.fbx no expone AnimationClips utilizables.");
            return;
        }

        foreach (AnimationClip clip in clips)
        {
            Debug.Log(
                $"Clip FBX detectado: '{clip.name}', duracion={clip.length:0.###}s, " +
                $"loop={clip.isLooping}, humano={clip.humanMotion}. No se asigno semanticamente."
            );
        }
    }
}
