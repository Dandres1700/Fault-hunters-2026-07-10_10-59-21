using UnityEngine;
using UnityEngine.EventSystems;

public class BotonHover : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("Efecto del botón")]
    [SerializeField] private float aumento = 1.08f;
    [SerializeField] private float velocidad = 12f;

    private Vector3 escalaNormal;
    private Vector3 escalaObjetivo;

    private void Awake()
    {
        escalaNormal = transform.localScale;
        escalaObjetivo = escalaNormal;
    }

    private void Update()
    {
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            escalaObjetivo,
            Time.unscaledDeltaTime * velocidad
        );
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        escalaObjetivo = escalaNormal * aumento;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        escalaObjetivo = escalaNormal;
    }

    private void OnDisable()
    {
        transform.localScale = escalaNormal;
        escalaObjetivo = escalaNormal;
    }
}