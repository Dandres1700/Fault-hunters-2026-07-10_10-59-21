using UnityEngine;

/// <summary>
/// Traduce los estados reales del Cazador a eventos de audio.
/// Se instala automaticamente, por lo que no necesita referencias manuales.
/// </summary>
[DisallowMultipleComponent]
public sealed class CazadorAudioController : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float intervaloPasoCaminando = 0.48f;
    [SerializeField, Min(0.1f)] private float intervaloPasoCorriendo = 0.31f;
    [SerializeField, Min(0f)] private float velocidadMinimaPaso = 0.55f;

    private CazadorController motor;
    private CazadorStateController state;
    private CazadorCombat combat;
    private CazadorStats stats;
    private float footstepTimer;
    private bool wasGrounded;
    private bool wasAttacking;
    private bool initialized;

    private void Awake()
    {
        motor = GetComponent<CazadorController>();
        state = GetComponent<CazadorStateController>();
        combat = GetComponent<CazadorCombat>();
        stats = GetComponent<CazadorStats>();
    }

    private void OnEnable()
    {
        if (stats != null)
        {
            stats.OnImpactoRecibido += OnImpactReceived;
            stats.OnMuerte += OnDeath;
        }
    }

    private void Start()
    {
        wasGrounded = state != null && state.IsGrounded;
        wasAttacking = combat != null && combat.EstaAtacando;
        initialized = true;
    }

    private void OnDisable()
    {
        if (stats != null)
        {
            stats.OnImpactoRecibido -= OnImpactReceived;
            stats.OnMuerte -= OnDeath;
        }
    }

    private void Update()
    {
        if (!initialized || state == null || state.IsDead)
        {
            return;
        }

        UpdateJumpAndLanding();
        UpdateAttack();
        UpdateFootsteps();
    }

    private void UpdateJumpAndLanding()
    {
        bool grounded = state.IsGrounded;
        if (wasGrounded && !grounded && motor != null && motor.VelocidadVertical > 0.1f)
        {
            GameAudioManager.Instance.PlayAt(GameSfx.PlayerJump, transform.position, 0.75f);
        }
        else if (!wasGrounded && grounded)
        {
            float landingVolume = motor != null
                ? Mathf.InverseLerp(1f, 18f, Mathf.Abs(motor.VelocidadVertical))
                : 0.7f;
            GameAudioManager.Instance.PlayAt(
                GameSfx.PlayerLand,
                transform.position,
                Mathf.Lerp(0.55f, 0.95f, landingVolume),
                0.96f,
                1.03f
            );
        }

        wasGrounded = grounded;
    }

    private void UpdateAttack()
    {
        bool attacking = combat != null && combat.EstaAtacando;
        if (attacking && !wasAttacking)
        {
            GameAudioManager.Instance.PlayAt(
                GameSfx.PlayerSwing,
                transform.position + transform.forward * 0.65f,
                0.82f,
                0.94f,
                1.07f
            );
        }

        wasAttacking = attacking;
    }

    private void UpdateFootsteps()
    {
        if (motor == null || !state.IsGrounded || state.MovementLocked ||
            motor.VelocidadActual < velocidadMinimaPaso)
        {
            footstepTimer = Mathf.Min(footstepTimer, 0.08f);
            return;
        }

        footstepTimer -= Time.deltaTime;
        if (footstepTimer > 0f)
        {
            return;
        }

        float speedFactor = Mathf.InverseLerp(
            velocidadMinimaPaso,
            Mathf.Max(velocidadMinimaPaso + 0.01f, motor.VelocidadMaximaActual),
            motor.VelocidadActual
        );
        float interval = Mathf.Lerp(
            intervaloPasoCaminando,
            intervaloPasoCorriendo,
            speedFactor
        );
        footstepTimer = interval;

        GameAudioManager.Instance.PlayAt(
            GameSfx.PlayerFootstep,
            transform.position,
            Mathf.Lerp(0.42f, 0.68f, speedFactor),
            0.92f,
            1.08f
        );
    }

    private void OnImpactReceived(DamageInfo impact)
    {
        Vector3 point = impact.PuntoImpacto == Vector3.zero
            ? transform.position
            : impact.PuntoImpacto;
        GameAudioManager.Instance.PlayAt(GameSfx.Impact, point, 0.9f, 0.94f, 1.05f);
        GameAudioManager.Instance.PlayAt(GameSfx.PlayerHurt, transform.position, 0.88f, 0.95f, 1.04f);
    }

    private void OnDeath()
    {
        GameAudioManager.Instance.PlayAt(GameSfx.PlayerDeath, transform.position, 1f, 0.98f, 1.02f);
    }
}
