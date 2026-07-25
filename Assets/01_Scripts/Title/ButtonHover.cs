using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private Vector3 originalScale;
    [SerializeField] private float hoverScale = 1.1f;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;

    [Tooltip("호버 사운드 볼륨 배율. AudioSource 볼륨에 곱해진다.")]
    [SerializeField, Range(0f, 3f)] private float hoverVolume = 1f;
    [Tooltip("클릭 사운드 볼륨 배율. 1보다 크면 호버보다 크게 들린다.")]
    [SerializeField, Range(0f, 3f)] private float clickVolume = 2f;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = originalScale * hoverScale;
        PlaySound(hoverSound, hoverVolume);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = originalScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PlaySound(clickSound, clickVolume);
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, volume);
    }
}