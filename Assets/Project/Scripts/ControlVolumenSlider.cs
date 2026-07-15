using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class ControlVolumenSlider : MonoBehaviour
{
    private const string ClaveVolumen = "VolumenGeneral";

    private Slider slider;

    private void Awake()
    {
        slider = GetComponent<Slider>();

        float volumenGuardado =
            PlayerPrefs.GetFloat(ClaveVolumen, 0.7f);

        AudioListener.volume = volumenGuardado;

        slider.SetValueWithoutNotify(volumenGuardado);

        slider.onValueChanged.AddListener(CambiarVolumen);
    }

    private void CambiarVolumen(float nuevoVolumen)
    {
        nuevoVolumen = Mathf.Clamp01(nuevoVolumen);

        AudioListener.volume = nuevoVolumen;

        PlayerPrefs.SetFloat(ClaveVolumen, nuevoVolumen);
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        if (slider != null)
        {
            slider.onValueChanged.RemoveListener(CambiarVolumen);
        }
    }
}