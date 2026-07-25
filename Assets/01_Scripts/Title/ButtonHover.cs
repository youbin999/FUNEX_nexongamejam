using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private Vector3 originalScale;
    [SerializeField] private float hoverScale = 1.1f;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;

    [Header("Click")]
    [Tooltip("클릭했을 때 할 일. 씬 이동 등을 인스펙터에서 연결한다")]
    [SerializeField] private UnityEvent onClick;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = originalScale * hoverScale;
        PlaySound(hoverSound);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = originalScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PlaySound(clickSound);
        onClick.Invoke();
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }
}