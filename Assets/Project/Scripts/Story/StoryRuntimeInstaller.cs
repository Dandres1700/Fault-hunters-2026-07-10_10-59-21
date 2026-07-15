using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Inserta los controladores narrativos apropiados al cargar cada escena.
/// </summary>
[DefaultExecutionOrder(-800)]
public sealed class StoryRuntimeInstaller : MonoBehaviour
{
    private static StoryRuntimeInstaller instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject root = new GameObject("[Story Runtime]");
        instance = root.AddComponent<StoryRuntimeInstaller>();
        DontDestroyOnLoad(root);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        InstallForScene(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallForScene(scene);
    }

    private static void InstallForScene(Scene scene)
    {
        switch (scene.name)
        {
            case FlujoEscenas.Prologo:
                AddSceneController<PrologoController>(scene, "[Prologo Controller]");
                break;
            case FlujoEscenas.MapaMundial:
                AddSceneController<WorldMapStoryController>(scene, "[World Map Story]");
                break;
            case FlujoEscenas.Mision:
                AddSceneController<MissionFlowController>(scene, "[Mission Story Flow]");
                break;
        }
    }

    private static void AddSceneController<T>(Scene scene, string objectName)
        where T : Component
    {
        if (FindFirstObjectByType<T>() != null)
        {
            return;
        }

        GameObject go = new GameObject(objectName);
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<T>();
    }
}
