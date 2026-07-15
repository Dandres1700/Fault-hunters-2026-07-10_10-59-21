using UnityEngine;
using UnityEngine.SceneManagement;

public class BotonVolverMenu : MonoBehaviour
{
    [Header("Escena a la que regresará")]
    [SerializeField] private string escenaMenu = "MenuPrincipal";

    public void Volver()
    {
        if (string.IsNullOrWhiteSpace(escenaMenu))
        {
            Debug.LogError("No se ha indicado la escena del menú principal.");
            return;
        }

        SceneManager.LoadSceneAsync(escenaMenu);
    }
}