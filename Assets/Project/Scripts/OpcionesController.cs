using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OpcionesController : MonoBehaviour
{
    [Header("Volumen")]
    [SerializeField] private Slider sliderVolumen;

    [Header("Escena del menú principal")]
    [SerializeField] private string escenaMenu = "MenuPrincipal";

    private const string ClaveVolumen = "VolumenGeneral";

    private void Start()
    {
        float volumenGuardado =
            PlayerPrefs.GetFloat(ClaveVolumen, 0.7f);

        AudioListener.volume = volumenGuardado;

        if (sliderVolumen != null)
        {
            sliderVolumen.SetValueWithoutNotify(volumenGuardado);
        }
    }

    public void CambiarVolumen(float nuevoVolumen)
    {
        float volumenSeguro = Mathf.Clamp01(nuevoVolumen);

        AudioListener.volume = volumenSeguro;

        PlayerPrefs.SetFloat(
            ClaveVolumen,
            volumenSeguro
        );

        PlayerPrefs.Save();
    }

    public void VolverAlMenu()
    {
        SceneManager.LoadSceneAsync(escenaMenu);
    }
}