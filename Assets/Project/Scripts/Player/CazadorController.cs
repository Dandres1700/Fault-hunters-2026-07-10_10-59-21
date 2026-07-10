using UnityEngine;

/// <summary>
/// Movimiento 3D del Cazador usando CharacterController.
/// Movimiento relativo a la camara (tipo action-adventure / arena boss fight).
/// Usa el Input System viejo (Input.GetAxis / Input.GetButtonDown).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class CazadorController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform camaraTransform;
    [SerializeField] private CazadorStats stats;
    [SerializeField] private Animator animator;

    [Header("Movimiento")]
    [SerializeField] private float velocidadCaminar = 4.5f;
    [SerializeField] private float velocidadRotacion = 12f;

    [Header("Dash / Esquive")]
    [SerializeField] private float velocidadDash = 14f;
    [SerializeField] private float duracionDash = 0.22f;
    [SerializeField] private float costoStaminaDash = 20f;
    [SerializeField] private float cooldownDash = 0.5f;

    [Header("Gravedad")]
    [SerializeField] private float gravedad = -25f;
    [SerializeField] private float velocidadCaidaMaxima = -30f;

    private CharacterController controller;
    private Vector3 velocidadVertical;
    private Vector3 direccionMovimientoActual;

    private bool estaDasheando;
    private float timerDash;
    private float timerCooldownDash;
    private Vector3 direccionDash;

    public bool PuedeActuar { get; private set; } = true; // combate puede bloquear esto
    public bool EstaDasheando => estaDasheando;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (stats == null) stats = GetComponent<CazadorStats>();
        if (camaraTransform == null && Camera.main != null) camaraTransform = Camera.main.transform;
    }

    private void Update()
    {
        if (!stats.EstaVivo) return;

        ActualizarTimers();

        if (estaDasheando)
        {
            EjecutarDash();
        }
        else
        {
            Vector3 inputMovimiento = LeerInputMovimiento();
            ManejarInicioDash(inputMovimiento);

            if (PuedeActuar)
            {
                MoverYRotar(inputMovimiento);
            }
        }

        AplicarGravedad();
        ActualizarAnimator();
    }

    private Vector3 LeerInputMovimiento()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 input = new Vector3(horizontal, 0f, vertical);
        if (input.sqrMagnitude > 1f) input.Normalize();

        return input;
    }

    private void MoverYRotar(Vector3 inputMovimiento)
    {
        Vector3 direccionCamaraForward = camaraTransform.forward;
        Vector3 direccionCamaraRight = camaraTransform.right;
        direccionCamaraForward.y = 0f;
        direccionCamaraRight.y = 0f;
        direccionCamaraForward.Normalize();
        direccionCamaraRight.Normalize();

        Vector3 direccionMundo = direccionCamaraForward * inputMovimiento.z + direccionCamaraRight * inputMovimiento.x;
        direccionMovimientoActual = direccionMundo;

        Vector3 movimientoHorizontal = direccionMundo * velocidadCaminar;
        controller.Move(movimientoHorizontal * Time.deltaTime);

        if (direccionMundo.sqrMagnitude > 0.001f)
        {
            Quaternion rotacionObjetivo = Quaternion.LookRotation(direccionMundo, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotacionObjetivo, velocidadRotacion * Time.deltaTime);
        }
    }

    private void ManejarInicioDash(Vector3 inputMovimiento)
    {
        bool botonDashPresionado = Input.GetButtonDown("Fire2"); // ajustar segun tu Input Manager (ej. bumper del PS4)

        if (!botonDashPresionado) return;
        if (timerCooldownDash > 0f) return;
        if (!PuedeActuar) return;
        if (!stats.IntentarGastarStamina(costoStaminaDash)) return;

        Vector3 direccion = inputMovimiento.sqrMagnitude > 0.01f
            ? TransformarInputADireccionMundo(inputMovimiento)
            : transform.forward;

        direccionDash = direccion.normalized;
        estaDasheando = true;
        timerDash = duracionDash;
    }

    private Vector3 TransformarInputADireccionMundo(Vector3 inputMovimiento)
    {
        Vector3 forward = camaraTransform.forward;
        Vector3 right = camaraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        return forward * inputMovimiento.z + right * inputMovimiento.x;
    }

    private void EjecutarDash()
    {
        controller.Move(direccionDash * velocidadDash * Time.deltaTime);
        timerDash -= Time.deltaTime;

        if (timerDash <= 0f)
        {
            estaDasheando = false;
            timerCooldownDash = cooldownDash;
        }
    }

    private void ActualizarTimers()
    {
        if (timerCooldownDash > 0f)
        {
            timerCooldownDash -= Time.deltaTime;
        }
    }

    private void AplicarGravedad()
    {
        if (controller.isGrounded && velocidadVertical.y < 0f)
        {
            velocidadVertical.y = -2f; // pequeno valor negativo para mantenerlo "pegado" al piso
        }

        velocidadVertical.y += gravedad * Time.deltaTime;
        velocidadVertical.y = Mathf.Max(velocidadVertical.y, velocidadCaidaMaxima);

        controller.Move(velocidadVertical * Time.deltaTime);
    }

    private void ActualizarAnimator()
    {
        if (animator == null) return;

        float velocidadNormalizada = new Vector2(direccionMovimientoActual.x, direccionMovimientoActual.z).magnitude;
        animator.SetFloat("Velocidad", velocidadNormalizada);
        animator.SetBool("Dasheando", estaDasheando);
    }

    /// <summary>
    /// Usado por CazadorCombat para bloquear movimiento durante ataques.
    /// </summary>
    public void SetPuedeActuar(bool valor)
    {
        PuedeActuar = valor;
    }
}