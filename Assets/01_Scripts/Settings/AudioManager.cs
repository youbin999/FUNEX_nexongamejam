using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BGM 과 효과음을 한 곳에서 재생하는 매니저. 씬이 바뀌어도 살아남는다.
/// 볼륨은 <see cref="GameSettings"/> 값을 따라가므로 설정 창의 게이지를 움직이면
/// 재생 중인 소리에도 바로 반영된다.
///
/// 효과음은 채널을 미리 만들어 두고 돌려 쓴다. 겹쳐서 나야 하는 소리가 많으면
/// <see cref="sfxChannels"/> 를 늘리면 된다.
/// </summary>
[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    private static AudioManager instance;

    [Header("BGM")]
    [Tooltip("비워두면 자식으로 하나 만들어 쓴다")]
    [SerializeField] private AudioSource bgmSource;

    [Tooltip("시작할 때 자동으로 틀 BGM. 없으면 아무것도 재생하지 않는다")]
    [SerializeField] private AudioClip startupBgm;

    [Tooltip("BGM 원본 크기. 설정 게이지 값에 이 값을 곱한다")]
    [Range(0f, 1f)]
    [SerializeField] private float bgmBaseVolume = 1f;

    [Header("효과음")]
    [Tooltip("동시에 겹쳐 재생할 수 있는 효과음 개수")]
    [SerializeField] private int sfxChannels = 8;

    [Tooltip("효과음 원본 크기. 설정 게이지 값에 이 값을 곱한다")]
    [Range(0f, 1f)]
    [SerializeField] private float sfxBaseVolume = 1f;

    [Header("옵션")]
    [Tooltip("씬이 바뀌어도 유지한다")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    private readonly List<AudioSource> sfxSources = new List<AudioSource>();

    // 채널마다 재생할 때 받은 볼륨 배율. 게이지를 움직이면 이 값에 다시 곱해 갱신한다.
    private readonly List<float> sfxScales = new List<float>();

    private int nextChannel;

    /// <summary>씬에 하나 놓여 있으면 그것을, 없으면 null 을 준다.</summary>
    public static AudioManager Instance => instance;

    /// <summary>지금 재생 중인 BGM 클립. 없으면 null.</summary>
    public AudioClip CurrentBgm => bgmSource != null ? bgmSource.clip : null;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        BuildSources();
        ApplyBgmVolume(GameSettings.BgmVolume);
        ApplySfxVolume(GameSettings.SfxVolume);
    }

    private void OnEnable()
    {
        GameSettings.BgmVolumeChanged += ApplyBgmVolume;
        GameSettings.SfxVolumeChanged += ApplySfxVolume;
    }

    private void OnDisable()
    {
        GameSettings.BgmVolumeChanged -= ApplyBgmVolume;
        GameSettings.SfxVolumeChanged -= ApplySfxVolume;
    }

    private void Start()
    {
        if (startupBgm != null)
            PlayBgm(startupBgm);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    /// <summary>BGM 을 튼다. 이미 같은 곡이 재생 중이면 아무것도 하지 않는다.</summary>
    public void PlayBgm(AudioClip clip, bool restartIfSame = false)
    {
        if (clip == null || bgmSource == null)
            return;

        if (!restartIfSame && bgmSource.clip == clip && bgmSource.isPlaying)
            return;

        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    /// <summary>BGM 을 멈춘다.</summary>
    public void StopBgm()
    {
        if (bgmSource != null)
            bgmSource.Stop();
    }

    /// <summary>효과음을 한 번 재생한다. volumeScale 로 이 소리만 크게/작게 낼 수 있다.</summary>
    public AudioSource PlaySfx(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
    {
        if (clip == null || sfxSources.Count == 0)
            return null;

        AudioSource source = TakeChannel();
        int index = sfxSources.IndexOf(source);
        if (index >= 0)
            sfxScales[index] = Mathf.Max(volumeScale, 0f);

        source.clip = clip;
        source.pitch = pitch;
        source.volume = GameSettings.SfxVolume * sfxBaseVolume * Mathf.Max(volumeScale, 0f);
        source.Play();
        return source;
    }

    /// <summary>재생 중인 효과음을 전부 멈춘다.</summary>
    public void StopAllSfx()
    {
        foreach (AudioSource source in sfxSources)
            source.Stop();
    }

    /// <summary>비어 있는 채널을 고른다. 전부 재생 중이면 가장 오래된 채널을 뺏는다.</summary>
    private AudioSource TakeChannel()
    {
        for (int i = 0; i < sfxSources.Count; i++)
        {
            if (!sfxSources[i].isPlaying)
                return sfxSources[i];
        }

        AudioSource source = sfxSources[nextChannel];
        nextChannel = (nextChannel + 1) % sfxSources.Count;
        return source;
    }

    private void BuildSources()
    {
        if (bgmSource == null)
        {
            var go = new GameObject("BGM");
            go.transform.SetParent(transform, false);
            bgmSource = go.AddComponent<AudioSource>();
        }

        bgmSource.playOnAwake = false;
        bgmSource.loop = true;

        int count = Mathf.Max(sfxChannels, 1);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"SFX_{i}");
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;

            sfxSources.Add(source);
            sfxScales.Add(1f);
        }
    }

    private void ApplyBgmVolume(float volume)
    {
        if (bgmSource != null)
            bgmSource.volume = volume * bgmBaseVolume;
    }

    private void ApplySfxVolume(float volume)
    {
        for (int i = 0; i < sfxSources.Count; i++)
            sfxSources[i].volume = volume * sfxBaseVolume * sfxScales[i];
    }
}
