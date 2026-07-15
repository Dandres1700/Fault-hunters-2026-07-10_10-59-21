using UnityEngine;
using UnityEngine.SceneManagement;

public static class MapaMundialGameplayLayout
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyLighting()
    {
        if (SceneManager.GetActiveScene().name != "MapaMundial")
        {
            return;
        }

        RenderSettings.ambientIntensity = 0.9f;
        RenderSettings.fogDensity = 0.002f;
    }
}
