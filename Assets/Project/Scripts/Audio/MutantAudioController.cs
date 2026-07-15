using UnityEngine;

/// <summary>
/// Audio reactivo del Mutante/Falla: pasos pesados, ataques, dano, rugido y muerte.
/// </summary>
[DisallowMultipleComponent]
public sealed class MutantAudioController : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float intervaloPaso = 0.58f;
    [SerializeField, Min(0f)] private float velocidadMinimaPaso = 0.7f;
    [SerializeField] private bool rugirAlAparecer = true;

    private MutantMotor motor;
    private MutantStateController state;
    private MutantCombat combat;
    private MutantStats stats;
    private float footstepTimer;
    private bool wasAttacking;
    private bool started;

    private void Awake()
    {
        motor = GetComponent<MutantMotor>();
        state = GetComponent<MutantStateController>();
        combat = GetComponent<MutantCombat>();
        stats = GetComponent<MutantStats>();
    }

    private void OnEnable()
    {
        if (stats != null)
        {
            stats.ImpactoRecibido += OnImpactReceived;
            stats.OnDeath += OnDeath;
        }
    }

    private void Start()
    {
        wasAttacking = combat != null && combat.EstaAtacando;
        started = true;
        if (rugirAlAparecer)
        {
            GameAudioManager.Instance.PlayAt(GameSfx.BossRoar, transform.position, 0.95f, 0.96f, 1.02f);
        }
    }

    private void OnDisable()
    {
        if (stats != null)
        {
            stats.ImpactoRecibido -= OnImpactReceived;
            stats.OnDeath -= OnDeath;
        }
    }

    private void Update()
    {
        if (!started || state == null || state.IsDead)
        {
            return;
        }

        UpdateAttack();
        UpdateFootsteps();
    }

    private void UpdateAttack()
    {
        bool attacking = combat != null && combat.EstaAtacando;
        if (attacking && !wasAttacking)
        {
            GameAudioManager.Instance.PlayAt(
                GameSfx.BossAttack,
                transform.position + transform.forward,
                1f,
                0.9f,
                1.04f
            );
        }

        wasAttacking = attacking;
    }

    private void UpdateFootsteps()
    {
        if (motor == null || !state.IsGrounded || state.MovementLocked ||
            motor.VelocidadActual < velocidadMinimaPaso)
        {
            footstepTimer = Mathf.Min(footstepTimer, 0.1f);
            return;
        }

        footstepTimer -= Time.deltaTime;
        if (footstepTimer > 0f)
        {
            return;
        }

        float speed = Mathf.Clamp01(motor.VelocidadNormalizada);
        footstepTimer = Mathf.Lerp(intervaloPaso, intervaloPaso * 0.68f, speed);
        GameAudioManager.Instance.PlayAt(
            GameSfx.BossFootstep,
            transform.position,
            Mathf.Lerp(0.65f, 0.95f, speed),
            0.84f,
            0.98f
        );
    }

    private void OnImpactReceived(DamageInfo impact)
    {
        Vector3 point = impact.PuntoImpacto == Vector3.zero
            ? transform.position
            : impact.PuntoImpacto;
        GameAudioManager.Instance.PlayAt(GameSfx.Impact, point, 1f, 0.82f, 0.98f);
        GameAudioManager.Instance.PlayAt(GameSfx.BossHurt, transform.position, 0.92f, 0.92f, 1.03f);
    }

    private void OnDeath()
    {
        GameAudioManager.Instance.PlayAt(GameSfx.BossDeath, transform.position, 1f, 0.92f, 1f);
        GameAudioManager.Instance.PlayAt(GameSfx.BossDefeated, transform.position, 0.95f, 0.98f, 1.02f);
    }
}
