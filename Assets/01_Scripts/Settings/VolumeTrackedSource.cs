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
    public enum Channel
    {
        Bgm,
        Sfx,
    }

    [Tooltip("이 소리를 어느 게이지에 묶을지")]
    [SerializeField] private Channel channel = Channel.Sfx;

    private AudioSource source;
    private float baseVolume;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        baseVolume = source.volume;
    }

    private void OnEnable()
    {
        GameSettings.BgmVolumeChanged += OnBgmChanged;
        GameSettings.SfxVolumeChanged += OnSfxChanged;
        Apply();
    }

    private void OnDisable()
    {
        GameSettings.BgmVolumeChanged -= OnBgmChanged;
        GameSettings.SfxVolumeChanged -= OnSfxChanged;
    }

    /// <summary>런타임에 원본 볼륨을 바꾸고 싶을 때.</summary>
    public void SetBaseVolume(float volume)
    {
        baseVolume = Mathf.Clamp01(volume);
        Apply();
    }

    private void OnBgmChanged(float volume)
    {
        if (channel == Channel.Bgm)
            Apply();
    }

    private void OnSfxChanged(float volume)
    {
        if (channel == Channel.Sfx)
            Apply();
    }

    private void Apply()
    {
        if (source == null)
            return;

        float setting = channel == Channel.Bgm ? GameSettings.BgmVolume : GameSettings.SfxVolume;
        source.volume = baseVolume * setting;
    }
}
