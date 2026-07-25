using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BGM 과 효과음을 한 곳에서 재생하는 매니저. 씬이 바뀌어도 살아남는다.
/// 볼륨은 <see cref="GameSettings"/> 값을 따라가므로 설정 창의 게이지를 움직이면
/// 재생 중인 소리에도 바로 반영된다.
///
/// BGM 은 채널 두 개를 번갈아 써서 곡을 바꿀 때 겹쳐 넘긴다(크로스페이드).
/// 시대별 BGM 은 <see cref="EraBgmDirector"/> 가, 씬 단위 BGM 은 <see cref="SceneBgm"/> 이
/// 이 매니저의 <see cref="PlayBgm"/> 을 불러서 튼다.
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

    [Tooltip("startupBgm 의 원본 크기. 곡마다 녹음 크기가 달라서 곡별로 잡는다")]
    [Range(0f, 1f)]
    [SerializeField] private float startupBgmVolume = 1f;

    [Tooltip("BGM 전체 크기. 설정 게이지 값과 곡별 크기에 이 값을 곱한다")]
    [Range(0f, 1f)]
    [SerializeField] private float bgmBaseVolume = 1f;

    [Tooltip("곡을 바꿀 때 겹쳐 넘기는 시간(초). 0이면 즉시 갈아탄다")]
    [Min(0f)]
    [SerializeField] private float bgmFadeDuration = 1.2f;

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

    // BGM 채널 두 개. active 가 지금 곡, fading 이 물러나는 중인 곡이다.
    // bgmAltSource 는 크로스페이드용으로 런타임에 만드는 두 번째 채널.
    private AudioSource bgmAltSource;
    private AudioSource activeBgm;
    private AudioSource fadingBgm;

    // 지금 곡 / 물러나는 곡의 곡별 크기. 게이지 값에 곱해 최종 볼륨을 만든다.
    private float bgmTrackVolume = 1f;
    private float fadingTrackVolume = 1f;

    private Coroutine bgmFadeRoutine;

    /// <summary>씬에 하나 놓여 있으면 그것을, 없으면 null 을 준다.</summary>
    public static AudioManager Instance => instance;

    /// <summary>지금 재생 중인 BGM 클립. 없으면 null.</summary>
    public AudioClip CurrentBgm => activeBgm != null ? activeBgm.clip : null;

    /// <summary>
    /// 매니저를 반드시 하나 확보한다. 타이틀을 거치지 않고 플레이 씬을 바로 열어도
    /// BGM 이 나오도록, 없으면 그 자리에서 만들어 준다.
    /// Awake 가 아니라 Start 이후에 부르는 것이 안전하다.
    /// </summary>
    public static AudioManager EnsureInstance()
    {
        if (instance != null)
            return instance;

        var found = FindFirstObjectByType<AudioManager>();
        if (found != null)
            return found;

        var go = new GameObject("AudioManager (auto)");
        return go.AddComponent<AudioManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // 이미 따라온 매니저가 있다. 이 씬에 지정된 시작 곡만 넘겨주고 사라진다 —
            // 엔딩·갤러리를 지나 타이틀로 돌아왔을 때 타이틀 곡이 다시 흐르게 하려는 것이다.
            // (startupBgm 은 매니저가 씬을 넘어 살아남기 때문에 원래는 게임당 한 번만 돈다)
            if (startupBgm != null)
                instance.PlayBgm(startupBgm, startupBgmVolume, restartIfSame: false, fadeDuration: bgmFadeDuration);

            Destroy(gameObject);
            return;
        }

        instance = this;

        if (dontDestroyOnLoad)
        {
            // DontDestroyOnLoad 는 루트 오브젝트에만 통한다. 자식으로 놓여 있으면 먼저 떼어낸다.
            if (transform.parent != null)
                transform.SetParent(null, worldPositionStays: true);

            DontDestroyOnLoad(gameObject);
        }

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
            PlayBgm(startupBgm, startupBgmVolume, fadeDuration: 0f);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    /// <summary>
    /// BGM 을 튼다. 이미 같은 곡이 돌고 있으면 크기만 맞추고 넘어간다.
    /// </summary>
    /// <param name="clip">틀 곡. null 이면 아무것도 하지 않는다(곡이 없는 시대는 직전 곡을 유지한다).</param>
    /// <param name="trackVolume">이 곡의 원본 크기(0~1). 설정 게이지 값에 곱해진다.</param>
    /// <param name="restartIfSame">같은 곡이어도 처음부터 다시 튼다.</param>
    /// <param name="fadeDuration">겹쳐 넘기는 시간(초). 음수면 인스펙터의 기본값을 쓴다.</param>
    public void PlayBgm(AudioClip clip, float trackVolume = 1f, bool restartIfSame = false, float fadeDuration = -1f)
    {
        if (clip == null || activeBgm == null)
            return;

        trackVolume = Mathf.Clamp01(trackVolume);

        if (!restartIfSame && activeBgm.clip == clip && activeBgm.isPlaying)
        {
            // 같은 곡이면 끊지 않는다 — 크기만 갱신한다.
            bgmTrackVolume = trackVolume;
            ApplyBgmVolume(GameSettings.BgmVolume);
            return;
        }

        float duration = fadeDuration >= 0f ? fadeDuration : bgmFadeDuration;

        BeginBgmTransition();

        bgmTrackVolume = trackVolume;
        activeBgm.clip = clip;
        activeBgm.loop = true;
        activeBgm.volume = duration > 0f ? 0f : TargetBgmVolume();
        activeBgm.Play();

        StartBgmFade(duration);
    }

    /// <summary>BGM 을 멈춘다. fadeDuration 이 음수면 인스펙터 기본값으로 서서히 줄인다.</summary>
    public void StopBgm(float fadeDuration = 0f)
    {
        float duration = fadeDuration >= 0f ? fadeDuration : bgmFadeDuration;

        // 지금 곡을 물러나는 쪽으로 넘기고, 새로 트는 곡 없이 페이드만 돌린다.
        BeginBgmTransition();
        bgmTrackVolume = 0f;

        StartBgmFade(duration);
    }

    /// <summary>
    /// 새 곡을 받을 준비를 한다. 지금 곡이 있으면 물러나는 쪽으로 밀고 빈 채널을 앞에 세운다.
    /// 처음 트는 곡이면 채널을 바꾸지 않는다 — 인스펙터에서 잡아둔 AudioSource 를 그대로 쓴다.
    /// </summary>
    private void BeginBgmTransition()
    {
        if (bgmFadeRoutine != null)
        {
            StopCoroutine(bgmFadeRoutine);
            bgmFadeRoutine = null;
        }

        // 이전 페이드가 덜 끝났으면 그때 물러나던 곡은 여기서 잘라낸다. 채널이 두 개뿐이라
        // 세 곡이 겹칠 수는 없다 — 가장 오래된 곡을 버리는 게 맞다.
        StopChannel(fadingBgm);
        fadingBgm = null;

        if (activeBgm == null || (!activeBgm.isPlaying && activeBgm.clip == null))
            return;

        fadingBgm = activeBgm;
        fadingTrackVolume = bgmTrackVolume;
        activeBgm = fadingBgm == bgmSource ? bgmAltSource : bgmSource;
    }

    private void StartBgmFade(float duration)
    {
        if (duration <= 0f)
        {
            StopChannel(fadingBgm);
            fadingBgm = null;

            if (activeBgm != null)
                activeBgm.volume = TargetBgmVolume();

            return;
        }

        bgmFadeRoutine = StartCoroutine(CrossFadeRoutine(duration));
    }

    /// <summary>
    /// 물러나는 곡을 줄이면서 새 곡을 키운다.
    /// 설정 창이 열려 있으면 <c>Time.timeScale</c> 이 0이므로 스케일 안 받는 시간을 쓴다.
    /// 매 프레임 게이지 값을 다시 읽어서, 페이드 중에 게이지를 움직여도 그대로 따라간다.
    /// </summary>
    private IEnumerator CrossFadeRoutine(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (activeBgm != null)
                activeBgm.volume = TargetBgmVolume() * t;

            if (fadingBgm != null)
                fadingBgm.volume = GameSettings.BgmVolume * bgmBaseVolume * fadingTrackVolume * (1f - t);

            yield return null;
        }

        StopChannel(fadingBgm);
        fadingBgm = null;

        if (activeBgm != null)
            activeBgm.volume = TargetBgmVolume();

        bgmFadeRoutine = null;
    }

    private static void StopChannel(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        source.clip = null;
        source.volume = 0f;
    }

    /// <summary>지금 곡이 최종적으로 가져야 할 볼륨.</summary>
    private float TargetBgmVolume() => GameSettings.BgmVolume * bgmBaseVolume * bgmTrackVolume;

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

    /// <summary>
    /// <see cref="PlaySfx"/> 가 돌려준 채널 하나만 멈춘다.
    /// 긴 클립이 미니게임 밖까지 이어지는 걸 끊을 때 쓴다.
    /// fadeDuration 을 주면 그만큼 볼륨을 줄이며 끄므로 뚝 끊기는 느낌이 덜하다.
    ///
    /// 채널은 돌려 쓰기 때문에, 부르는 쪽이 재생시킨 클립이 아직 그 채널에 걸려 있을 때만 손댄다.
    /// </summary>
    public void StopSfx(AudioSource source, float fadeDuration = 0f)
    {
        if (source == null || !source.isPlaying || !sfxSources.Contains(source))
            return;

        if (fadeDuration <= 0f)
        {
            source.Stop();
            return;
        }

        StartCoroutine(FadeOutRoutine(source, fadeDuration));
    }

    private IEnumerator FadeOutRoutine(AudioSource source, float duration)
    {
        AudioClip clip = source.clip;
        float startVolume = source.volume;
        float time = 0f;

        while (time < duration)
        {
            // 페이드 도중 채널이 다른 소리에 넘어갔으면 그 소리를 죽이지 않고 물러난다.
            if (source == null || source.clip != clip || !source.isPlaying)
                yield break;

            // 설정 창이 열려 게임이 멈춰 있어도 페이드는 진행돼야 한다.
            time += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, time / duration);
            yield return null;
        }

        if (source != null && source.clip == clip)
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
        else if (dontDestroyOnLoad && !bgmSource.transform.IsChildOf(transform))
        {
            // 씬에 따로 놓인 AudioSource 를 참조하면 이 매니저만 살아남고 소스는 씬과 함께
            // 사라져서 다음 씬에서 BGM 이 끊긴다. 계층 밖에 있으면 이쪽으로 데려온다.
            bgmSource.transform.SetParent(transform, worldPositionStays: false);
        }

        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;

        // 곡을 겹쳐 넘기려면 채널이 둘 필요하다. 두 번째는 항상 여기서 만든다.
        var altGo = new GameObject("BGM_Alt");
        altGo.transform.SetParent(transform, false);

        bgmAltSource = altGo.AddComponent<AudioSource>();
        bgmAltSource.playOnAwake = false;
        bgmAltSource.loop = true;
        bgmAltSource.spatialBlend = 0f;
        bgmAltSource.volume = 0f;
        bgmAltSource.outputAudioMixerGroup = bgmSource.outputAudioMixerGroup;

        activeBgm = bgmSource;

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

    /// <summary>설정 창의 BGM 게이지를 움직였을 때 지금 곡에 바로 반영한다.</summary>
    private void ApplyBgmVolume(float volume)
    {
        // 겹쳐 넘기는 중이면 코루틴이 매 프레임 다시 계산하므로 건드리지 않는다.
        if (bgmFadeRoutine != null)
            return;

        if (activeBgm != null)
            activeBgm.volume = volume * bgmBaseVolume * bgmTrackVolume;
    }

    private void ApplySfxVolume(float volume)
    {
        for (int i = 0; i < sfxSources.Count; i++)
            sfxSources[i].volume = volume * sfxBaseVolume * sfxScales[i];
    }
}
