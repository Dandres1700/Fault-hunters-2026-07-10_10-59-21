using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CazadorInputReader))]
[RequireComponent(typeof(CazadorStateController))]
public sealed class CazadorController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform camaraTransform;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private CazadorInputReader input;
    [SerializeField] private CazadorStateController state;
    [SerializeField] private CazadorStats stats;
    [SerializeField] private CazadorAnimationController animationController;

    [Header("Movimiento")]
    [SerializeField, Min(0f)] private float velocidadCaminar = 4.5f;
    [SerializeField, Min(0f)] private float velocidadCorrer = 7.5f;
    [SerializeField, Min(0f)] private float velocidadAgachado = 2.25f;
    [SerializeField, Min(0.01f)] private float aceleracion = 18f;
    [SerializeField, Min(0.01f)] private float desaceleracion = 24f;
    [SerializeField, Min(0.01f)] private float velocidadRotacion = 14f;

    [Header("Salto y gravedad")]
    [SerializeField, Min(0.01f)] private float alturaSalto = 1.4f;
    [SerializeField] private float gravedad = -25f;
    [SerializeField] private float fuerzaPegadoSuelo = -2f;
    [SerializeField] private float velocidadCaidaMaxima = -35f;
    [SerializeField] private float umbralCaida = -1.5f;

    [Header("Suelo")]
    [SerializeField, Min(0.01f)] private float radioSuelo = 0.24f;
    [SerializeField] private LayerMask capasSuelo = ~0;

    [Header("Agacharse")]
    [SerializeField, Range(0.35f, 0.9f)] private float proporcionAlturaAgachado = 0.6f;
    [SerializeField, Min(0.01f)] private float velocidadCambioAltura = 8f;
    [SerializeField] private LayerMask capasObstruccion = ~0;

    private CharacterController characterController;
    private float velocidadActual;
    private float velocidadVertical;
    private float alturaNormal;
    private float alturaAgachado;
    private Vector3 centroNormal;
    private Vector3 centroAgachado;
    private Vector3 direccionMundo;
    private bool running;

    public bool PuedeActuar => state != null && !state.MovementLocked;
    public bool EstaDasheando => state != null &&
                                 state.Action == EstadoAccionCazador.Dodging;
    public float VelocidadActual => velocidadActual;
    public float VelocidadMaximaActual { get; private set; }
    public float VelocidadNormalizada => velocidadCorrer > 0.01f
        ? Mathf.Clamp01(velocidadActual / velocidadCorrer)
        : 0f;
    public float VelocidadVertical => velocidadVertical;
    public Vector3 DireccionMovimientoMundo => direccionMundo;
    public bool EstaCorriendo => running;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        input ??= GetComponent<CazadorInputReader>();
        state ??= GetComponent<CazadorStateController>();
        stats ??= GetComponent<CazadorStats>();
        animationController ??= GetComponent<CazadorAnimationController>();

        if (camaraTransform == null && Camera.main != null)
        {
            camaraTransform = Camera.main.transform;
        }

        alturaNormal = characterController.height;
        centroNormal = characterController.center;
        alturaAgachado = Mathf.Max(
            characterController.radius * 2f,
            alturaNormal * proporcionAlturaAgachado
        );
        float bottom = centroNormal.y - alturaNormal * 0.5f;
        centroAgachado = new Vector3(
            centroNormal.x,
            bottom + alturaAgachado * 0.5f,
            centroNormal.z
        );
    }

    private void OnEnable()
    {
        if (stats != null)
        {
            stats.OnMuerte += OnDeath;
        }
    }

    private void OnDisable()
    {
        if (stats != null)
        {
            stats.OnMuerte -= OnDeath;
        }
    }

    private void Update()
    {
        if (state == null || input == null || state.IsDead)
        {
            return;
        }

        bool grounded = DetectGround();
        UpdateGroundState(grounded);
        HandleCrouch();
        HandleJump(grounded);
        UpdateHorizontalMovement();
        UpdateVerticalMovement(grounded);
        UpdateControllerHeight();

        Vector3 displacement = direccionMundo * velocidadActual;
        displacement.y = velocidadVertical;
        characterController.Move(displacement * Time.deltaTime);
    }

    private bool DetectGround()
    {
        Vector3 checkPosition = groundCheck != null
            ? groundCheck.position
            : transform.position + Vector3.up * characterController.skinWidth;

        return characterController.isGrounded || Physics.CheckSphere(
            checkPosition,
            radioSuelo,
            capasSuelo,
            QueryTriggerInteraction.Ignore
        );
    }

    private void UpdateGroundState(bool grounded)
    {
        if (grounded && velocidadVertical <= 0f)
        {
            bool landed = state.Locomotion == EstadoLocomocionCazador.Falling ||
                          state.Locomotion == EstadoLocomocionCazador.Jumping;
            state.SetLocomotion(EstadoLocomocionCazador.Grounded);
            velocidadVertical = fuerzaPegadoSuelo;

            if (landed)
            {
                animationController?.NotifyLanding();
            }
        }
        else if (!grounded && velocidadVertical <= umbralCaida)
        {
            state.SetLocomotion(EstadoLocomocionCazador.Falling);
        }
    }

    private void HandleJump(bool grounded)
    {
        if (!input.ConsumeJump() || !grounded || !state.CanJump)
        {
            return;
        }

        velocidadVertical = Mathf.Sqrt(alturaSalto * -2f * gravedad);
        state.SetLocomotion(EstadoLocomocionCazador.Jumping);
        animationController?.NotifyJump();
    }

    private void HandleCrouch()
    {
        if (!input.ConsumeCrouch() || !state.CanToggleCrouch)
        {
            return;
        }

        if (state.IsCrouching)
        {
            if (HasStandingClearance())
            {
                state.TrySetCrouching(false);
            }

            return;
        }

        state.TrySetCrouching(true);
    }

    private void UpdateHorizontalMovement()
    {
        Vector2 moveInput = Vector2.ClampMagnitude(input.Move, 1f);
        Vector3 forward = camaraTransform != null ? camaraTransform.forward : Vector3.forward;
        Vector3 right = camaraTransform != null ? camaraTransform.right : Vector3.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 desiredDirection = forward * moveInput.y + right * moveInput.x;
        float inputMagnitude = Mathf.Clamp01(moveInput.magnitude);
        direccionMundo = desiredDirection.sqrMagnitude > 0.0001f
            ? desiredDirection.normalized
            : Vector3.zero;

        running = input.SprintHeld && state.CanSprint && inputMagnitude > 0.1f;
        VelocidadMaximaActual = state.IsCrouching
            ? velocidadAgachado
            : running ? velocidadCorrer : velocidadCaminar;

        float targetSpeed = state.MovementLocked
            ? 0f
            : VelocidadMaximaActual * inputMagnitude;
        float rate = targetSpeed > velocidadActual ? aceleracion : desaceleracion;
        velocidadActual = Mathf.MoveTowards(
            velocidadActual,
            targetSpeed,
            rate * Time.deltaTime
        );

        if (!state.MovementLocked && direccionMundo.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direccionMundo, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                1f - Mathf.Exp(-velocidadRotacion * Time.deltaTime)
            );
        }
    }

    private void UpdateVerticalMovement(bool grounded)
    {
        if (grounded && velocidadVertical < 0f)
        {
            velocidadVertical = fuerzaPegadoSuelo;
            return;
        }

        velocidadVertical += gravedad * Time.deltaTime;
        velocidadVertical = Mathf.Max(velocidadVertical, velocidadCaidaMaxima);
    }

    private void UpdateControllerHeight()
    {
        float targetHeight = state.IsCrouching ? alturaAgachado : alturaNormal;
        Vector3 targetCenter = state.IsCrouching ? centroAgachado : centroNormal;
        float step = velocidadCambioAltura * Time.deltaTime;

        characterController.height = Mathf.MoveTowards(
            characterController.height,
            targetHeight,
            step
        );
        characterController.center = Vector3.MoveTowards(
            characterController.center,
            targetCenter,
            step
        );
    }

    private bool HasStandingClearance()
    {
        float radius = characterController.radius * 0.95f;
        Vector3 currentTop = transform.TransformPoint(
            characterController.center + Vector3.up *
            (characterController.height * 0.5f - radius)
        );
        Vector3 standingTop = transform.TransformPoint(
            centroNormal + Vector3.up * (alturaNormal * 0.5f - radius)
        );

        return !Physics.CheckCapsule(
            currentTop,
            standingTop,
            radius,
            capasObstruccion,
            QueryTriggerInteraction.Ignore
        );
    }

    private void OnDeath()
    {
        state?.SetDead();
        velocidadActual = 0f;
    }

    public void SetPuedeActuar(bool value)
    {
        if (value)
        {
            state?.EndAttack();
        }
        else
        {
            state?.TryBeginAttack(true);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 position = groundCheck != null
            ? groundCheck.position
            : transform.position + Vector3.up * 0.05f;
        Gizmos.DrawWireSphere(position, radioSuelo);
    }
}
