#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AudioStoryValidationTool
{
    private static readonly string[] RequiredScenes =
    {
        "MenuPrincipal",
        "Prologo",
        "MapaMundial",
        "SampleScene",
        "Opciones"
    };

    private static readonly string[] RequiredAudio =
    {
        "Audio/Music/prologue_theme",
        "Audio/Music/map_theme",
        "Audio/Music/battle_theme",
        "Audio/Ambience/prologue_ambience",
        "Audio/Ambience/map_ambience",
        "Audio/Ambience/mission_ambience",
        "Audio/SFX/UI/ui_hover",
        "Audio/SFX/UI/ui_click",
        "Audio/SFX/UI/story_advance",
        "Audio/SFX/UI/mission_confirm",
        "Audio/SFX/Player/jump",
        "Audio/SFX/Player/land",
        "Audio/SFX/Player/death",
        "Audio/SFX/Boss/roar",
        "Audio/SFX/Boss/hurt",
        "Audio/SFX/Boss/death",
        "Audio/SFX/World/impact",
        "Audio/SFX/World/mission_start",
        "Audio/SFX/World/boss_defeated"
    };

    [MenuItem("Tools/Cazadores de Fallas/Validar historia y audio")]
    public static void ValidateProject()
    {
        List<string> errors = new List<string>();
        HashSet<string> buildScenes = new HashSet<string>(
            EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => System.IO.Path.GetFileNameWithoutExtension(scene.path))
        );

        foreach (string scene in RequiredScenes)
        {
            if (!buildScenes.Contains(scene))
            {
                errors.Add($"La escena '{scene}' no está activa en Build Settings.");
            }
        }

        foreach (string path in RequiredAudio)
        {
            if (Resources.Load<AudioClip>(path) == null)
            {
                errors.Add($"No se encontró el AudioClip de Resources: {path}");
            }
        }

        if (Resources.LoadAll<AudioClip>("Audio/SFX/Player/Footsteps").Length == 0)
        {
            errors.Add("No hay pasos del jugador en Audio/SFX/Player/Footsteps.");
        }

        if (Resources.LoadAll<AudioClip>("Audio/SFX/Boss/Footsteps").Length == 0)
        {
            errors.Add("No hay pasos del jefe en Audio/SFX/Boss/Footsteps.");
        }

        if (errors.Count == 0)
        {
            Debug.Log(
                "<b>[Cazadores de Fallas]</b> Validación completada: escenas, historia y audio base están listos."
            );
            EditorUtility.DisplayDialog(
                "Cazadores de Fallas",
                "Validación completada. No se encontraron errores en el sistema base de historia y audio.",
                "Aceptar"
            );
            return;
        }

        string report = string.Join("\n• ", errors);
        Debug.LogError($"[Cazadores de Fallas] Problemas encontrados:\n• {report}");
        EditorUtility.DisplayDialog(
            "Cazadores de Fallas - Revisión necesaria",
            $"Se encontraron {errors.Count} problema(s). Revisa la consola de Unity para ver el detalle.",
            "Aceptar"
        );
    }
}
#endif
