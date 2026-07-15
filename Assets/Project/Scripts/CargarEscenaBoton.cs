using UnityEngine;
using UnityEngine.SceneManagement;

public class CargarEscenaBoton : MonoBehaviour
{
    [Header("Nombre exacto de la escena")]
    [SerializeField] private string nombreEscena;

    public void CargarEscena()
    {
        if (string.IsNullOrWhiteSpace(nombreEscena))
        {
            Debug.LogError(
                gameObject.name + ": no tiene un nombre de escena asignado."
            );
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(nombreEscena))
        {
            Debug.LogError(
                "La escena '" + nombreEscena +
                "' no está agregada al Build Profile o el nombre está mal escrito."
            );
            return;
        }

        SceneManager.LoadSceneAsync(nombreEscena);
    }
}