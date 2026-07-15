#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class MutantSetupTool
{
    private const string ModelPath = "Assets/Project/Art/Models/Player/Mutant.fbx";
    private const string ControllerPath = "Assets/Project/Art/Animations/Mutant.controller";
    private const string PrefabPath = "Assets/Project/Prefabs/Bosses/Mutant.prefab";
    private const string InputPath = "Assets/Project/Settings/Input/Player2Controller.inputactions";
    private const string TestScenePath = "Assets/Project/Scenes/SampleScene.unity";
    private const float MutantScale = 3.4f;

    private readonly struct MutantClipDefinition
    {
        public MutantClipDefinition(string stateName, string fileName, bool loop, bool used)
        {
            StateName = stateName;
            FileName = fileName;
            Loop = loop;
            Used = used;
        }

        public string StateName { get; }
        public string FileName { get; }
        public bool Loop { get; }
        public bool Used { get; }
        public string Path => $"Assets/Project/Art/Animations/mutant/{FileName}.fbx";
    }

    private static readonly MutantClipDefinition[] MutantClips =
    {
        new MutantClipDefinition("Idle", "Mutant@Mutant Idle", true, true),
        new MutantClipDefinition("Walk", "Mutant@Mutant Walking", true, true),
        new MutantClipDefinition("Run", "Mutant@Mutant Run", true, true),
        new MutantClipDefinition("Jump", "Mutant@Mutant Jumping", false, true),
        new MutantClipDefinition("Fall", "Mutant@Falling Idle", true, true),
        new MutantClipDefinition("Land", "Mutant@Hard Landing", false, true),
        new MutantClipDefinition("CrouchIdle", "Mutant@CrouchIdle", true, true),
        new MutantClipDefinition("CrouchWalk", "Mutant@CrouchWalk", true, true),
        new MutantClipDefinition("Attack", "Mutant@Mutant Swiping", false, true),
        new MutantClipDefinition("Death", "Mutant@Mutant Dying", false, true),
        new MutantClipDefinition("JumpAttack", "Mutant@Jump Attack", false, false),
        new MutantClipDefinition(
            "StealthAssassination",
            "Mutant@Stealth Assassination",
            false,
            false
        )
    };

    [MenuItem("Tools/Fault Hunters/Mutant/Configurar Mutant")]
    public static void ConfigurarMutant()
    {
        try
        {
            ConfigureHumanoidModel();
            Avatar avatar = LoadMutantAvatar();
            ConfigureMutantAnimationImports(avatar);
            AnimatorController controller = ConfigureAnimatorController();
            ConfigurePrefab(avatar, controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateMutant();
            Debug.Log("Mutant configurado correctamente.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    [MenuItem("Tools/Fault Hunters/Mutant/Configurar animaciones propias")]
    public static void ConfigurarAnimacionesPropiasMutant()
    {
        Avatar avatar = LoadMutantAvatar();
        ConfigureMutantAnimationImports(avatar);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            ControllerPath
        );
        if (controller == null)
        {
            throw new InvalidOperationException("No existe Mutant.controller.");
        }

        EnsureParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Death", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "IsDead", AnimatorControllerParameterType.Bool);

        ReplaceControllerMotions(controller);
        SyncCombatDurations();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateMutant();
        Debug.Log("Animaciones propias del Mutant configuradas correctamente.");
    }

    [MenuItem("Tools/Fault Hunters/Mutant/Validar Mutant")]
    public static void ValidateMutant()
    {
        Avatar avatar = LoadMutantAvatar();
        if (!avatar.isValid || !avatar.isHuman)
        {
            throw new InvalidOperationException("El Avatar del Mutant no es Humanoid valido.");
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            ControllerPath
        );
        if (controller == null)
        {
            throw new InvalidOperationException("No existe Mutant.controller.");
        }

        string[] requiredParameters =
        {
            "Speed", "IsGrounded", "IsCrouching", "VerticalVelocity",
            "Jump", "Land", "Attack", "Hit", "Death", "IsDead"
        };
        foreach (string parameter in requiredParameters)
        {
            if (!controller.parameters.Any(item => item.name == parameter))
            {
                throw new InvalidOperationException($"Falta el parametro Animator {parameter}.");
            }
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        string[] requiredStates =
        {
            "Locomotion", "Crouch Locomotion", "Jump", "Fall", "Land", "Attack", "Death"
        };
        foreach (string stateName in requiredStates)
        {
            if (!machine.states.Any(item => item.state.name == stateName))
            {
                throw new InvalidOperationException($"Falta el estado Animator {stateName}.");
            }
        }

        AnimatorState locomotion = machine.states.First(
            item => item.state.name == "Locomotion"
        ).state;
        AnimatorState crouch = machine.states.First(
            item => item.state.name == "Crouch Locomotion"
        ).state;
        if (locomotion.motion is not BlendTree locomotionTree ||
            locomotionTree.children.Length != 3)
        {
            throw new InvalidOperationException("Locomotion no tiene sus tres clips.");
        }

        if (crouch.motion is not BlendTree crouchTree || crouchTree.children.Length != 2)
        {
            throw new InvalidOperationException("Crouch Locomotion no tiene sus dos clips.");
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException("No existe el prefab Mutant.");
        }

        if ((prefab.transform.localScale - Vector3.one * MutantScale).sqrMagnitude > 0.0001f)
        {
            throw new InvalidOperationException("La escala del prefab Mutant no es 3.4 uniforme.");
        }

        CharacterController character = prefab.GetComponent<CharacterController>();
        PlayerInput playerInput = prefab.GetComponent<PlayerInput>();
        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        Transform hurtboxes = prefab.transform.Find("Hurtboxes");
        Collider legacyHurtbox = prefab.transform.Find("Hurtbox")?
            .GetComponent<Collider>();
        bool hasDamageCoverage = legacyHurtbox != null ||
                                 (hurtboxes != null && hurtboxes.childCount > 0);
        if (character == null || playerInput == null || animator == null ||
            !hasDamageCoverage)
        {
            throw new InvalidOperationException("Faltan componentes fisicos o de entrada.");
        }

        if (playerInput.actions == null ||
            AssetDatabase.GetAssetPath(playerInput.actions) != InputPath)
        {
            throw new InvalidOperationException("PlayerInput no usa Player2Controller.");
        }

        if (animator.avatar != avatar || animator.runtimeAnimatorController != controller ||
            animator.applyRootMotion)
        {
            throw new InvalidOperationException("Animator, Avatar o Root Motion incorrectos.");
        }

        Type[] requiredComponents =
        {
            typeof(MutantInputReader), typeof(MutantStateController),
            typeof(MutantMotor), typeof(MutantAnimationController),
            typeof(MutantCombat), typeof(MutantStats), typeof(MutantControlMode)
        };
        foreach (Type type in requiredComponents)
        {
            if (prefab.GetComponent(type) == null)
            {
                throw new InvalidOperationException($"Falta {type.Name} en el prefab.");
            }
        }

        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) != 0)
        {
            throw new InvalidOperationException("El prefab contiene Missing Scripts.");
        }

        ValidateMutantAnimationAssignments(controller, avatar);

        Debug.Log(
            $"Validacion Mutant correcta. Scale={prefab.transform.localScale}, " +
            $"CharacterController center={character.center}, height={character.height:F3}, " +
            $"radius={character.radius:F3}, skin={character.skinWidth:F3}, " +
            $"step={character.stepOffset:F3}, slope={character.slopeLimit:F1}."
        );
    }

    [MenuItem("Tools/Fault Hunters/Mutant/Preparar SampleScene para probar Mutant")]
    public static void PrepararSampleSceneParaMutant()
    {
        Scene scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException("Primero debe existir el prefab Mutant.");
        }

        GameObject mutant = scene.GetRootGameObjects()
            .FirstOrDefault(root => root.GetComponent<MutantControlMode>() != null);
        GameObject rawMutant = scene.GetRootGameObjects()
            .FirstOrDefault(root => root.name.StartsWith("Mutant") &&
                                    root.GetComponent<MutantControlMode>() == null);

        Vector3 spawnPosition = new Vector3(0.65f, 0.55f, 0f);
        Quaternion spawnRotation = Quaternion.identity;
        if (rawMutant != null)
        {
            spawnPosition = rawMutant.transform.position;
            spawnRotation = rawMutant.transform.rotation;
            UnityEngine.Object.DestroyImmediate(rawMutant);
        }

        if (mutant == null)
        {
            mutant = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        }

        if (mutant == null)
        {
            throw new InvalidOperationException("No se pudo colocar Mutant.prefab en SampleScene.");
        }

        mutant.name = "Mutant";
        mutant.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

        GameObject cazador = scene.GetRootGameObjects()
            .FirstOrDefault(root => root.name == "Cazador");
        if (cazador != null &&
            Vector3.ProjectOnPlane(cazador.transform.position - spawnPosition, Vector3.up)
                .sqrMagnitude < 3.24f)
        {
            cazador.transform.position = new Vector3(-1.45f, 0.55f, 0f);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(
            "SampleScene preparada: Mutant usa Player2Controller y toma el control al entrar en Play."
        );
    }

    private static void ConfigureHumanoidModel()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"No se encontro {ModelPath}.");
        }

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null)
        {
            throw new InvalidOperationException("No se pudo cargar la jerarquia de Mutant.fbx.");
        }

        HumanDescription description = importer.humanDescription;
        description.human = CreateHumanBoneMapping();
        description.skeleton = CreateSkeleton(modelAsset);
        description.armStretch = 0.05f;
        description.legStretch = 0.05f;
        description.upperArmTwist = 0.5f;
        description.lowerArmTwist = 0.5f;
        description.upperLegTwist = 0.5f;
        description.lowerLegTwist = 0.5f;
        description.feetSpacing = 0f;
        description.hasTranslationDoF = false;

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.humanDescription = description;
        importer.importAnimation = true;
        importer.SaveAndReimport();
    }

    private static Avatar LoadMutantAvatar()
    {
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
            .OfType<Avatar>()
            .FirstOrDefault();
        if (avatar == null)
        {
            throw new InvalidOperationException("Mutant.fbx no genero Avatar.");
        }

        return avatar;
    }

    private static void ConfigureMutantAnimationImports(Avatar avatar)
    {
        foreach (MutantClipDefinition definition in MutantClips)
        {
            ModelImporter importer = AssetImporter.GetAtPath(definition.Path) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"No se encontro la animacion propia: {definition.Path}"
                );
            }

            bool requiresOwnSourceAvatar = RequiresOwnSourceAvatar(definition);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = requiresOwnSourceAvatar
                ? ModelImporterAvatarSetup.CreateFromThisModel
                : ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = requiresOwnSourceAvatar ? null : avatar;
            importer.importAnimation = true;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips == null || clips.Length == 0)
            {
                throw new InvalidOperationException(
                    $"{definition.FileName}.fbx no contiene AnimationClip importable."
                );
            }

            ModelImporterClipAnimation clip = clips[0];
            clip.name = definition.StateName;
            clip.loopTime = definition.Loop;
            clip.loopPose = definition.Loop;
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
            clips[0] = clip;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            AnimationClip importedClip = LoadClip(definition.StateName);
            if (importedClip == null || !importedClip.humanMotion)
            {
                throw new InvalidOperationException(
                    $"Unity no pudo retargetear {definition.FileName} con el Avatar Mutant."
                );
            }
        }
    }

    private static void ReplaceControllerMotions(AnimatorController controller)
    {
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState locomotion = FindState(machine, "Locomotion");
        AnimatorState crouch = FindState(machine, "Crouch Locomotion");
        AnimatorState jump = FindState(machine, "Jump");
        AnimatorState fall = FindState(machine, "Fall");
        AnimatorState land = FindState(machine, "Land");
        AnimatorState attack = FindState(machine, "Attack");
        AnimatorState death = machine.states
            .Select(item => item.state)
            .FirstOrDefault(item => item.name == "Death") ?? machine.AddState("Death");

        if (locomotion.motion is not BlendTree locomotionTree ||
            locomotionTree.children.Length != 3)
        {
            throw new InvalidOperationException("Locomotion no conserva su Blend Tree de 3 clips.");
        }

        ChildMotion[] locomotionChildren = locomotionTree.children;
        locomotionChildren[0].motion = LoadRequiredClip("Idle");
        locomotionChildren[1].motion = LoadRequiredClip("Walk");
        locomotionChildren[2].motion = LoadRequiredClip("Run");
        locomotionTree.children = locomotionChildren;

        if (crouch.motion is not BlendTree crouchTree || crouchTree.children.Length != 2)
        {
            throw new InvalidOperationException(
                "Crouch Locomotion no conserva su Blend Tree de 2 clips."
            );
        }

        ChildMotion[] crouchChildren = crouchTree.children;
        crouchChildren[0].motion = LoadRequiredClip("CrouchIdle");
        crouchChildren[1].motion = LoadRequiredClip("CrouchWalk");
        crouchTree.children = crouchChildren;

        jump.motion = LoadRequiredClip("Jump");
        fall.motion = LoadRequiredClip("Fall");
        land.motion = LoadRequiredClip("Land");
        attack.motion = LoadRequiredClip("Attack");
        death.motion = LoadRequiredClip("Death");

        foreach (AnimatorStateTransition transition in death.transitions.ToArray())
        {
            death.RemoveTransition(transition);
        }

        bool hasDeathTransition = machine.anyStateTransitions.Any(transition =>
            transition.destinationState == death);
        if (!hasDeathTransition)
        {
            AnimatorStateTransition transition = machine.AddAnyStateTransition(death);
            transition.hasExitTime = false;
            transition.duration = 0.08f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
        }

        foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
        {
            if (transition.destinationState == attack)
            {
                transition.canTransitionToSelf = false;
            }
        }

        EditorUtility.SetDirty(locomotionTree);
        EditorUtility.SetDirty(crouchTree);
        EditorUtility.SetDirty(controller);
    }

    private static void SyncCombatDurations()
    {
        AnimationClip attackClip = LoadRequiredClip("Attack");
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            MutantCombat combat = root.GetComponent<MutantCombat>();
            if (combat == null)
            {
                throw new InvalidOperationException("Mutant.prefab no contiene MutantCombat.");
            }

            SerializedObject serialized = new SerializedObject(combat);
            serialized.FindProperty("duracionAtaque").floatValue = attackClip.length;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            MutantDeathController deathController = root.GetComponent<MutantDeathController>();
            if (deathController != null)
            {
                SerializedObject deathSerialized = new SerializedObject(deathController);
                deathSerialized.FindProperty("tiempoVisible").floatValue =
                    LoadRequiredClip("Death").length + 1f;
                deathSerialized.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ValidateMutantAnimationAssignments(
        AnimatorController controller,
        Avatar avatar
    )
    {
        foreach (MutantClipDefinition definition in MutantClips)
        {
            ModelImporter importer = AssetImporter.GetAtPath(definition.Path) as ModelImporter;
            AnimationClip clip = LoadRequiredClip(definition.StateName);
            bool ownSource = RequiresOwnSourceAvatar(definition);
            bool avatarConfigurationValid = ownSource
                ? importer != null &&
                  importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel &&
                  AssetDatabase.LoadAllAssetsAtPath(definition.Path)
                      .OfType<Avatar>().Any(source => source.isValid && source.isHuman)
                : importer != null &&
                  importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther &&
                  importer.sourceAvatar == avatar;
            if (importer == null || importer.animationType != ModelImporterAnimationType.Human ||
                !avatarConfigurationValid || clip.isLooping != definition.Loop)
            {
                throw new InvalidOperationException(
                    $"Rig, Avatar o Loop incorrecto en {definition.FileName}.fbx."
                );
            }
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        BlendTree locomotion = (BlendTree)FindState(machine, "Locomotion").motion;
        BlendTree crouch = (BlendTree)FindState(machine, "Crouch Locomotion").motion;
        AssertMotion(locomotion.children[0].motion, "Idle");
        AssertMotion(locomotion.children[1].motion, "Walk");
        AssertMotion(locomotion.children[2].motion, "Run");
        AssertMotion(crouch.children[0].motion, "CrouchIdle");
        AssertMotion(crouch.children[1].motion, "CrouchWalk");
        AssertMotion(FindState(machine, "Jump").motion, "Jump");
        AssertMotion(FindState(machine, "Fall").motion, "Fall");
        AssertMotion(FindState(machine, "Land").motion, "Land");
        AssertMotion(FindState(machine, "Attack").motion, "Attack");
        AssertMotion(FindState(machine, "Death").motion, "Death");
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        AnimatorState state = machine.states
            .FirstOrDefault(item => item.state.name == name).state;
        return state ?? throw new InvalidOperationException($"Falta el estado {name}.");
    }

    private static bool RequiresOwnSourceAvatar(MutantClipDefinition definition)
    {
        return definition.StateName == "CrouchIdle" ||
               definition.StateName == "CrouchWalk";
    }

    private static void AssertMotion(Motion actual, string stateName)
    {
        AnimationClip expected = LoadRequiredClip(stateName);
        if (actual != expected ||
            !AssetDatabase.GetAssetPath(expected).StartsWith(
                "Assets/Project/Art/Animations/mutant/",
                StringComparison.Ordinal
            ))
        {
            throw new InvalidOperationException(
                $"El motion {stateName} no usa la animacion propia del Mutant."
            );
        }
    }

    private static AnimatorController ConfigureAnimatorController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            ControllerPath
        );
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "IsGrounded", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "IsCrouching", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "VerticalVelocity", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "Jump", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Land", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "Death", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "IsDead", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        ClearStateMachine(machine);
        foreach (BlendTree oldTree in AssetDatabase.LoadAllAssetsAtPath(ControllerPath)
                     .OfType<BlendTree>().ToArray())
        {
            UnityEngine.Object.DestroyImmediate(oldTree, true);
        }

        AnimationClip idle = LoadClip("Idle");
        AnimationClip walk = LoadClip("Walk");
        AnimationClip run = LoadClip("Run");
        AnimationClip crouchIdle = LoadClip("CrouchIdle");
        AnimationClip crouchWalk = LoadClip("CrouchWalk");

        BlendTree locomotionTree = CreateBlendTree(
            controller,
            "Mutant Locomotion Blend Tree",
            "Speed",
            (idle, 0f), (walk, 0.61f), (run, 1f)
        );
        BlendTree crouchTree = CreateBlendTree(
            controller,
            "Mutant Crouch Blend Tree",
            "Speed",
            (crouchIdle, 0f), (crouchWalk, 0.32f)
        );

        AnimatorState locomotion = machine.AddState("Locomotion", new Vector3(240f, 20f));
        AnimatorState crouch = machine.AddState("Crouch Locomotion", new Vector3(240f, 160f));
        AnimatorState jump = machine.AddState("Jump", new Vector3(500f, -100f));
        AnimatorState fall = machine.AddState("Fall", new Vector3(720f, -100f));
        AnimatorState land = machine.AddState("Land", new Vector3(720f, 20f));
        AnimatorState attack = machine.AddState("Attack", new Vector3(500f, 220f));
        AnimatorState death = machine.AddState("Death", new Vector3(920f, 220f));
        machine.defaultState = locomotion;

        locomotion.motion = locomotionTree;
        crouch.motion = crouchTree;
        jump.motion = LoadClip("Jump");
        fall.motion = LoadClip("Fall");
        land.motion = LoadClip("Land");
        attack.motion = LoadClip("Attack");
        death.motion = LoadClip("Death");

        AddConditionTransition(
            locomotion, crouch, AnimatorConditionMode.If, 0f, "IsCrouching", 0.12f
        );
        AddConditionTransition(
            crouch, locomotion, AnimatorConditionMode.IfNot, 0f, "IsCrouching", 0.12f
        );
        AddConditionTransition(
            jump, fall, AnimatorConditionMode.Less, 0f, "VerticalVelocity", 0.08f
        );
        AddConditionTransition(
            fall, land, AnimatorConditionMode.If, 0f, "IsGrounded", 0.08f
        );
        AddExitTransition(land, locomotion, 0.88f, 0.08f);
        AddExitTransition(attack, locomotion, 0.94f, 0.1f);

        AnimatorStateTransition deathTransition = machine.AddAnyStateTransition(death);
        deathTransition.hasExitTime = false;
        deathTransition.duration = 0.08f;
        deathTransition.canTransitionToSelf = false;
        deathTransition.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");

        AddAnyStateTrigger(machine, jump, "Jump", 0.06f);
        AddAnyStateTrigger(machine, land, "Land", 0.06f);
        AddAnyStateTrigger(machine, attack, "Attack", 0.06f);
        AnimatorStateTransition fallTransition = machine.AddAnyStateTransition(fall);
        fallTransition.hasExitTime = false;
        fallTransition.duration = 0.08f;
        fallTransition.canTransitionToSelf = false;
        fallTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
        fallTransition.AddCondition(AnimatorConditionMode.Less, -1f, "VerticalVelocity");

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void ConfigurePrefab(Avatar avatar, AnimatorController controller)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
        if (modelAsset == null || inputActions == null)
        {
            throw new InvalidOperationException("Falta el modelo Mutant o Player2Controller.");
        }

        int playerLayer = LayerMask.NameToLayer("Player");
        int groundLayer = LayerMask.NameToLayer("Ground");
        int damageableLayer = LayerMask.NameToLayer("Damageable");
        if (playerLayer < 0 || groundLayer < 0 || damageableLayer < 0)
        {
            throw new InvalidOperationException("Faltan las capas Player, Ground o Damageable.");
        }

        GameObject root = new GameObject("Mutant");
        try
        {
            root.layer = playerLayer;
            Transform visual = CreateChild(root.transform, "Visual", playerLayer);
            GameObject model = PrefabUtility.InstantiatePrefab(modelAsset, visual) as GameObject;
            if (model == null)
            {
                throw new InvalidOperationException("No se pudo instanciar Mutant.fbx.");
            }

            model.name = "MutantModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            SetLayerRecursively(model, playerLayer);

            Bounds bounds = CalculateRendererBounds(model);
            visual.localPosition = new Vector3(
                -bounds.center.x,
                -bounds.min.y,
                -bounds.center.z
            );
            float height = Mathf.Max(1f, bounds.size.y);
            float depth = Mathf.Max(0.4f, bounds.size.z);
            float radius = Mathf.Clamp(depth * 0.48f, 0.32f, height * 0.25f);

            Animator animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            CharacterController character = root.AddComponent<CharacterController>();
            character.height = height;
            character.radius = radius;
            character.center = new Vector3(0f, height * 0.5f, 0f);
            character.skinWidth = Mathf.Max(0.025f, radius * 0.1f);
            character.stepOffset = Mathf.Min(height * 0.1f, 0.22f);
            character.slopeLimit = 50f;
            character.minMoveDistance = 0f;

            Transform cameraTarget = CreateChild(root.transform, "CameraTarget", playerLayer);
            cameraTarget.localPosition = Vector3.up * height * 0.76f;
            Transform groundCheck = CreateChild(root.transform, "GroundCheck", playerLayer);
            groundCheck.localPosition = Vector3.up * 0.025f;
            Transform attackOrigin = CreateChild(root.transform, "AttackOrigin", playerLayer);
            attackOrigin.localPosition = new Vector3(0f, height * 0.56f, depth * 0.4f);
            Transform hurtboxObject = CreateChild(root.transform, "Hurtbox", damageableLayer);

            CapsuleCollider hurtbox = hurtboxObject.gameObject.AddComponent<CapsuleCollider>();
            hurtbox.isTrigger = true;
            hurtbox.height = height * 0.96f;
            hurtbox.radius = radius * 1.08f;
            hurtbox.center = Vector3.up * hurtbox.height * 0.5f;

            Transform cameraObject = CreateChild(root.transform, "MutantCamera", playerLayer);
            Camera camera = cameraObject.gameObject.AddComponent<Camera>();
            camera.tag = "Untagged";
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            cameraObject.gameObject.AddComponent<UniversalAdditionalCameraData>();
            AudioListener listener = cameraObject.gameObject.AddComponent<AudioListener>();

            PlayerInput playerInput = root.AddComponent<PlayerInput>();
            playerInput.actions = inputActions;
            playerInput.defaultActionMap = "Player";
            playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

            MutantInputReader input = root.AddComponent<MutantInputReader>();
            MutantStateController state = root.AddComponent<MutantStateController>();
            MutantAnimationController animation = root.AddComponent<MutantAnimationController>();
            MutantMotor motor = root.AddComponent<MutantMotor>();
            MutantAttackHitbox attackHitbox = attackOrigin.gameObject
                .AddComponent<MutantAttackHitbox>();
            MutantCombat combat = root.AddComponent<MutantCombat>();
            MutantStats stats = root.AddComponent<MutantStats>();
            MutantHurtbox hurtboxScript = hurtboxObject.gameObject
                .AddComponent<MutantHurtbox>();
            MutantCameraController cameraController = cameraObject.gameObject
                .AddComponent<MutantCameraController>();
            MutantControlMode controlMode = root.AddComponent<MutantControlMode>();

            SetObject(animation, "animator", animator);
            SetObject(animation, "motor", motor);
            SetObject(animation, "state", state);
            SetObject(motor, "fuenteIntenciones", input);
            SetObject(motor, "camaraTransform", camera.transform);
            SetObject(motor, "groundCheck", groundCheck);
            SetObject(motor, "state", state);
            SetObject(motor, "animationController", animation);
            SetLayerMask(motor, "capasSuelo", 1 << groundLayer);
            SetLayerMask(motor, "capasObstruccion", ~(1 << playerLayer));
            SetObject(attackHitbox, "origenAtaque", attackOrigin);
            SetObject(attackHitbox, "raizPropietario", root.transform);
            SetLayerMask(
                attackHitbox,
                "capasGolpeables",
                (1 << playerLayer) | (1 << damageableLayer)
            );
            SetObject(combat, "fuenteIntenciones", input);
            SetObject(combat, "state", state);
            SetObject(combat, "animationController", animation);
            SetObject(combat, "hitbox", attackHitbox);
            SetObject(stats, "state", state);
            SetObject(hurtboxScript, "receptor", stats);
            cameraController.Configure(cameraTarget, input);
            SetLayerMask(cameraController, "capasColision", ~(1 << playerLayer));
            SetObject(controlMode, "playerInput", playerInput);
            SetObject(controlMode, "inputReader", input);
            SetObject(controlMode, "cameraController", cameraController);
            SetObject(controlMode, "mutantCamera", camera);
            SetObject(controlMode, "audioListener", listener);

            root.transform.localScale = Vector3.one * MutantScale;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static AnimationClip LoadClip(string clipName)
    {
        MutantClipDefinition definition = MutantClips.FirstOrDefault(
            item => item.StateName == clipName
        );
        if (string.IsNullOrEmpty(definition.FileName))
        {
            return null;
        }

        return AssetDatabase.LoadAllAssetsAtPath(definition.Path)
            .OfType<AnimationClip>()
            .FirstOrDefault(item =>
                !item.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                item.name == definition.StateName
            );
    }

    private static AnimationClip LoadRequiredClip(string clipName)
    {
        AnimationClip clip = LoadClip(clipName);
        if (clip == null || !clip.humanMotion)
        {
            throw new InvalidOperationException(
                $"No se encontro el clip Humanoid propio para {clipName}."
            );
        }

        return clip;
    }

    private static void EnsureParameter(
        AnimatorController controller,
        string name,
        AnimatorControllerParameterType type
    )
    {
        AnimatorControllerParameter existing = controller.parameters
            .FirstOrDefault(item => item.name == name);
        if (existing == null)
        {
            controller.AddParameter(name, type);
        }
        else if (existing.type != type)
        {
            controller.RemoveParameter(existing);
            controller.AddParameter(name, type);
        }
    }

    private static void ClearStateMachine(AnimatorStateMachine machine)
    {
        foreach (ChildAnimatorState state in machine.states.ToArray())
        {
            machine.RemoveState(state.state);
        }

        foreach (ChildAnimatorStateMachine child in machine.stateMachines.ToArray())
        {
            machine.RemoveStateMachine(child.stateMachine);
        }

        foreach (AnimatorStateTransition transition in machine.anyStateTransitions.ToArray())
        {
            machine.RemoveAnyStateTransition(transition);
        }
    }

    private static BlendTree CreateBlendTree(
        AnimatorController controller,
        string name,
        string parameter,
        params (Motion motion, float threshold)[] children
    )
    {
        BlendTree tree = new BlendTree
        {
            name = name,
            blendType = BlendTreeType.Simple1D,
            blendParameter = parameter,
            useAutomaticThresholds = false
        };
        AssetDatabase.AddObjectToAsset(tree, controller);
        foreach ((Motion motion, float threshold) child in children)
        {
            tree.AddChild(child.motion, child.threshold);
        }

        return tree;
    }

    private static void AddConditionTransition(
        AnimatorState from,
        AnimatorState to,
        AnimatorConditionMode mode,
        float threshold,
        string parameter,
        float duration
    )
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.AddCondition(mode, threshold, parameter);
    }

    private static void AddExitTransition(
        AnimatorState from,
        AnimatorState to,
        float exitTime,
        float duration
    )
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.duration = duration;
    }

    private static void AddAnyStateTrigger(
        AnimatorStateMachine machine,
        AnimatorState target,
        string parameter,
        float duration
    )
    {
        AnimatorStateTransition transition = machine.AddAnyStateTransition(target);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);
    }

    private static Transform CreateChild(Transform parent, string name, int layer)
    {
        GameObject child = new GameObject(name) { layer = layer };
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            throw new InvalidOperationException("Mutant.fbx no contiene Renderer.");
        }

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        return bounds;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            transform.gameObject.layer = layer;
        }
    }

    private static HumanBone[] CreateHumanBoneMapping()
    {
        return new[]
        {
            HumanBone("Hips", "mixamorig:Hips"),
            HumanBone("Spine", "mixamorig:Spine"),
            HumanBone("Chest", "mixamorig:Spine1"),
            HumanBone("UpperChest", "mixamorig:Spine2"),
            HumanBone("Neck", "mixamorig:Neck"),
            HumanBone("Head", "mixamorig:Head"),
            HumanBone("LeftShoulder", "mixamorig:LeftShoulder"),
            HumanBone("LeftUpperArm", "mixamorig:LeftArm"),
            HumanBone("LeftLowerArm", "mixamorig:LeftForeArm"),
            HumanBone("LeftHand", "mixamorig:LeftHand"),
            HumanBone("RightShoulder", "mixamorig:RightShoulder"),
            HumanBone("RightUpperArm", "mixamorig:RightArm"),
            HumanBone("RightLowerArm", "mixamorig:RightForeArm"),
            HumanBone("RightHand", "mixamorig:RightHand"),
            HumanBone("LeftUpperLeg", "mixamorig:LeftUpLeg"),
            HumanBone("LeftLowerLeg", "mixamorig:LeftLeg"),
            HumanBone("LeftFoot", "mixamorig:LeftFoot"),
            HumanBone("LeftToes", "mixamorig:LeftToeBase"),
            HumanBone("RightUpperLeg", "mixamorig:RightUpLeg"),
            HumanBone("RightLowerLeg", "mixamorig:RightLeg"),
            HumanBone("RightFoot", "mixamorig:RightFoot"),
            HumanBone("RightToes", "mixamorig:RightToeBase")
        };
    }

    private static HumanBone HumanBone(string humanName, string boneName)
    {
        return new HumanBone
        {
            humanName = humanName,
            boneName = boneName,
            limit = new HumanLimit { useDefaultValues = true }
        };
    }

    private static SkeletonBone[] CreateSkeleton(GameObject modelAsset)
    {
        return modelAsset.GetComponentsInChildren<Transform>(true)
            .Select(transform => new SkeletonBone
            {
                name = transform.name,
                position = transform.localPosition,
                rotation = transform.localRotation,
                scale = transform.localScale
            })
            .ToArray();
    }

    private static void SetObject(UnityEngine.Object target, string property, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty field = serialized.FindProperty(property);
        if (field == null)
        {
            throw new MissingFieldException(target.GetType().Name, property);
        }

        field.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLayerMask(UnityEngine.Object target, string property, int value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty field = serialized.FindProperty(property);
        if (field == null)
        {
            throw new MissingFieldException(target.GetType().Name, property);
        }

        field.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
