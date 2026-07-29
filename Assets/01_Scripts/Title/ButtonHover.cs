using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 마우스를 올리면 살짝 커지고, 올리거나 누를 때 소리를 내는 버튼 연출.
/// 타이틀 화면 버튼에 붙여 쓴다.
///
/// 소리는 인스펙터에 꽂아둔 AudioSource 로 직접 재생한다.
/// 설정 창의 효과음 게이지에 묶으려면 같은 오브젝트에 <see cref="VolumeTrackedSource"/> 를 붙인다.
/// </summary>
public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("크기")]
    [Tooltip("마우스를 올렸을 때 커지는 배율. 1이면 커지지 않는다")]
    [SerializeField] private float hoverScale = 1.1f;

    [Header("소리")]
    [Tooltip("소리를 낼 AudioSource. 비워두면 아무 소리도 나지 않는다")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("마우스를 올렸을 때 나는 소리")]
    [SerializeField] private AudioClip hoverSound;

    [Tooltip("눌렀을 때 나는 소리")]
    [SerializeField] private AudioClip clickSound;

    [Tooltip("호버 소리 크기 배율. AudioSource 볼륨에 곱해진다")]
    [SerializeField, Range(0f, 3f)] private float hoverVolume = 1f;

    [Tooltip("클릭 소리 크기 배율. 1보다 크면 호버보다 크게 들린다")]
    [SerializeField, Range(0f, 3f)] private float clickVolume = 2f;

    // 커지기 전 원래 크기. 매번 여기서부터 다시 계산하므로 크기가 누적되지 않는다.
    private Vector3 originalScale;


    // ── 수명주기 ──

    /// <summary>원래 크기를 기억해 둔다.</summary>
    private void Awake()
    {
        originalScale = transform.localScale;
    }


    // ── 포인터 반응 ──

    /// <summary>마우스가 올라오면 크기를 키우고 호버 소리를 낸다.</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = originalScale * hoverScale;
        PlaySound(hoverSound, hoverVolume);
    }

    /// <summary>마우스가 벗어나면 원래 크기로 되돌린다.</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = originalScale;
    }

    /// <summary>눌리면 클릭 소리를 낸다. 씬 전환 등 실제 동작은 Button 의 OnClick 이 맡는다.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        PlaySound(clickSound, clickVolume);
    }

    /// <summary>클립과 AudioSource 가 모두 연결돼 있을 때만 재생한다.</summary>
    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, volume);
    }
}
