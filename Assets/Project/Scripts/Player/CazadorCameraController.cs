using UnityEngine;

[DisallowMultipleComponent]
public sealed class CazadorCameraController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform target;
    [SerializeField] private CazadorInputReader input;

    [Header("Orbita")]
    [SerializeField] private bool primeraPersona = false;
    [SerializeField, Min(0.1f)] private float distancia = 9f;
    [SerializeField] private float sensibilidadRaton = 0.12f;
    [SerializeField] private float sensibilidadMando = 150f;
    [SerializeField, Range(-89f, 0f)] private float pitchMinimo = -35f;
    [SerializeField, Range(0f, 89f)] private float pitchMaximo = 70f;
    [SerializeField] private float pitchInicial = 15f;

    [Header("Colision")]
    [SerializeField, Min(0.01f)] private float radioColision = 0.2f;
    [SerializeField, Min(0f)] private float margenColision = 0.08f;
    [SerializeField, Min(0.05f)] private float distanciaMinima = 0.35f;
    [SerializeField] private LayerMask capasColision = ~0;

    private float yaw;
    private float pitch;
    private bool loggedMissing;

    private void Awake()
    {
        AutoFindReferences();

        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = pitchInicial;
    }

    private void OnEnable()
    {
        AutoFindReferences();
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
            AutoFindReferences();
            if (target == null || input == null)
            {
                if (!loggedMissing)
                {
                    loggedMissing = true;
                    Debug.LogWarning(
                        $"CazadorCameraController: sin referencia. " +
                        $"target={target}, input={input}", this);
                }
                return;
            }
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

        // En primera persona la cámara permanece en el punto de vista del
        // cazador; solo su orientación cambia con el mouse o mando.
        if (primeraPersona)
        {
            transform.SetPositionAndRotation(target.position, rotation);
            return;
        }

        Vector3 backward = rotation * Vector3.back;
        float resolvedDistance = ResolveCollisionDistance(backward);

        transform.SetPositionAndRotation(
            target.position + backward * resolvedDistance,
            rotation
        );
    }

    private void AutoFindReferences()
    {
        if (input == null)
        {
            input = FindFirstObjectByType<CazadorInputReader>();
        }

        if (target == null && input != null)
        {
            Transform cazadorTransform = input.transform;
            Transform existing = cazadorTransform.Find("CameraTarget");
            if (existing == null)
            {
                existing = cazadorTransform.Find("CameraTarget_MapaMundial");
            }
            if (existing != null)
            {
                target = existing;
            }
            else
            {
                GameObject targetObject = new GameObject("CameraTarget");
                targetObject.transform.SetParent(cazadorTransform, false);
                Renderer[] renderers =
                    cazadorTransform.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                    {
                        bounds.Encapsulate(renderers[i].bounds);
                    }
                    targetObject.transform.localPosition =
                        bounds.center - cazadorTransform.position +
                        Vector3.up * bounds.extents.y * 0.15f;
                }
                else
                {
                    targetObject.transform.localPosition = Vector3.up * 1f;
                }
                target = targetObject.transform;
            }
        }

        if (target != null && input != null && !loggedMissing)
        {
            Debug.Log(
                $"CazadorCameraController: referencias OK. " +
                $"target={target.name}, input={input.name}, " +
                $"input.enabled={input.enabled}, " +
                $"input.Look={input.Look}", this);
        }
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

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
