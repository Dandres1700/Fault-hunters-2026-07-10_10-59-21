using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MapaMundialLayoutTool
{
    private const string ScenePath = "Assets/Project/Scenes/MapaMundial.unity";
    private static readonly Vector3 MapScale = Vector3.one * 0.050465586f;
    private static readonly Vector3 MutantScale = Vector3.one * 25f;

    [MenuItem("Tools/Cazadores de Fallas/Mapa Mundial/Ampliar mapa y reducir Mutant")]
    public static void Apply()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform map = FindInScene(scene, "Mapa1_CiudadAbandonada");
        Transform mutant = FindInScene(scene, "Mutant_CPU_MapaMundial");

        if (map == null || mutant == null)
        {
            throw new InvalidOperationException(
                "No se encontro el mapa o Mutant_CPU_MapaMundial en MapaMundial."
            );
        }

        map.localScale = MapScale;
        mutant.localScale = MutantScale;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("MapaMundial actualizado: mapa ampliado y Mutant reducido.");
    }

    private static Transform FindInScene(Scene scene, string objectName)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(item => string.Equals(
                item.name, objectName, StringComparison.OrdinalIgnoreCase));
    }
}
