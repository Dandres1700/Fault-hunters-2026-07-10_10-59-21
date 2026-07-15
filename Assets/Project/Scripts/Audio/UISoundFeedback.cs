using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class UISoundFeedback : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        GameAudioManager.Instance.PlayUI(GameSfx.UiHover, 0.45f, Random.Range(0.98f, 1.04f));
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        GameAudioManager.Instance.PlayUI(GameSfx.UiClick, 0.72f, Random.Range(0.97f, 1.03f));
    }
}
