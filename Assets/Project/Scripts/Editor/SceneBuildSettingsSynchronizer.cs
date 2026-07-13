using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

[InitializeOnLoad]
internal static class SceneBuildSettingsSynchronizer
{
    private const string ScenesRoot = "Assets/Project/Scenes/";

    static SceneBuildSettingsSynchronizer()
    {
        EditorApplication.delayCall += Synchronize;
    }

    [MenuItem("Tools/Fault Hunters/Sincronizar escenas")]
    private static void Synchronize()
    {
        string[] projectScenes = AssetDatabase
            .FindAssets("t:Scene", new[] { ScenesRoot.TrimEnd('/') })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Dictionary<string, string> pathsByGuid = projectScenes.ToDictionary(
            AssetDatabase.AssetPathToGUID,
            path => path,
            StringComparer.OrdinalIgnoreCase
        );

        List<EditorBuildSettingsScene> synchronizedScenes =
            new List<EditorBuildSettingsScene>();
        HashSet<string> configuredGuids = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        );

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            string sceneGuid = scene.guid.ToString();

            if (pathsByGuid.TryGetValue(sceneGuid, out string currentPath))
            {
                if (configuredGuids.Add(sceneGuid))
                {
                    synchronizedScenes.Add(
                        new EditorBuildSettingsScene(currentPath, scene.enabled)
                    );
                }
            }
            else if (!scene.path.StartsWith(
                ScenesRoot,
                StringComparison.OrdinalIgnoreCase
            ))
            {
                synchronizedScenes.Add(scene);
                configuredGuids.Add(sceneGuid);
            }
        }

        foreach (string scenePath in projectScenes)
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);

            if (configuredGuids.Add(sceneGuid))
            {
                synchronizedScenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }
        }

        if (!HaveSameConfiguration(EditorBuildSettings.scenes, synchronizedScenes))
        {
            EditorBuildSettings.scenes = synchronizedScenes.ToArray();
        }
    }

    private static bool HaveSameConfiguration(
        IReadOnlyList<EditorBuildSettingsScene> current,
        IReadOnlyList<EditorBuildSettingsScene> synchronized
    )
    {
        if (current.Count != synchronized.Count)
        {
            return false;
        }

        for (int index = 0; index < current.Count; index++)
        {
            if (current[index].enabled != synchronized[index].enabled ||
                !string.Equals(
                    current[index].path,
                    synchronized[index].path,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return false;
            }
        }

        return true;
    }

    private sealed class SceneAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths
        )
        {
            if (ContainsManagedScene(importedAssets) ||
                ContainsManagedScene(deletedAssets) ||
                ContainsManagedScene(movedAssets) ||
                ContainsManagedScene(movedFromAssetPaths))
            {
                EditorApplication.delayCall += Synchronize;
            }
        }

        private static bool ContainsManagedScene(IEnumerable<string> paths)
        {
            return paths.Any(path =>
                path.StartsWith(ScenesRoot, StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
            );
        }
    }
}
