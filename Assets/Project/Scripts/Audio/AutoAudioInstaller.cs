using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Instala audio en personajes y controles existentes sin modificar prefabs a mano.
/// Tambien detecta objetos instanciados durante la partida.
/// </summary>
[DefaultExecutionOrder(-900)]
public sealed class AutoAudioInstaller : MonoBehaviour
{
    private static AutoAudioInstaller instance;
    private Coroutine scanRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject root = new GameObject("[Auto Audio Installer]");
        instance = root.AddComponent<AutoAudioInstaller>();
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

    private void OnEnable()
    {
        scanRoutine = StartCoroutine(PeriodicScan());
    }

    private void OnDisable()
    {
        if (scanRoutine != null)
        {
            StopCoroutine(scanRoutine);
            scanRoutine = null;
        }
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
        InstallAll();
    }

    private IEnumerator PeriodicScan()
    {
        while (true)
        {
            InstallAll();
            yield return new WaitForSecondsRealtime(1.25f);
        }
    }

    private static void InstallAll()
    {
        CazadorController[] players = FindObjectsByType<CazadorController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        foreach (CazadorController player in players)
        {
            if (player != null && player.GetComponent<CazadorAudioController>() == null)
            {
                player.gameObject.AddComponent<CazadorAudioController>();
            }
        }

        MutantStats[] mutants = FindObjectsByType<MutantStats>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        foreach (MutantStats mutant in mutants)
        {
            if (mutant != null && mutant.GetComponent<MutantAudioController>() == null)
            {
                mutant.gameObject.AddComponent<MutantAudioController>();
            }
        }

        Selectable[] controls = FindObjectsByType<Selectable>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        foreach (Selectable control in controls)
        {
            if (control == null || control.GetComponent<UISoundFeedback>() != null ||
                control.GetComponent<SonidoClickBoton>() != null)
            {
                continue;
            }

            control.gameObject.AddComponent<UISoundFeedback>();
        }
    }
}
