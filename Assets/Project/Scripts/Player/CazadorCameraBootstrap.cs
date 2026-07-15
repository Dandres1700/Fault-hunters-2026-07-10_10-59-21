using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CazadorInputReader))]
[DefaultExecutionOrder(100)]
public sealed class CazadorCameraBootstrap : MonoBehaviour
{
    private CazadorInputReader cachedInput;

    private void Awake()
    {
        cachedInput = GetComponent<CazadorInputReader>();
    }

    private void Start()
    {
        SetupCamera();
        EnsureInputEnabled();
        SnapHunterToStreet();
        PositionEnemiesForMap();
    }

    private void SetupCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            mainCamera = cameraObject.AddComponent<Camera>();
            mainCamera.tag = "MainCamera";
            Debug.Log("CazadorCameraBootstrap: Camera.main era null, se creo una.");
        }

        mainCamera.gameObject.SetActive(true);
        mainCamera.enabled = true;

        AudioListener listener = mainCamera.GetComponent<AudioListener>();
        if (listener == null)
        {
            listener = mainCamera.gameObject.AddComponent<AudioListener>();
        }
        listener.enabled = true;

        foreach (Camera other in FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
        {
            if (other != mainCamera)
            {
                other.enabled = false;
            }
        }

        foreach (AudioListener other in FindObjectsByType<AudioListener>(
                     FindObjectsInactive.Exclude))
        {
            if (other != null && other != listener &&
                other.GetComponentInParent<Camera>() != mainCamera)
            {
                other.enabled = false;
            }
        }

        CazadorCameraController controller =
            mainCamera.GetComponent<CazadorCameraController>();
        if (controller == null)
        {
            controller = mainCamera.gameObject.AddComponent<CazadorCameraController>();
        }
    }

    private void EnsureInputEnabled()
    {
        PlayerInput playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            if (playerInput.actions == null)
            {
                Debug.LogError(
                    "CazadorCameraBootstrap: PlayerInput.actions es null. " +
                    "Asigna PlayerController.inputactions en el Inspector del PlayerInput.",
                    this
                );
            }
            playerInput.enabled = true;
        }
        else
        {
            Debug.LogError(
                "CazadorCameraBootstrap: No hay PlayerInput en el Cazador.",
                this
            );
        }

        CazadorInputReader cazadorInput = GetComponent<CazadorInputReader>();
        if (cazadorInput != null)
        {
            cazadorInput.enabled = true;
        }
    }

    private void SnapHunterToStreet()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MapaMundial")
        {
            return;
        }

        GameObject street = GameObject.Find("Object_2");
        Collider streetCollider = street != null
            ? street.GetComponent<Collider>()
            : null;
        CharacterController controller = GetComponent<CharacterController>();
        if (streetCollider == null || controller == null)
        {
            return;
        }

        Bounds bounds = streetCollider.bounds;
        Vector3 origin = new Vector3(bounds.center.x, bounds.max.y + 100f, bounds.center.z);
        if (!streetCollider.Raycast(new Ray(origin, Vector3.down), out RaycastHit hit, 250f))
        {
            return;
        }

        controller.enabled = false;
        transform.position = hit.point + Vector3.up *
            (controller.height * 0.5f - controller.center.y + 0.05f);
        controller.enabled = true;
        Physics.SyncTransforms();
    }

    private void PositionEnemiesForMap()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MapaMundial")
        {
            return;
        }

        Collider streetCollider = GameObject.Find("Object_2")?.GetComponent<Collider>();
        CazadorStats hunter = GetComponent<CazadorStats>();
        if (streetCollider == null || hunter == null)
        {
            return;
        }

        PlaceOnStreet(GameObject.Find("Mutant_CPU_MapaMundial"), streetCollider,
            hunter.transform.position + new Vector3(0f, 0f, -65f));
        PlaceOnStreet(GameObject.Find("RobotFallaExplosiva_MapaMundial"), streetCollider,
            hunter.transform.position + new Vector3(-45f, 0f, -30f));
        PlaceOnStreet(GameObject.Find("RobotFallaRastrera_MapaMundial"), streetCollider,
            hunter.transform.position + new Vector3(48f, 0f, -32f));
        PlaceOnStreet(GameObject.Find("RobotFallaGeneradora_MapaMundial"), streetCollider,
            hunter.transform.position + new Vector3(65f, 0f, 15f));

        MutantEnemyIntentSource mutant = FindAnyObjectByType<MutantEnemyIntentSource>();
        mutant?.SetTarget(hunter);
        foreach (FallaCore robot in FindObjectsByType<FallaCore>(
                     FindObjectsInactive.Exclude))
        {
            robot.SetTarget(hunter.transform);
        }
    }

    private static void PlaceOnStreet(GameObject target, Collider streetCollider,
        Vector3 desired)
    {
        if (target == null)
        {
            return;
        }

        Bounds streetBounds = streetCollider.bounds;
        Ray ray = new Ray(new Vector3(desired.x, streetBounds.max.y + 100f, desired.z),
            Vector3.down);
        if (!streetCollider.Raycast(ray, out RaycastHit hit, 250f))
        {
            return;
        }

        target.transform.position = hit.point;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds targetBounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                targetBounds.Encapsulate(renderers[index].bounds);
            }
            target.transform.position += Vector3.up * (hit.point.y + 0.1f - targetBounds.min.y);
        }
        Physics.SyncTransforms();
    }
}
