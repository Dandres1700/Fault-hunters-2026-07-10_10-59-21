using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CazadorStats))]
public sealed class CazadorDeathController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private CazadorStats stats;
    [SerializeField] private CazadorStateController state;
    [SerializeField] private CazadorAnimationController animationController;
    [SerializeField] private CazadorInputReader input;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private CazadorController motor;
    [SerializeField] private CazadorCombat combat;
    [SerializeField] private HitboxAtaque hitbox;

    [Header("Eliminacion")]
    [Tooltip("Incluye la duracion completa del clip de muerte.")]
    [SerializeField, Min(0f)] private float tiempoVisible = 4f;
    [SerializeField] private bool desactivarAlFinal = true;

    private bool deathStarted;

    public event Action OnEliminado;

    private void Awake()
    {
        stats ??= GetComponent<CazadorStats>();
        state ??= GetComponent<CazadorStateController>();
        animationController ??= GetComponent<CazadorAnimationController>();
        input ??= GetComponent<CazadorInputReader>();
        playerInput ??= GetComponent<PlayerInput>();
        motor ??= GetComponent<CazadorController>();
        combat ??= GetComponent<CazadorCombat>();
        hitbox ??= GetComponentInChildren<HitboxAtaque>(true);
    }

    private void OnEnable()
    {
        if (stats != null)
        {
            stats.OnMuerte += BeginDeath;
        }
    }

    private void OnDisable()
    {
        if (stats != null)
        {
            stats.OnMuerte -= BeginDeath;
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
