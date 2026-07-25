using UnityEngine;

/// <summary>
/// 미니게임 이벤트에 효과음을 붙이는 어댑터.
/// 미니게임 스크립트를 고치지 않고, 인스펙터의 UnityEvent(onHit / onRub / onClear / onFail 등)에
/// 이 컴포넌트의 <see cref="Play"/> 를 연결해서 쓴다.
///
/// 소리는 <see cref="AudioManager"/> 를 거쳐 재생되므로 설정 창의 효과음 게이지가 그대로 먹는다.
/// AudioManager 가 없는 씬(미니게임 단독 테스트 등)에서는 자기 AudioSource 를 만들어 대신 재생한다.
///
/// 프리팹 안에 빈 오브젝트를 만들어 붙이고 Sfx_Hit / Sfx_Clear 처럼 이름을 주면 배선할 때 찾기 쉽다.
/// </summary>
[DisallowMultipleComponent]
public class SfxPlayer : MonoBehaviour
{
    [Header("클립")]
    [Tooltip("재생할 소리. 여러 개 넣으면 매번 무작위로 하나 고른다 — 연타음이 단조로워지지 않는다")]
    [SerializeField] private AudioClip[] clips;

    [Header("크기 / 높이")]
    [Tooltip("이 소리의 원본 크기. 설정 창의 효과음 게이지 값에 이 값을 곱한다")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;

    [Tooltip("기본 음 높이")]
    [SerializeField] private float pitch = 1f;

    [Tooltip("재생할 때마다 음 높이를 이만큼 무작위로 흔든다. 0이면 항상 같은 높이로 난다")]
    [Range(0f, 0.5f)]
    [SerializeField] private float pitchJitter = 0.06f;

    [Header("연타")]
    [Tooltip("이 간격(초) 안에 다시 불리면 무시한다. 연타할 때 소리가 뭉개지는 걸 막는다. 0이면 전부 재생")]
    [SerializeField] private float minInterval = 0.03f;

    [Tooltip("PlayStep 으로 재생할 때 한 단계마다 올라가는 음 높이")]
    [SerializeField] private float stepPitchRise = 0.03f;

    [Tooltip("PlayStep 의 음 높이가 이만큼 오르면 더는 올라가지 않는다")]
    [SerializeField] private float stepPitchCap = 0.5f;

    // AudioManager 가 없을 때만 만들어 쓰는 대비책.
    private AudioSource fallbackSource;

    private float lastPlayTime = float.NegativeInfinity;
    private int lastIndex = -1;

    private void OnEnable()
    {
        // 프리팹이 풀에서 다시 꺼내질 때 지난 판의 간격 제한과 직전 클립 기록이 남지 않게 한다.
        lastPlayTime = float.NegativeInfinity;
        lastIndex = -1;
    }

    /// <summary>
    /// 클립 하나를 무작위로 골라 재생한다.
    /// 인수가 없어서 어떤 UnityEvent 에든 연결할 수 있다 — 기본으로 이걸 쓴다.
    /// </summary>
    public void Play()
    {
        PlayInternal(PickIndex(), pitch);
    }

    /// <summary>정해진 순번의 클립을 재생한다. 범위를 벗어나면 가장 가까운 것으로 맞춘다.</summary>
    public void PlayAt(int index)
    {
        PlayInternal(index, pitch);
    }

    /// <summary>
    /// 연타 횟수에 따라 음이 점점 올라가게 재생한다.
    /// 횟수를 넘겨주는 이벤트(onHit / onRub / onMash / onDig 등)에 그대로 연결하면 된다.
    /// </summary>
    public void PlayStep(int step)
    {
        float rise = Mathf.Min(Mathf.Max(step - 1, 0) * stepPitchRise, stepPitchCap);
        PlayInternal(PickIndex(), pitch + rise);
    }

    private void PlayInternal(int index, float basePitch)
    {
        if (clips == null || clips.Length == 0)
            return;

        // 설정 창이 열려 게임이 멈춰 있어도 간격이 제대로 재지도록 스케일 안 받는 시간을 쓴다.
        if (minInterval > 0f && Time.unscaledTime - lastPlayTime < minInterval)
            return;

        AudioClip clip = clips[Mathf.Clamp(index, 0, clips.Length - 1)];
        if (clip == null)
            return;

        lastPlayTime = Time.unscaledTime;

        float finalPitch = basePitch + Random.Range(-pitchJitter, pitchJitter);

        AudioManager manager = AudioManager.Instance;
        if (manager != null)
        {
            manager.PlaySfx(clip, volume, finalPitch);
            return;
        }

        PlayOnFallback(clip, finalPitch);
    }

    /// <summary>같은 소리가 연달아 나지 않게 고른다. 클립이 하나뿐이면 그냥 그것.</summary>
    private int PickIndex()
    {
        if (clips.Length <= 1)
            return 0;

        int index = Random.Range(0, clips.Length);
        if (index == lastIndex)
            index = (index + 1) % clips.Length;

        lastIndex = index;
        return index;
    }

    /// <summary>
    /// AudioManager 가 없는 씬을 위한 대비책. 자기 AudioSource 로 직접 재생한다.
    /// 이 경로에서는 게이지 연동을 대신 해줄 곳이 없으므로 설정값을 여기서 직접 곱한다.
    /// </summary>
    private void PlayOnFallback(AudioClip clip, float finalPitch)
    {
        if (fallbackSource == null)
        {
            fallbackSource = gameObject.AddComponent<AudioSource>();
            fallbackSource.playOnAwake = false;
            fallbackSource.loop = false;

            // 2D 로 재생한다. 3D 로 두면 카메라와의 거리에 따라 소리가 죽는다.
            fallbackSource.spatialBlend = 0f;
        }

        fallbackSource.pitch = finalPitch;
        fallbackSource.PlayOneShot(clip, GameSettings.SfxVolume * volume);
    }
}
