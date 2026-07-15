using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuPrincipalController : MonoBehaviour
{
    [Header("Escenas que se conectarán después")]
    [SerializeField] private string escenaContinuar = "MapaMundial";
    [SerializeField] private string escenaNuevaPartida = "Prologo";
    [SerializeField] private string escenaSeleccionarMision = "MapaMundial";
    [SerializeField] private string escenaArchivosFalla;
    [SerializeField] private string escenaOpciones;

    public void Continuar()
    {
        CargarEscena(escenaContinuar, "Continuar");
    }

    public void NuevaPartida()
    {
        GameProgress.ResetProgress();
        CargarEscena(escenaNuevaPartida, "Nueva partida");
    }

    public void SeleccionarMision()
    {
        CargarEscena(escenaSeleccionarMision, "Seleccionar misión");
    }

    public void AbrirArchivos()
    {
        CargarEscena(escenaArchivosFalla, "Archivos de fallas");
    }

    public void AbrirOpciones()
    {
        CargarEscena(escenaOpciones, "Opciones");
    }

    private void CargarEscena(string nombreEscena, string nombreBoton)
    {
        if (string.IsNullOrWhiteSpace(nombreEscena))
        {
            Debug.LogWarning(
                nombreBoton + ": todavía no tiene una escena conectada."
            );

            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(nombreEscena))
        {
            Debug.LogError(
                "La escena '" + nombreEscena +
                "' no está agregada al Build Profile."
            );

            return;
        }

        SceneManager.LoadSceneAsync(nombreEscena);
    }

    public void Salir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
