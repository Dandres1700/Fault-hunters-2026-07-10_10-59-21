using UnityEngine;

public class CargarVolumenGuardado : MonoBehaviour
{
    private const string ClaveVolumen = "VolumenGeneral";

    private void Awake()
    {
        float volumenGuardado =
            PlayerPrefs.GetFloat(ClaveVolumen, 0.7f);

        AudioListener.volume = volumenGuardado;
    }
}