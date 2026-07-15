using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(MutantStats))]
public sealed class MutantDeathController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private MutantStats stats;
    [SerializeField] private MutantStateController state;
    [SerializeField] private MutantAnimationController animationController;
    [SerializeField] private MutantInputReader input;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private MutantMotor motor;
    [SerializeField] private MutantCombat combat;
    [SerializeField] private MutantAttackHitbox hitbox;
    [Tooltip("Componentes de CPU futuros que deben detenerse al morir.")]
    [SerializeField] private Behaviour[] controladoresCpu;

    [Header("Eliminacion")]
    [Tooltip("Incluye la duracion completa del clip de muerte.")]
    [SerializeField, Min(0f)] private float tiempoVisible = 4f;
    [SerializeField] private bool desactivarAlFinal = true;

    private CharacterController characterController;
    private NavMeshAgent[] navMeshAgents;
    private bool deathStarted;

    public event Action OnEliminado;

    private void Awake()
    {
        stats ??= GetComponent<MutantStats>();
        state ??= GetComponent<MutantStateController>();
        animationController ??= GetComponent<MutantAnimationController>();
        input ??= GetComponent<MutantInputReader>();
        playerInput ??= GetComponent<PlayerInput>();
        motor ??= GetComponent<MutantMotor>();
        combat ??= GetComponent<MutantCombat>();
        hitbox ??= GetComponentInChildren<MutantAttackHitbox>(true);
        characterController = GetComponent<CharacterController>();
        navMeshAgents = GetComponents<NavMeshAgent>();
    }

    private void OnEnable()
    {
        if (stats != null)
        {
            stats.OnDeath += BeginDeath;
        }
    }

    private void OnDisable()
    {
        if (stats != null)
        {
            stats.OnDeath -= BeginDeath;
        }
    }

    private void BeginDeath()
    {
        if (deathStarted)
        {
            return;
        }

        deathStarted = true;
        state?.SetDead();
        hitbox?.Desactivar();
        animationController?.NotifyDeath();

        if (combat != null) combat.enabled = false;
        if (motor != null) motor.enabled = false;
        if (input != null) input.enabled = false;
        if (playerInput != null) playerInput.enabled = false;
        if (characterController != null) characterController.enabled = false;

        foreach (NavMeshAgent agent in navMeshAgents)
        {
            if (agent != null) agent.enabled = false;
        }

        if (controladoresCpu != null)
        {
            foreach (Behaviour controller in controladoresCpu)
            {
                if (controller != null) controller.enabled = false;
            }
        }

        StartCoroutine(EliminationRoutine());
    }

    private IEnumerator EliminationRoutine()
    {
        yield return new WaitForSeconds(tiempoVisible);
        OnEliminado?.Invoke();
        if (desactivarAlFinal)
        {
            gameObject.SetActive(false);
        }
    }
}
