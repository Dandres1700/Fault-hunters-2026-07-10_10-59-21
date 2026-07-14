using UnityEngine;
using UnityEngine.EventSystems;

public class SonidoClickBoton : MonoBehaviour, IPointerDownHandler
{
    [Header("Sonido del botón")]
    [SerializeField] private AudioSource fuenteAudio;
    [SerializeField] private AudioClip sonidoClick;

    [Range(0f, 1f)]
    [SerializeField] private float volumen = 0.7f;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (fuenteAudio == null || sonidoClick == null)
        {
            Debug.LogWarning("Falta asignar el AudioSource o el sonido de clic.");
            return;
        }

        fuenteAudio.PlayOneShot(sonidoClick, volumen);
    }
}