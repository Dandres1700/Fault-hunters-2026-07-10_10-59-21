using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// La ejecucion automatica ocurre solo mientras no exista un reporte persistido.
public static class RobotFallaPlayModeRunner
{
    private const string ValidationScenePath =
        "Assets/Project/Scenes/FallaValidation.unity";
    private const string RunKey = "CazadoresDeFallas.PlayModeValidation.v3";
    private const string RunningKey = "CazadoresDeFallas.PlayModeValidation.Running";
    private const string PreviousSceneKey = "CazadoresDeFallas.PlayModeValidation.PreviousScene";
    private const string ReportPath =
        "Assets/Project/Validation/RobotFallaPlayModeReport.json";

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (AssetDatabase.LoadAssetAtPath<TextAsset>(ReportPath) == null &&
            !SessionState.GetBool(RunKey, false) &&
            !SessionState.GetBool(RunningKey, false))
        {
            SessionState.SetBool(RunKey, true);
            EditorApplication.delayCall += Run;
        }
    }

    [MenuItem("Tools/Cazadores de Fallas/Ejecutar validacion Play Mode")]
    public static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += Run;
            return;
        }
        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.isDirty)
        {
            Debug.LogWarning(
                "No se inicio la validacion automatica porque la escena activa tiene cambios sin guardar.");
            return;
        }
        SessionState.SetString(PreviousSceneKey, active.path ?? string.Empty);
        SessionState.SetBool(RunningKey, true);
        Scene validation = EditorSceneManager.OpenScene(
            ValidationScenePath, OpenSceneMode.Single);
        RobotFallaPlayModeProbe probe =
            Object.FindAnyObjectByType<RobotFallaPlayModeProbe>(FindObjectsInactive.Include);
        if (probe == null)
        {
            Debug.LogError("FallaValidation no contiene RobotFallaPlayModeProbe.");
            SessionState.SetBool(RunningKey, false);
            return;
        }
        probe.enabled = true;
        EditorSceneManager.MarkSceneDirty(validation);
        EditorSceneManager.SaveScene(validation);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode ||
            !SessionState.GetBool(RunningKey, false))
        {
            return;
        }
        SessionState.SetBool(RunningKey, false);
        AssetDatabase.Refresh();
        RobotFallaPlayModeProbe probe =
            Object.FindAnyObjectByType<RobotFallaPlayModeProbe>(FindObjectsInactive.Include);
        if (probe != null)
        {
            probe.enabled = false;
            Scene validation = probe.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(validation);
            EditorSceneManager.SaveScene(validation);
        }
        string previousScene = SessionState.GetString(PreviousSceneKey, string.Empty);
        if (!string.IsNullOrEmpty(previousScene) && previousScene != ValidationScenePath)
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
                }
            };
        }
    }
}
