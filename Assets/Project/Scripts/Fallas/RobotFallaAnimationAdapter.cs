using System.Collections.Generic;
using UnityEngine;

public enum RobotFallaMovementAnimation
{
    Walk,
    Roll
}

[DisallowMultipleComponent]
public sealed class RobotFallaAnimationAdapter : MonoBehaviour
{
    private static readonly int WalkHash = Animator.StringToHash("Walk_Anim");
    private static readonly int RollHash = Animator.StringToHash("Roll_Anim");
    private static readonly int OpenHash = Animator.StringToHash("Open_Anim");

    [SerializeField] private Animator animator;
    [SerializeField] private FallaCore core;
    [SerializeField] private FallaGenerator generator;
    [SerializeField] private RobotFallaMovementAnimation movimiento =
        RobotFallaMovementAnimation.Roll;
    [SerializeField] private Transform reactionRoot;
    [SerializeField, Min(0.05f)] private float duracionReaccion = 0.18f;
    [SerializeField, Min(0.05f)] private float duracionGeneracion = 0.7f;

    private readonly HashSet<int> availableParameters = new HashSet<int>();
    private Quaternion baseRotation;
    private float reactionTimer;
    private float generationTimer;
    private FallaState previousState;

    private void Awake()
    {
        animator ??= GetComponentInChildren<Animator>(true);
        core ??= GetComponentInParent<FallaCore>();
        generator ??= GetComponentInParent<FallaGenerator>();
        reactionRoot ??= transform;
        baseRotation = reactionRoot.localRotation;

        if (animator == null)
        {
            Debug.LogError("RobotFallaAnimationAdapter necesita un Animator.", this);
            enabled = false;
            return;
        }
        animator.applyRootMotion = false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            availableParameters.Add(parameter.nameHash);
        }
        ValidateParameter(WalkHash, "Walk_Anim");
        ValidateParameter(RollHash, "Roll_Anim");
        ValidateParameter(OpenHash, "Open_Anim");
        previousState = (FallaState)(-1);
    }

    private void OnEnable()
    {
        if (core != null)
        {
            core.Impactada += OnImpacted;
            core.Murio += OnDeath;
        }
        if (generator != null)
        {
            generator.Generada += OnGenerated;
        }
    }

    private void OnDisable()
    {
        if (core != null)
        {
            core.Impactada -= OnImpacted;
            core.Murio -= OnDeath;
        }
        if (generator != null)
        {
            generator.Generada -= OnGenerated;
        }
        if (reactionRoot != null)
        {
            reactionRoot.localRotation = baseRotation;
        }
    }

    private void Update()
    {
        if (core == null || animator == null)
        {
            return;
        }

        reactionTimer = Mathf.Max(0f, reactionTimer - Time.deltaTime);
        generationTimer = Mathf.Max(0f, generationTimer - Time.deltaTime);
        if (reactionRoot != null)
        {
            float hitAmount = reactionTimer > 0f
                ? Mathf.Sin((reactionTimer / duracionReaccion) * Mathf.PI) * 12f
                : 0f;
            reactionRoot.localRotation = baseRotation * Quaternion.Euler(0f, 0f, hitAmount);
        }

        if (previousState != core.Estado || generationTimer > 0f)
        {
            previousState = core.Estado;
            ApplyAnimationState();
        }
    }

    private void ApplyAnimationState()
    {
        bool moving = core.Estado == FallaState.Persiguiendo;
        bool activeAction = core.Estado == FallaState.Atacando || generationTimer > 0f;
        bool dead = core.Estado == FallaState.Muerta;

        SetBool(WalkHash, moving && movimiento == RobotFallaMovementAnimation.Walk && !dead);
        SetBool(RollHash, moving && movimiento == RobotFallaMovementAnimation.Roll && !dead);
        SetBool(OpenHash, activeAction && !dead);
    }

    private void OnImpacted(FallaCore owner, DamageInfo impact)
    {
        reactionTimer = duracionReaccion;
    }

    private void OnGenerated(FallaCore spawned)
    {
        generationTimer = duracionGeneracion;
        ApplyAnimationState();
    }

    private void OnDeath(FallaCore owner)
    {
        SetBool(WalkHash, false);
        SetBool(RollHash, false);
        SetBool(OpenHash, false);
    }

    private void SetBool(int hash, bool value)
    {
        if (availableParameters.Contains(hash))
        {
            animator.SetBool(hash, value);
        }
    }

    private void ValidateParameter(int hash, string parameterName)
    {
        if (!availableParameters.Contains(hash))
        {
            Debug.LogWarning(
                $"El Animator del RobotSphere no contiene el parametro '{parameterName}'.",
                this
            );
        }
    }
}

