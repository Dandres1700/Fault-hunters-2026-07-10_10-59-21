using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public sealed class FallaCore : MonoBehaviour, IRecibeImpacto
{
    [Header("Configuracion")]
    [SerializeField] private FallaConfiguration configuracion;
    [SerializeField] private LayerMask capasObjetivo = 1 << 8;
    [SerializeField] private LayerMask capasObstaculo = ~((1 << 8) | (1 << 10));

    [Header("Referencias")]
    [SerializeField] private Transform objetivoInicial;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform nucleo;
    [SerializeField] private FallaVisualController visualController;
    [SerializeField] private ParticleSystem particulasMuerte;
    [SerializeField] private AudioSource audioSource;

    [Header("Audio opcional")]
    [SerializeField] private AudioClip sonidoAparicion;
    [SerializeField] private AudioClip sonidoDeteccion;
    [SerializeField] private AudioClip sonidoImpacto;
    [SerializeField] private AudioClip sonidoMuerte;

    private readonly Collider[] detectionResults = new Collider[24];
    private Rigidbody body;
    private Collider physicalCollider;
    private IFallaAttack attack;
    private Transform target;
    private float health;
    private float invulnerabilityTimer;
    private float detectionTimer;
    private float attackCooldownTimer;
    private float powerMultiplier = 1f;
    private bool externallyRevealed;
    private bool removedNotified;

    public FallaConfiguration Configuracion => configuracion;
    public FallaType Tipo => configuracion != null ? configuracion.Tipo : FallaType.Rastrera;
    public FallaState Estado { get; private set; } = FallaState.Inactiva;
    public float VidaActual => health;
    public float VidaMaxima => configuracion != null ? configuracion.VidaMaxima : 1f;
    public bool EstaViva => Estado != FallaState.Muerta && health > 0f;
    public Transform Objetivo => target;
    public UnityEngine.Object IdentidadImpacto => this;
    public float DanoActual => configuracion != null
        ? configuracion.Dano * powerMultiplier
        : 0f;

    public event Action<FallaCore> Detectada;
    public event Action<FallaCore, DamageInfo> Impactada;
    public event Action<FallaCore> Murio;
    public event Action<FallaCore> Removida;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        physicalCollider = GetComponent<Collider>();
        visualController ??= GetComponentInChildren<FallaVisualController>(true);
        audioSource ??= GetComponent<AudioSource>();
        attack = FindAttack();

        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationZ |
                           RigidbodyConstraints.FreezePositionY;

        if (physicalCollider.isTrigger)
        {
            Debug.LogWarning("El collider fisico de una Falla no debe ser trigger; se corrigio.", this);
            physicalCollider.isTrigger = false;
        }
    }

    private void OnEnable()
    {
        if (configuracion == null)
        {
            Debug.LogError("FallaCore necesita una FallaConfiguration.", this);
            enabled = false;
            return;
        }

        health = configuracion.VidaMaxima;
        invulnerabilityTimer = 0f;
        detectionTimer = UnityEngine.Random.Range(0f, configuracion.IntervaloDeteccion);
        attackCooldownTimer = 0f;
        powerMultiplier = 1f;
        removedNotified = false;
        if (physicalCollider != null)
        {
            physicalCollider.enabled = true;
        }
        Estado = FallaState.Inactiva;
        target = objetivoInicial;
        externallyRevealed = false;
        visualController?.Initialize(configuracion);
        UpdateCoreVisibility();
        PlayOptional(sonidoAparicion);
    }

    private void Update()
    {
        if (!EstaViva)
        {
            return;
        }

        invulnerabilityTimer = Mathf.Max(0f, invulnerabilityTimer - Time.deltaTime);
        attackCooldownTimer = Mathf.Max(0f, attackCooldownTimer - Time.deltaTime);

        if (Estado == FallaState.Atacando)
        {
            if (attack == null || !attack.IsRunning)
            {
                attackCooldownTimer = configuracion.CooldownAtaque;
                Estado = target != null ? FallaState.Persiguiendo : FallaState.Inactiva;
                visualController?.SetAttacking(false);
                UpdateCoreVisibility();
            }
            return;
        }

        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0f)
        {
            detectionTimer = configuracion.IntervaloDeteccion;
            RefreshTarget();
        }

        if (target == null)
        {
            Estado = FallaState.Inactiva;
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (distance > configuracion.RangoDeteccion * 1.25f)
        {
            target = null;
            Estado = FallaState.Inactiva;
            return;
        }

        if (attack != null && distance <= configuracion.RangoAtaque &&
            attackCooldownTimer <= 0f)
        {
            Estado = FallaState.Atacando;
            visualController?.SetAttacking(true);
            UpdateCoreVisibility();
            attack.BeginAttack(this, target);
            return;
        }

        Estado = FallaState.Persiguiendo;
        MoveTowardsTarget(toTarget, distance);
    }

    private IFallaAttack FindAttack()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IFallaAttack candidate)
            {
                return candidate;
            }
        }
        return null;
    }

    private void RefreshTarget()
    {
        if (target != null)
        {
            IRecibeImpacto receiver = target.GetComponentInParent<IRecibeImpacto>();
            if (receiver != null)
            {
                return;
            }
            target = null;
        }

        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            configuracion.RangoDeteccion,
            detectionResults,
            capasObjetivo,
            QueryTriggerInteraction.Collide
        );

        float nearestSqr = float.PositiveInfinity;
        Transform nearest = null;
        for (int index = 0; index < count; index++)
        {
            Collider candidate = detectionResults[index];
            detectionResults[index] = null;
            if (candidate == null || candidate.GetComponentInParent<IRecibeImpacto>() == null)
            {
                continue;
            }

            float sqrDistance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance < nearestSqr)
            {
                nearestSqr = sqrDistance;
                nearest = candidate.transform;
            }
        }

        if (nearest == null)
        {
            return;
        }

        target = nearest;
        Estado = FallaState.Alerta;
        visualController?.SetAlerted(true);
        UpdateCoreVisibility();
        PlayOptional(sonidoDeteccion);
        Detectada?.Invoke(this);
    }

    private void MoveTowardsTarget(Vector3 direction, float distance)
    {
        if (configuracion.Velocidad <= 0f || distance <= configuracion.DistanciaMinimaObjetivo ||
            direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 normalized = direction / distance;
        Quaternion desiredRotation = Quaternion.LookRotation(normalized, Vector3.up);
        Quaternion nextRotation = Quaternion.Slerp(
            body.rotation,
            desiredRotation,
            1f - Mathf.Exp(-configuracion.VelocidadRotacion * Time.deltaTime)
        );

        float radius = Mathf.Max(0.1f, physicalCollider.bounds.extents.x * 0.75f);
        float step = Mathf.Min(configuracion.Velocidad * Time.deltaTime,
            distance - configuracion.DistanciaMinimaObjetivo);
        bool blocked = Physics.SphereCast(
            transform.position + Vector3.up * radius,
            radius,
            normalized,
            out _,
            Mathf.Max(0f, step),
            capasObstaculo,
            QueryTriggerInteraction.Ignore
        );

        body.MoveRotation(nextRotation);
        if (!blocked && step > 0f)
        {
            body.MovePosition(body.position + normalized * step);
        }
    }

    public void SetTarget(Transform value, bool forceAlert = true)
    {
        target = value;
        if (target != null && forceAlert && EstaViva)
        {
            Estado = FallaState.Alerta;
            visualController?.SetAlerted(true);
            UpdateCoreVisibility();
        }
    }

    public void RevealCore(bool value)
    {
        externallyRevealed = value;
        UpdateCoreVisibility();
    }

    public void ApplyPowerMultiplier(float value)
    {
        powerMultiplier = Mathf.Clamp(value, 0.1f, 5f);
        visualController?.SetPowerMultiplier(powerMultiplier);
    }

    public void RecibirDano(float cantidad)
    {
        RecibirImpacto(new DamageInfo(cantidad, transform.position, Vector3.zero, null));
    }

    public bool RecibirImpacto(DamageInfo impacto)
    {
        if (!EstaViva || invulnerabilityTimer > 0f || impacto.Cantidad <= 0f)
        {
            return false;
        }

        health = Mathf.Max(0f, health - impacto.Cantidad);
        invulnerabilityTimer = configuracion.InvulnerabilidadTrasImpacto;
        Estado = health <= 0f ? FallaState.Muerta : FallaState.Herida;
        visualController?.PlayHit();
        PlayOptional(sonidoImpacto);
        Impactada?.Invoke(this, impacto);

        if (health <= 0f)
        {
            BeginDeath();
        }
        else
        {
            if (impacto.Fuente != null)
            {
                SetTarget(impacto.Fuente.transform);
            }
            UpdateCoreVisibility();
        }
        return true;
    }

    public void KillImmediately()
    {
        if (!EstaViva)
        {
            return;
        }
        health = 0f;
        Estado = FallaState.Muerta;
        BeginDeath();
    }

    private void BeginDeath()
    {
        attack?.CancelAttack();
        visualController?.SetAttacking(false);
        physicalCollider.enabled = false;
        particulasMuerte?.Play(true);
        PlayOptional(sonidoMuerte);
        Murio?.Invoke(this);
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        float duration = configuracion.TiempoDesaparicion;
        Vector3 initialScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.Lerp(initialScale, Vector3.zero,
                    Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration)));
            }
            yield return null;
        }

        NotifyRemoved();
        if (configuracion.DesactivarAlMorir)
        {
            gameObject.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void UpdateCoreVisibility()
    {
        if (nucleo == null || configuracion == null)
        {
            return;
        }

        bool visible = configuracion.VisibilidadNucleo switch
        {
            FallaCoreVisibility.SiempreVisible => true,
            FallaCoreVisibility.DuranteAtaque => Estado == FallaState.Atacando,
            FallaCoreVisibility.AlRecibirDano => Estado == FallaState.Herida,
            FallaCoreVisibility.TrasDeteccion => target != null,
            FallaCoreVisibility.ReveladoExterno => externallyRevealed,
            _ => false
        };
        nucleo.gameObject.SetActive(visible);
    }

    private void PlayOptional(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private void NotifyRemoved()
    {
        if (removedNotified)
        {
            return;
        }
        removedNotified = true;
        Removida?.Invoke(this);
    }

    private void OnDisable()
    {
        attack?.CancelAttack();
        if (Estado == FallaState.Muerta)
        {
            NotifyRemoved();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (configuracion == null)
        {
            return;
        }
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, configuracion.RangoDeteccion);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, configuracion.RangoAtaque);
    }
}
