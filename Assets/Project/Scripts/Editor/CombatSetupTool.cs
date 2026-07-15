#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;

public static class CombatSetupTool
{
    private const string CazadorPrefabPath =
        "Assets/Project/Prefabs/Player/Cazador.prefab";
    private const string MutantPrefabPath =
        "Assets/Project/Prefabs/Bosses/Mutant.prefab";
    private const string CazadorControllerPath =
        "Assets/Project/Art/Animations/Cazador.controller";
    private const string MutantControllerPath =
        "Assets/Project/Art/Animations/Mutant.controller";
    private const string CazadorDeathPath =
        "Assets/Project/Art/Animations/cazador/cazador@Sword And Shield Death.fbx";
    private const string MutantDeathPath =
        "Assets/Project/Art/Animations/mutant/Mutant@Mutant Dying.fbx";

    private readonly struct HurtboxDefinition
    {
        public HurtboxDefinition(
            string name,
            HumanBodyBones bone,
            float worldRadius,
            params string[] fallbackNames
        )
        {
            Name = name;
            Bone = bone;
            WorldRadius = worldRadius;
            FallbackNames = fallbackNames;
        }

        public string Name { get; }
        public HumanBodyBones Bone { get; }
        public float WorldRadius { get; }
        public string[] FallbackNames { get; }
    }

    [MenuItem("Tools/Fault Hunters/Configurar combate y muerte")]
    public static void ConfigurarCombateYMuerte()
    {
        ConfigureCazadorPrefab();
        ConfigureMutantPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidarCombateYMuerte();
        Debug.Log("Combate, hurtboxes, efectos y muerte configurados para Cazador y Mutant.");
    }

    [MenuItem("Tools/Fault Hunters/Validar combate y muerte")]
    public static void ValidarCombateYMuerte()
    {
        ValidatePrefab(
            CazadorPrefabPath,
            CazadorControllerPath,
            "Death",
            CazadorDeathPath,
            new[]
            {
                "Hurtbox_Head", "Hurtbox_Torso", "Hurtbox_Pelvis",
                "Hurtbox_LeftLeg", "Hurtbox_RightLeg"
            },
            typeof(HitboxAtaque),
            typeof(CazadorDeathController),
            typeof(CazadorStats)
        );
        ValidatePrefab(
            MutantPrefabPath,
            MutantControllerPath,
            "Death",
            MutantDeathPath,
            new[]
            {
                "Hurtbox_Torso", "Hurtbox_Pelvis", "Hurtbox_LeftLeg",
                "Hurtbox_RightLeg", "Hurtbox_LeftFoot", "Hurtbox_RightFoot"
            },
            typeof(MutantAttackHitbox),
            typeof(MutantDeathController),
            typeof(MutantStats)
        );
    }

    private static void ConfigureCazadorPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CazadorPrefabPath);
        try
        {
            Animator animator = RequireAnimator(root);
            CazadorStats stats = Require<CazadorStats>(root);
            DamageEffects effects = GetOrAdd<DamageEffects>(root);
            CazadorDeathController death = GetOrAdd<CazadorDeathController>(root);
            SetObject(stats, "efectosDano", effects);
            SetFloat(death, "tiempoVisible", LoadClip(CazadorDeathPath, "Death").length + 1f);

            ConfigureHurtboxes(
                root,
                animator,
                stats,
                new[]
                {
                    new HurtboxDefinition("Hurtbox_Head", HumanBodyBones.Head, 0.24f, "mixamorig:Head"),
                    new HurtboxDefinition("Hurtbox_Torso", HumanBodyBones.Chest, 0.36f, "mixamorig:Spine1", "mixamorig:Spine2"),
                    new HurtboxDefinition("Hurtbox_Pelvis", HumanBodyBones.Hips, 0.3f, "mixamorig:Hips"),
                    new HurtboxDefinition("Hurtbox_LeftLeg", HumanBodyBones.LeftLowerLeg, 0.24f, "mixamorig:LeftLeg"),
                    new HurtboxDefinition("Hurtbox_RightLeg", HumanBodyBones.RightLowerLeg, 0.24f, "mixamorig:RightLeg")
                }
            );

            Transform rightHand = ResolveBone(
                animator,
                HumanBodyBones.RightHand,
                "mixamorig:RightHand"
            );
            Transform attackOrigin = root.transform.Find("AttackOrigin");
            HitboxAtaque hitbox = attackOrigin != null
                ? attackOrigin.GetComponent<HitboxAtaque>()
                : null;
            if (attackOrigin == null || hitbox == null)
            {
                throw new InvalidOperationException("Cazador no contiene AttackOrigin/HitboxAtaque.");
            }

            FollowBone(attackOrigin, rightHand);
            SetObject(hitbox, "origenAtaque", attackOrigin);
            SetObject(hitbox, "raizPropietario", root.transform);
            SetObject(hitbox, "direccionReferencia", root.transform);
            SetFloat(hitbox, "radio", 0.52f);
            SetFloat(hitbox, "desplazamientoFrontal", 0.32f);
            PrefabUtility.SaveAsPrefabAsset(root, CazadorPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureMutantPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MutantPrefabPath);
        try
        {
            Animator animator = RequireAnimator(root);
            MutantStats stats = Require<MutantStats>(root);
            DamageEffects effects = GetOrAdd<DamageEffects>(root);
            MutantDeathController death = GetOrAdd<MutantDeathController>(root);
            SetObject(stats, "efectosDano", effects);
            SetFloat(death, "tiempoVisible", LoadClip(MutantDeathPath, "Death").length + 1f);

            Transform legacyHurtbox = root.transform.Find("Hurtbox");
            if (legacyHurtbox != null)
            {
                UnityEngine.Object.DestroyImmediate(legacyHurtbox.gameObject);
            }

            ConfigureHurtboxes(
                root,
                animator,
                stats,
                new[]
                {
                    new HurtboxDefinition("Hurtbox_Torso", HumanBodyBones.Chest, 0.9f, "mixamorig:Spine1", "mixamorig:Spine2"),
                    new HurtboxDefinition("Hurtbox_Pelvis", HumanBodyBones.Hips, 0.76f, "mixamorig:Hips"),
                    new HurtboxDefinition("Hurtbox_LeftLeg", HumanBodyBones.LeftLowerLeg, 0.62f, "mixamorig:LeftLeg"),
                    new HurtboxDefinition("Hurtbox_RightLeg", HumanBodyBones.RightLowerLeg, 0.62f, "mixamorig:RightLeg"),
                    new HurtboxDefinition("Hurtbox_LeftFoot", HumanBodyBones.LeftFoot, 0.52f, "mixamorig:LeftFoot"),
                    new HurtboxDefinition("Hurtbox_RightFoot", HumanBodyBones.RightFoot, 0.52f, "mixamorig:RightFoot")
                }
            );

            Transform rightHand = ResolveBone(
                animator,
                HumanBodyBones.RightHand,
                "mixamorig:RightHand"
            );
            Transform attackOrigin = root.transform.Find("AttackOrigin");
            MutantAttackHitbox hitbox = attackOrigin != null
                ? attackOrigin.GetComponent<MutantAttackHitbox>()
                : null;
            if (attackOrigin == null || hitbox == null)
            {
                throw new InvalidOperationException("Mutant no contiene AttackOrigin/MutantAttackHitbox.");
            }

            FollowBone(attackOrigin, rightHand);
            SetObject(hitbox, "origenAtaque", attackOrigin);
            SetObject(hitbox, "raizPropietario", root.transform);
            SetObject(hitbox, "direccionReferencia", root.transform);
            SetFloat(hitbox, "radio", 1.2f);
            SetFloat(hitbox, "desplazamientoFrontal", 0.65f);
            PrefabUtility.SaveAsPrefabAsset(root, MutantPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureHurtboxes(
        GameObject root,
        Animator animator,
        MonoBehaviour receiver,
        HurtboxDefinition[] definitions
    )
    {
        int damageableLayer = LayerMask.NameToLayer("Damageable");
        if (damageableLayer < 0)
        {
            throw new InvalidOperationException("No existe la capa Damageable.");
        }

        Transform previous = root.transform.Find("Hurtboxes");
        if (previous != null)
        {
            UnityEngine.Object.DestroyImmediate(previous.gameObject);
        }

        GameObject containerObject = new GameObject("Hurtboxes")
        {
            layer = damageableLayer
        };
        containerObject.transform.SetParent(root.transform, false);
        float rootScale = Mathf.Max(0.0001f, Mathf.Abs(root.transform.lossyScale.x));

        foreach (HurtboxDefinition definition in definitions)
        {
            Transform bone = ResolveBone(animator, definition.Bone, definition.FallbackNames);
            GameObject hurtboxObject = new GameObject(definition.Name)
            {
                layer = damageableLayer
            };
            hurtboxObject.transform.SetParent(containerObject.transform, false);
            hurtboxObject.transform.position = bone.position;

            SphereCollider collider = hurtboxObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = definition.WorldRadius / rootScale;
            Hurtbox hurtbox = hurtboxObject.AddComponent<Hurtbox>();
            hurtbox.Configurar(receiver);
            FollowBone(hurtboxObject.transform, bone);
        }
    }

    private static void FollowBone(Transform target, Transform bone)
    {
        PositionConstraint existing = target.GetComponent<PositionConstraint>();
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }

        target.position = bone.position;
        PositionConstraint constraint = target.gameObject.AddComponent<PositionConstraint>();
        constraint.AddSource(new ConstraintSource { sourceTransform = bone, weight = 1f });
        constraint.translationAtRest = target.localPosition;
        constraint.translationOffset = Vector3.zero;
        constraint.constraintActive = true;
        constraint.locked = true;
    }

    private static Transform ResolveBone(
        Animator animator,
        HumanBodyBones humanBone,
        params string[] fallbackNames
    )
    {
        if (animator.isHuman)
        {
            Transform bone = animator.GetBoneTransform(humanBone);
            if (bone != null)
            {
                return bone;
            }
        }

        Transform fallback = animator.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(transform => fallbackNames.Contains(transform.name));
        return fallback ?? throw new InvalidOperationException(
            $"No se encontro el hueso {humanBone} en {animator.name}."
        );
    }

    private static void ValidatePrefab(
        string prefabPath,
        string controllerPath,
        string deathStateName,
        string deathClipPath,
        string[] hurtboxNames,
        Type hitboxType,
        Type deathControllerType,
        Type statsType
    )
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            controllerPath
        );
        AnimationClip deathClip = LoadClip(deathClipPath, "Death");
        if (prefab == null || controller == null || deathClip == null || deathClip.isLooping)
        {
            throw new InvalidOperationException($"Prefab, controller o Death invalido: {prefabPath}");
        }

        if (prefab.GetComponent(statsType) == null ||
            prefab.GetComponent(deathControllerType) == null ||
            prefab.GetComponent<DamageEffects>() == null ||
            prefab.GetComponentInChildren(hitboxType, true) == null)
        {
            throw new InvalidOperationException($"Faltan componentes de combate en {prefab.name}.");
        }

        Transform container = prefab.transform.Find("Hurtboxes");
        if (container == null || container.childCount != hurtboxNames.Length)
        {
            throw new InvalidOperationException($"Hurtboxes incompletas en {prefab.name}.");
        }

        MonoBehaviour centralStats = prefab.GetComponent(statsType) as MonoBehaviour;
        foreach (string name in hurtboxNames)
        {
            Transform child = container.Find(name);
            Collider collider = child != null ? child.GetComponent<Collider>() : null;
            Hurtbox hurtbox = child != null ? child.GetComponent<Hurtbox>() : null;
            if (child == null || collider == null || !collider.isTrigger || hurtbox == null ||
                child.gameObject.layer != LayerMask.NameToLayer("Damageable"))
            {
                throw new InvalidOperationException($"Hurtbox invalida: {prefab.name}/{name}");
            }

            SerializedObject serialized = new SerializedObject(hurtbox);
            if (serialized.FindProperty("receptor").objectReferenceValue != centralStats)
            {
                throw new InvalidOperationException($"{name} no comparte la vida central.");
            }
        }

        AnimatorState deathState = controller.layers[0].stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(state => state.name == deathStateName);
        if (deathState == null || deathState.motion != deathClip ||
            deathState.transitions.Length != 0)
        {
            throw new InvalidOperationException($"Estado Death invalido en {controller.name}.");
        }

        string[] parameters = { "Hit", "Death", "IsDead" };
        foreach (string parameter in parameters)
        {
            if (!controller.parameters.Any(item => item.name == parameter))
            {
                throw new InvalidOperationException(
                    $"Falta {parameter} en {controller.name}."
                );
            }
        }

        foreach (Transform transform in prefab.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
            {
                throw new InvalidOperationException($"Missing Script en {transform.name}.");
            }
        }

        foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
        {
            if (child.state.motion == null)
            {
                throw new InvalidOperationException(
                    $"Missing Motion en {controller.name}/{child.state.name}."
                );
            }
        }

        Debug.Log($"Validacion de combate correcta: {prefab.name}.");
    }

    private static AnimationClip LoadClip(string path, string expectedName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip =>
                !clip.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                clip.name == expectedName
            );
    }

    private static Animator RequireAnimator(GameObject root)
    {
        Animator animator = root.GetComponentInChildren<Animator>(true);
        return animator ?? throw new InvalidOperationException($"{root.name} no tiene Animator.");
    }

    private static T Require<T>(GameObject root) where T : Component
    {
        T component = root.GetComponent<T>();
        return component ?? throw new InvalidOperationException($"{root.name} no tiene {typeof(T).Name}.");
    }

    private static T GetOrAdd<T>(GameObject root) where T : Component
    {
        return root.GetComponent<T>() ?? root.AddComponent<T>();
    }

    private static void SetObject(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null) throw new MissingFieldException(target.GetType().Name, name);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(UnityEngine.Object target, string name, float value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null) throw new MissingFieldException(target.GetType().Name, name);
        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
