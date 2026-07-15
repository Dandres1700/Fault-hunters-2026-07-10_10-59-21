using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MapaMundialPlayModeRunner
{
    private const string MenuScene = "Assets/Project/Scenes/MenuPrincipal.unity";
    private const string MapName = "MapaMundial";
    private const string RunningKey = "FaultHunters.MapValidation.Running";
    private const string PreviousSceneKey = "FaultHunters.MapValidation.Previous";

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [MenuItem("Tools/Cazadores de Fallas/Probar flujo completo Mapa Mundial %#t")]
    public static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += Run;
            return;
        }
        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.isDirty)
        {
            Debug.LogWarning("Guarda la escena activa antes de ejecutar la validacion.");
            return;
        }
        SessionState.SetString(PreviousSceneKey, active.path ?? string.Empty);
        SessionState.SetBool(RunningKey, true);
        PlayerPrefs.SetInt("MapaMundialValidation.LoadedFromMenu", 0);
        EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false))
        {
            return;
        }
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SceneManager.LoadSceneAsync(MapName);
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(RunningKey, false);
            AssetDatabase.Refresh();
            string previous = SessionState.GetString(PreviousSceneKey, string.Empty);
            if (!string.IsNullOrEmpty(previous))
            {
                EditorApplication.delayCall += () =>
                    EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            }
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!SessionState.GetBool(RunningKey, false) || scene.name != MapName)
        {
            return;
        }
        PlayerPrefs.SetInt("MapaMundialValidation.LoadedFromMenu", 1);
        MapaMundialPlayModeProbe probe =
            Object.FindAnyObjectByType<MapaMundialPlayModeProbe>(FindObjectsInactive.Include);
        if (probe == null)
        {
            Debug.LogError("MapaMundial no contiene el probe de validacion.");
            EditorApplication.isPlaying = false;
            return;
        }
        probe.enabled = true;
    }
}
