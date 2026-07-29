using UnityEngine;

/// <summary>
/// 씬이나 프리팹에 직접 놓아 둔 AudioSource 를 설정 창의 게이지에 연동시킨다.
/// <see cref="AudioManager"/> 를 거치지 않고 재생하는 소리에 이걸 붙여두면
/// 게이지를 움직일 때 같이 볼륨이 바뀐다.
///
/// 인스펙터에 넣어둔 볼륨을 원본 크기로 기억하고, 거기에 설정값을 곱한다.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public class VolumeTrackedSource : MonoBehaviour
{
    /// <summary>이 소리를 묶을 설정 게이지.</summary>
    public enum Channel
    {
        Bgm,
        Sfx,
    }

    [Tooltip("이 소리를 어느 게이지에 묶을지")]
    [SerializeField] private Channel channel = Channel.Sfx;

    private AudioSource source;
    private float baseVolume;


    // ── 수명주기 ──

    /// <summary>인스펙터에 넣어둔 볼륨을 원본 크기로 기억해 둔다.</summary>
    private void Awake()
    {
        source = GetComponent<AudioSource>();
        baseVolume = source.volume;
    }

    /// <summary>게이지 변경 통지를 구독하고 현재 설정을 곧바로 반영한다.</summary>
    private void OnEnable()
    {
        GameSettings.BgmVolumeChanged += OnBgmChanged;
        GameSettings.SfxVolumeChanged += OnSfxChanged;
        Apply();
    }

    /// <summary>게이지 변경 통지 구독을 해제한다.</summary>
    private void OnDisable()
    {
        GameSettings.BgmVolumeChanged -= OnBgmChanged;
        GameSettings.SfxVolumeChanged -= OnSfxChanged;
    }


    // ── 볼륨 반영 ──

    /// <summary>런타임에 원본 볼륨을 바꾸고 싶을 때.</summary>
    public void SetBaseVolume(float volume)
    {
        baseVolume = Mathf.Clamp01(volume);
        Apply();
    }

    /// <summary>BGM 게이지가 움직였을 때. 이 소리가 BGM 채널일 때만 반영한다.</summary>
    private void OnBgmChanged(float volume)
    {
        if (channel == Channel.Bgm)
            Apply();
    }

    /// <summary>효과음 게이지가 움직였을 때. 이 소리가 효과음 채널일 때만 반영한다.</summary>
    private void OnSfxChanged(float volume)
    {
        if (channel == Channel.Sfx)
            Apply();
    }

    /// <summary>원본 크기에 해당 채널의 설정값을 곱해 AudioSource 에 적용한다.</summary>
    private void Apply()
    {
        if (source == null)
            return;

        float setting = channel == Channel.Bgm ? GameSettings.BgmVolume : GameSettings.SfxVolume;
        source.volume = baseVolume * setting;
    }
}
