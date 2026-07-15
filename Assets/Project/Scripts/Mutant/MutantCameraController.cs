using UnityEngine;

[DisallowMultipleComponent]
public sealed class MutantCameraController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform target;
    [SerializeField] private MutantInputReader input;

    [Header("Orbita adaptada al jefe")]
    [SerializeField, Min(0.1f)] private float distancia = 11f;
    [SerializeField] private float sensibilidadRaton = 0.12f;
    [SerializeField] private float sensibilidadMando = 150f;
    [SerializeField, Range(-89f, 0f)] private float pitchMinimo = -30f;
    [SerializeField, Range(0f, 89f)] private float pitchMaximo = 65f;
    [SerializeField] private float pitchInicial = 12f;

    [Header("Colision")]
    [SerializeField, Min(0.01f)] private float radioColision = 0.45f;
    [SerializeField, Min(0f)] private float margenColision = 0.15f;
    [SerializeField, Min(0.05f)] private float distanciaMinima = 2f;
    [SerializeField] private LayerMask capasColision = ~0;

    private float yaw;
    private float pitch;

    private void Awake()
    {
        yaw = transform.eulerAngles.y;
        pitch = pitchInicial;
    }

    private void OnEnable()
    {
        SetCursorLocked(true);
    }

    private void OnDisable()
    {
        SetCursorLocked(false);
    }

    private void LateUpdate()
    {
        if (target == null || input == null)
        {
            return;
        }

        Vector2 look = input.Look;
        if (input.IsUsingGamepad)
        {
            yaw += look.x * sensibilidadMando * Time.unscaledDeltaTime;
            pitch -= look.y * sensibilidadMando * Time.unscaledDeltaTime;
        }
        else
        {
            yaw += look.x * sensibilidadRaton;
            pitch -= look.y * sensibilidadRaton;
        }

        pitch = Mathf.Clamp(pitch, pitchMinimo, pitchMaximo);
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 backward = rotation * Vector3.back;
        float resolvedDistance = ResolveCollisionDistance(backward);
        transform.SetPositionAndRotation(
            target.position + backward * resolvedDistance,
            rotation
        );
    }

    public void Configure(Transform newTarget, MutantInputReader newInput)
    {
        target = newTarget;
        input = newInput;
    }

    private float ResolveCollisionDistance(Vector3 backward)
    {
        if (Physics.SphereCast(
                target.position,
                radioColision,
                backward,
                out RaycastHit hit,
                distancia,
                capasColision,
                QueryTriggerInteraction.Ignore
            ))
        {
            return Mathf.Max(distanciaMinima, hit.distance - margenColision);
        }

        return distancia;
    }

    private static void SetCursorLocked(bool value)
    {
        Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !value;
    }
}
