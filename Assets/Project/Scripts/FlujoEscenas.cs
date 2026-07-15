using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Centraliza la navegacion entre las escenas principales del juego.
/// Sus metodos publicos se pueden conectar directamente a botones o UnityEvents.
/// </summary>
public class FlujoEscenas : MonoBehaviour
{
    public const string MenuPrincipal = "MenuPrincipal";
    public const string Prologo = "Prologo";
    public const string MapaMundial = "MapaMundial";
    public const string Mision = "SampleScene";
    public const string Opciones = "Opciones";

    private static string escenaAnterior = MenuPrincipal;
    private bool cargandoEscena;

    public void NuevaPartida()
    {
        Cargar(Prologo);
    }

    public void ContinuarPartida()
    {
        Cargar(MapaMundial);
    }

    public void FinalizarPrologo()
    {
        Cargar(MapaMundial);
    }

    public void IniciarMision()
    {
        Cargar(Mision);
    }

    public void VolverAlMapa()
    {
        Cargar(MapaMundial);
    }

    public void VolverAlMenu()
    {
        Cargar(MenuPrincipal);
    }

    public void AbrirOpciones()
    {
        string escenaActual = SceneManager.GetActiveScene().name;

        if (escenaActual != Opciones)
        {
            escenaAnterior = escenaActual;
        }

        Cargar(Opciones);
    }

    public void CerrarOpciones()
    {
        string destino = string.IsNullOrWhiteSpace(escenaAnterior)
            ? MenuPrincipal
            : escenaAnterior;

        Cargar(destino);
    }

    /// <summary>
    /// Avanza segun el orden principal del juego.
    /// </summary>
    public void CargarSiguiente()
    {
        string escenaActual = SceneManager.GetActiveScene().name;

        switch (escenaActual)
        {
            case MenuPrincipal:
                Cargar(Prologo);
                break;
            case Prologo:
                Cargar(MapaMundial);
                break;
            case MapaMundial:
                Cargar(Mision);
                break;
            case Mision:
                Cargar(MapaMundial);
                break;
            default:
                Debug.LogWarning(
                    $"No se definio una escena siguiente para '{escenaActual}'.",
                    this
                );
                break;
        }
    }

    public void CargarEscena(string nombreEscena)
    {
        Cargar(nombreEscena);
    }

    private void Cargar(string nombreEscena)
    {
        if (cargandoEscena)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(nombreEscena))
        {
            Debug.LogError("El nombre de la escena esta vacio.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(nombreEscena))
        {
            Debug.LogError(
                $"La escena '{nombreEscena}' no esta incluida en Build Settings.",
                this
            );
            return;
        }

        cargandoEscena = true;
        AsyncOperation carga = SceneManager.LoadSceneAsync(nombreEscena);

        if (carga == null)
        {
            cargandoEscena = false;
            Debug.LogError($"No se pudo iniciar la carga de '{nombreEscena}'.", this);
        }
    }
}
