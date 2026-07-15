using UnityEngine;

public enum MutantEnemyAIState
{
    Idle,
    Detect,
    Chase,
    Attack,
    Cooldown,
    Hurt,
    Dead
}

[DisallowMultipleComponent]
[RequireComponent(typeof(MutantStateController))]
[RequireComponent(typeof(MutantStats))]
public sealed class MutantEnemyIntentSource : MonoBehaviour, IMutantIntentSource
{
    [Header("Objetivo")]
    [SerializeField] private CazadorStats objetivo;
    [SerializeField] private LayerMask capaCazador = 1 << 8;
    [SerializeField, Min(1f)] private float rangoDeteccion = 28f;
    [SerializeField, Min(0.05f)] private float intervaloDeteccion = 0.25f;

    [Header("Combate")]
    [Tooltip("Ajustada al radio 1.2 + desplazamiento 0.65 del hitbox existente.")]
    [SerializeField, Min(0.5f)] private float distanciaDetencion = 1.55f;
    [SerializeField, Min(0.5f)] private float rangoAtaque = 1.9f;
    [SerializeField, Min(0.1f)] private float cooldownAtaque = 3.4f;
    [SerializeField, Min(0f)] private float pausaAlRecibirDano = 0.18f;
    [SerializeField, Min(0f)] private float velocidadGiroEnAtaque = 8f;

    [Header("Navegacion simple")]
    [SerializeField] private LayerMask capasObstaculo = ~((1 << 8) | (1 << 9) | (1 << 10));
    [SerializeField, Min(0.1f)] private float radioEvasion = 0.55f;
    [SerializeField, Min(0.2f)] private float distanciaEvasion = 1.6f;
    [SerializeField] private MutantStateController state;
    [SerializeField] private MutantStats stats;

    private readonly Collider[] detectionResults = new Collider[16];
    private float detectionTimer;
    private float attackCooldownTimer;
    private float hurtTimer;
    private bool attackRequested;
    // El mapa mundial coloca a los enemigos lejos al iniciar. En ese caso el
    // objetivo se asigna desde la escena y no debe perderse por el rango de
    // deteccion antes de que el Mutant haya podido acercarse.
    private bool objetivoAsignadoExternamente;

    public MutantEnemyAIState Estado { get; private set; } = MutantEnemyAIState.Idle;
    public Vector2 Move { get; private set; }
    public Vector2 Look => Vector2.zero;
    public bool SprintHeld { get; private set; }
    public bool IsUsingGamepad => false;
    public CazadorStats Objetivo => objetivo;

    private void Awake()
    {
        state ??= GetComponent<MutantStateController>();
        stats ??= GetComponent<MutantStats>();
    }

    private void OnEnable()
    {
        detectionTimer = 0f;
        attackCooldownTimer = 0f;
        hurtTimer = 0f;
        attackRequested = false;
        if (stats != null)
        {
            stats.DanoRecibido += OnHurt;
            stats.OnDeath += OnDeath;
        }
    }

    private void OnDisable()
    {
        if (stats != null)
        {
            stats.DanoRecibido -= OnHurt;
            stats.OnDeath -= OnDeath;
        }
        ClearIntent();
    }

    private void Update()
    {
        attackCooldownTimer = Mathf.Max(0f, attackCooldownTimer - Time.deltaTime);
        hurtTimer = Mathf.Max(0f, hurtTimer - Time.deltaTime);

        if (state == null || stats == null || state.IsDead || !stats.EstaVivo)
        {
            Estado = MutantEnemyAIState.Dead;
            ClearIntent();
            return;
        }

        detectionTimer -= Time.deltaTime;
        if (detectionTimer <= 0f)
        {
            detectionTimer = intervaloDeteccion;
            RefreshTarget();
        }

        if (objetivo == null || !objetivo.EstaVivo)
        {
            Estado = MutantEnemyAIState.Idle;
            ClearIntent();
            return;
        }

        Vector3 toTarget = objetivo.transform.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (!objetivoAsignadoExternamente && distance > rangoDeteccion * 1.2f)
        {
            objetivo = null;
            Estado = MutantEnemyAIState.Idle;
            ClearIntent();
            return;
        }

        if (hurtTimer > 0f)
        {
            Estado = MutantEnemyAIState.Hurt;
            ClearIntent();
            FaceTarget(toTarget);
            return;
        }

        if (distance > distanciaDetencion)
        {
            Estado = MutantEnemyAIState.Chase;
            Vector3 direction = ResolveMovementDirection(toTarget.normalized);
            Move = new Vector2(direction.x, direction.z);
            SprintHeld = distance > rangoAtaque * 2f;
            return;
        }

        ClearMovement();
        FaceTarget(toTarget);
        if (distance <= rangoAtaque && attackCooldownTimer <= 0f && state.CanAttack)
        {
            Estado = MutantEnemyAIState.Attack;
            attackRequested = true;
            attackCooldownTimer = cooldownAtaque;
        }
        else
        {
            Estado = MutantEnemyAIState.Cooldown;
        }
    }

    public void SetTarget(CazadorStats value)
    {
        objetivo = value;
        objetivoAsignadoExternamente = value != null;
        detectionTimer = intervaloDeteccion;
        if (objetivo != null)
        {
            Estado = MutantEnemyAIState.Detect;
        }
    }

    public bool ConsumeAttack()
    {
        bool value = attackRequested;
        attackRequested = false;
        return value;
    }

    public bool ConsumeJump() => false;
    public bool ConsumeCrouch() => false;

    private void RefreshTarget()
    {
        if (objetivo != null && objetivo.EstaVivo)
        {
            return;
        }

        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            rangoDeteccion,
            detectionResults,
            capaCazador,
            QueryTriggerInteraction.Collide
        );
        float nearestSqr = float.PositiveInfinity;
        CazadorStats nearest = null;
        for (int index = 0; index < count; index++)
        {
            Collider candidate = detectionResults[index];
            detectionResults[index] = null;
            if (!CombatTargeting.TryGetCazador(candidate, out IRecibeImpacto receiver))
            {
                continue;
            }
            CazadorStats statsCandidate = CombatTargeting.GetCazadorStats(receiver);
            if (statsCandidate == null || !statsCandidate.EstaVivo)
            {
                continue;
            }
            float sqrDistance = (statsCandidate.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance < nearestSqr)
            {
                nearestSqr = sqrDistance;
                nearest = statsCandidate;
            }
        }
        if (nearest != null)
        {
            objetivo = nearest;
            Estado = MutantEnemyAIState.Detect;
        }
    }

    private Vector3 ResolveMovementDirection(Vector3 desired)
    {
        Vector3 origin = transform.position + Vector3.up * 1.1f;
        if (!Physics.SphereCast(origin, radioEvasion, desired, out _, distanciaEvasion,
                capasObstaculo, QueryTriggerInteraction.Ignore))
        {
            return desired;
        }

        Vector3 right = Vector3.Cross(Vector3.up, desired).normalized;
        if (!Physics.SphereCast(origin, radioEvasion, right, out _, distanciaEvasion,
                capasObstaculo, QueryTriggerInteraction.Ignore))
        {
            return right;
        }
        return -right;
    }

    private void FaceTarget(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f || velocidadGiroEnAtaque <= 0f)
        {
            return;
        }
        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            1f - Mathf.Exp(-velocidadGiroEnAtaque * Time.deltaTime)
        );
    }

    private void OnHurt(float amount)
    {
        if (amount > 0f)
        {
            hurtTimer = pausaAlRecibirDano;
            Estado = MutantEnemyAIState.Hurt;
        }
    }

    private void OnDeath()
    {
        Estado = MutantEnemyAIState.Dead;
        ClearIntent();
        enabled = false;
    }

    private void ClearMovement()
    {
        Move = Vector2.zero;
        SprintHeld = false;
    }

    private void ClearIntent()
    {
        ClearMovement();
        attackRequested = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, rangoDeteccion);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, rangoAtaque);
    }
}
