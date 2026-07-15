using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(MutantStateController))]
public sealed class MutantMotor : MonoBehaviour
{
    [Header("Fuentes y referencias")]
    [Tooltip("Componente que implementa IMutantIntentSource. Puede sustituirse por una CPU.")]
    [SerializeField] private MonoBehaviour fuenteIntenciones;
    [SerializeField] private Transform camaraTransform;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private MutantStateController state;
    [SerializeField] private MutantAnimationController animationController;

    [Header("Movimiento mundial")]
    [SerializeField, Min(0f)] private float velocidadCaminar = 5.5f;
    [SerializeField, Min(0f)] private float velocidadCorrer = 9f;
    [SerializeField, Min(0f)] private float velocidadAgachado = 2.8f;
    [SerializeField, Min(0.01f)] private float aceleracion = 16f;
    [SerializeField, Min(0.01f)] private float desaceleracion = 22f;
    [SerializeField, Min(0.01f)] private float velocidadRotacion = 10f;

    [Header("Salto y gravedad")]
    [SerializeField, Min(0.01f)] private float alturaSalto = 2.2f;
    [SerializeField] private float gravedad = -30f;
    [SerializeField] private float fuerzaPegadoSuelo = -3f;
    [SerializeField] private float velocidadCaidaMaxima = -45f;
    [SerializeField] private float umbralCaida = -1.5f;

    [Header("Suelo")]
    [Tooltip("Radio mundial; no se multiplica por la escala del Transform.")]
    [SerializeField, Min(0.01f)] private float radioSuelo = 0.38f;
    [SerializeField] private LayerMask capasSuelo = ~0;

    [Header("Agacharse")]
    [SerializeField, Range(0.35f, 0.9f)] private float proporcionAlturaAgachado = 0.62f;
    [SerializeField, Min(0.01f)] private float velocidadCambioAltura = 3f;
    [SerializeField] private LayerMask capasObstruccion = ~0;

    private CharacterController characterController;
    private IMutantIntentSource intents;
    private float velocidadActual;
    private float velocidadVertical;
    private float alturaNormal;
    private float alturaAgachado;
    private Vector3 centroNormal;
    private Vector3 centroAgachado;
    private Vector3 direccionMundo;
    private bool running;

    public float VelocidadActual => velocidadActual;
    public float VelocidadVertical => velocidadVertical;
    public Vector3 DireccionMovimientoMundo => direccionMundo;
    public bool EstaCorriendo => running;
    public float VelocidadNormalizada => velocidadCorrer > 0.01f
        ? Mathf.Clamp01(velocidadActual / velocidadCorrer)
        : 0f;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        state ??= GetComponent<MutantStateController>();
        animationController ??= GetComponent<MutantAnimationController>();
        ResolveIntentSource();

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

    private void Update()
    {
        if (state == null || intents == null || state.IsDead)
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

        Vector3 velocity = direccionMundo * velocidadActual;
        velocity.y = velocidadVertical;
        characterController.Move(velocity * Time.deltaTime);
    }

    public void SetIntentSource(MonoBehaviour source)
    {
        fuenteIntenciones = source;
        ResolveIntentSource();
    }

    public void SetCameraTransform(Transform value)
    {
        camaraTransform = value;
    }

    private void ResolveIntentSource()
    {
        fuenteIntenciones ??= GetComponent<MutantInputReader>();
        intents = fuenteIntenciones as IMutantIntentSource;
        if (fuenteIntenciones != null && intents == null)
        {
            Debug.LogError(
                $"{fuenteIntenciones.GetType().Name} no implementa IMutantIntentSource.",
                this
            );
        }
    }

    private bool DetectGround()
    {
        Vector3 position = groundCheck != null
            ? groundCheck.position
            : transform.position + Vector3.up * characterController.skinWidth;

        return characterController.isGrounded || Physics.CheckSphere(
            position,
            radioSuelo,
            capasSuelo,
            QueryTriggerInteraction.Ignore
        );
    }

    private void UpdateGroundState(bool grounded)
    {
        if (grounded && velocidadVertical <= 0f)
        {
            bool landed = state.Locomotion == MutantLocomotionState.Falling ||
                          state.Locomotion == MutantLocomotionState.Jumping;
            state.SetLocomotion(
                landed ? MutantLocomotionState.Landing : MutantLocomotionState.Grounded
            );
            velocidadVertical = fuerzaPegadoSuelo;

            if (landed)
            {
                animationController?.NotifyLanding();
                state.SetLocomotion(MutantLocomotionState.Grounded);
            }
        }
        else if (!grounded && velocidadVertical <= umbralCaida)
        {
            state.SetLocomotion(MutantLocomotionState.Falling);
        }
    }

    private void HandleJump(bool grounded)
    {
        if (!intents.ConsumeJump() || !grounded || !state.CanJump)
        {
            return;
        }

        velocidadVertical = Mathf.Sqrt(alturaSalto * -2f * gravedad);
        state.SetLocomotion(MutantLocomotionState.Jumping);
        animationController?.NotifyJump();
    }

    private void HandleCrouch()
    {
        if (!intents.ConsumeCrouch() || !state.CanToggleCrouch)
        {
            return;
        }

        if (state.IsCrouching)
        {
            if (HasStandingClearance())
            {
                state.TrySetCrouching(false);
            }
        }
        else
        {
            state.TrySetCrouching(true);
        }
    }

    private void UpdateHorizontalMovement()
    {
        Vector2 moveInput = Vector2.ClampMagnitude(intents.Move, 1f);
        Vector3 forward = camaraTransform != null ? camaraTransform.forward : Vector3.forward;
        Vector3 right = camaraTransform != null ? camaraTransform.right : Vector3.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 desired = forward * moveInput.y + right * moveInput.x;
        float magnitude = Mathf.Clamp01(moveInput.magnitude);
        direccionMundo = desired.sqrMagnitude > 0.0001f ? desired.normalized : Vector3.zero;

        running = intents.SprintHeld && state.CanSprint && magnitude > 0.1f;
        float maxSpeed = state.IsCrouching
            ? velocidadAgachado
            : running ? velocidadCorrer : velocidadCaminar;
        float targetSpeed = state.MovementLocked ? 0f : maxSpeed * magnitude;
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

        velocidadVertical = Mathf.Max(
            velocidadVertical + gravedad * Time.deltaTime,
            velocidadCaidaMaxima
        );
    }

    private void UpdateControllerHeight()
    {
        float targetHeight = state.IsCrouching ? alturaAgachado : alturaNormal;
        Vector3 targetCenter = state.IsCrouching ? centroAgachado : centroNormal;
        float localStep = velocidadCambioAltura * Time.deltaTime;
        characterController.height = Mathf.MoveTowards(
            characterController.height,
            targetHeight,
            localStep
        );
        characterController.center = Vector3.MoveTowards(
            characterController.center,
            targetCenter,
            localStep
        );
    }

    private bool HasStandingClearance()
    {
        float worldScale = Mathf.Max(
            Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.z)
        );
        float radius = characterController.radius * 0.95f * worldScale;
        Vector3 currentTop = transform.TransformPoint(
            characterController.center + Vector3.up *
            (characterController.height * 0.5f - characterController.radius)
        );
        Vector3 standingTop = transform.TransformPoint(
            centroNormal + Vector3.up * (alturaNormal * 0.5f - characterController.radius)
        );

        return !Physics.CheckCapsule(
            currentTop,
            standingTop,
            radius,
            capasObstruccion,
            QueryTriggerInteraction.Ignore
        );
    }

    private void OnDrawGizmosSelected()
    {
        Transform check = groundCheck;
        Vector3 position = check != null ? check.position : transform.position;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(position, radioSuelo);
    }
}
