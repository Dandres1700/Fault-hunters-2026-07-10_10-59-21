using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ConfigurarFlujoEscenasTool
{
    private const string MenuPath = "Assets/Project/Scenes/MenuPrincipal.unity";
    private const string OpcionesPath = "Assets/Project/Scenes/Opciones.unity";

    [MenuItem("Fault Hunters/Configurar flujo de escenas")]
    public static void ConfigurarDesdeMenu()
    {
        ConfigurarTodo();
    }

    private static void ConfigurarTodo()
    {
        ConfigurarEscena(MenuPath, ConfigurarMenu);
        ConfigurarEscena(OpcionesPath, ConfigurarOpciones);
        AssetDatabase.SaveAssets();
        Debug.Log("Flujo de escenas configurado correctamente.");
    }

    private static void ConfigurarEscena(string path, Action<Scene> configurar)
    {
        Scene escena = SceneManager.GetSceneByPath(path);
        bool abiertaPorHerramienta = !escena.IsValid() || !escena.isLoaded;

        if (abiertaPorHerramienta)
        {
            escena = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        configurar(escena);
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);

        if (abiertaPorHerramienta)
        {
            EditorSceneManager.CloseScene(escena, true);
        }
    }

    private static void ConfigurarMenu(Scene escena)
    {
        FlujoEscenas flujo = ObtenerFlujo(escena, "MenuManager");

        Conectar(escena, "BotonNuevaPartida", flujo, flujo.NuevaPartida);
        Conectar(escena, "BotonContinuar", flujo, flujo.ContinuarPartida);
        Conectar(escena, "BotonSeleccionarMision", flujo, flujo.VolverAlMapa);
        Conectar(escena, "BotonOpciones", flujo, flujo.AbrirOpciones);
    }

    private static void ConfigurarOpciones(Scene escena)
    {
        FlujoEscenas flujo = ObtenerFlujo(escena, "OpcionesManager");
        Conectar(escena, "BotonVolver", flujo, flujo.CerrarOpciones);
    }

    private static FlujoEscenas ObtenerFlujo(Scene escena, string managerName)
    {
        GameObject manager = BuscarObjeto(escena, managerName);

        if (manager == null)
        {
            manager = new GameObject(managerName);
            SceneManager.MoveGameObjectToScene(manager, escena);
        }

        FlujoEscenas flujo = manager.GetComponent<FlujoEscenas>();
        return flujo != null ? flujo : manager.AddComponent<FlujoEscenas>();
    }

    private static void Conectar(
        Scene escena,
        string buttonName,
        FlujoEscenas flujo,
        UnityEngine.Events.UnityAction accion)
    {
        GameObject buttonObject = BuscarObjeto(escena, buttonName);
        Button button = buttonObject != null ? buttonObject.GetComponent<Button>() : null;

        if (button == null)
        {
            throw new InvalidOperationException(
                $"No se encontro el boton '{buttonName}' en '{escena.name}'."
            );
        }

        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEngine.Object target = button.onClick.GetPersistentTarget(i);

            if (target is MenuPrincipalController ||
                target is CargarEscenaBoton ||
                target is BotonVolverMenu ||
                target is FlujoEscenas)
            {
                UnityEventTools.RemovePersistentListener(button.onClick, i);
            }
        }

        UnityEventTools.AddPersistentListener(button.onClick, accion);
        EditorUtility.SetDirty(button);
        EditorUtility.SetDirty(flujo);
    }

    private static GameObject BuscarObjeto(Scene escena, string objectName)
    {
        foreach (GameObject root in escena.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

            foreach (Transform candidate in transforms)
            {
                if (candidate.name == objectName)
                {
                    return candidate.gameObject;
                }
            }
        }

        return null;
    }
}
