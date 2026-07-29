using UnityEngine;

/// <summary>
/// 빅뱅 효과음을 미디어 위치(0~1)에 맞춰 스크럽 재생하는 컴포넌트.
/// 테이프를 손으로 앞뒤로 돌리는 것과 같아서, 위치가 오르면 정방향으로 흐르고
/// 내려가면 역재생되며 멈추면 소리도 멈춘다.
///
/// <see cref="AudioSource.time"/> 을 매 프레임 대입하는 방식은 쓸 수 없다. 탐색할 때마다
/// 파형이 끊겨 팝 노이즈가 나고, 역재생도 표현되지 않는다. 그래서 원본 PCM 전체를 메모리에
/// 올려 두고 재생 헤드를 직접 움직인다(3.7초짜리라 720KB 남짓이다).
///
/// 위치 값은 <see cref="BigBangVisual"/> 이 영상에 쓰는 것과 같은 값을 그대로 받는다.
/// 그래야 보정 곡선·구간 설정이 바뀌어도 영상과 소리가 어긋나지 않는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class BigBangAudio : MonoBehaviour
{
    // AudioSource 가 재생 중이어야 OnAudioFilterRead 가 호출되므로, 내용을 덮어쓸 무음 클립을
    // 하나 물려 둔다. 전부 0이라 루프 이음매도 없다.
    private const int SilenceClipFrames = 4096;

    [Header("참조")]
    [Tooltip("스크럽할 원본 효과음. Load Type = Decompress On Load, Preload Audio Data = 켬 이어야 한다")]
    [SerializeField] private AudioClip sourceClip;

    [Tooltip("비워두면 같은 오브젝트의 AudioSource 를 찾아 쓴다")]
    [SerializeField] private AudioSource output;

    [Header("스크럽")]
    [Tooltip("출력 볼륨. AudioSource.volume 은 필터 앞단에 적용되므로 여기서 따로 곱한다")]
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    [Tooltip("재생 배속 상한. 위치가 순간적으로 크게 튀어도 이 배속 이상으로는 감지 않는다")]
    [SerializeField] private float maxRate = 3f;

    [Tooltip("배속 추정을 부드럽게 만드는 시간 상수(초). 프레임 간격 흔들림을 흡수한다. " +
             "길수록 부드럽지만 방향 전환이 굼떠진다")]
    [SerializeField] private float rateSmoothingSeconds = 0.06f;

    [Tooltip("헤드가 목표 위치에서 벌어졌을 때 따라잡는 데 걸리는 시간(초). " +
             "짧게 잡으면 배속이 요동치므로 넉넉히 둔다")]
    [SerializeField] private float catchUpSeconds = 0.25f;

    [Tooltip("이 배속 아래에서는 음소거한다. 위치가 멈춰 한 샘플이 유지될 때 생기는 잡음을 막는다")]
    [SerializeField] private float minAudibleRate = 0.02f;

    [Tooltip("음소거/복귀에 걸리는 시간(초). 짧을수록 반응이 빠르지만 딱딱 끊기는 느낌이 난다")]
    [SerializeField] private float gainFadeSeconds = 0.03f;

    // 원본 PCM 전체. 채널이 인터리브된 상태다.
    private float[] samples;
    private int sourceChannels;
    private int frameCount;

    // 필터가 도는 출력 샘플레이트. 원본 클립의 샘플레이트와 다를 수 있다.
    private int outputSampleRate;

    // 오디오 스레드만 만지는 재생 헤드(원본 샘플 프레임 단위, 소수 포함)와 볼륨 엔벨로프.
    private double head;
    private float gain;

    // 메인 스레드만 만지는 속도 추정 상태.
    private float lastPosition;
    private float smoothedVelocity;
    private bool velocityPrimed;

    // 메인 스레드가 쓰고 오디오 스레드가 읽는 값들.
    private volatile float targetPosition;
    private volatile float playbackRate;
    private volatile bool snapRequested;
    private volatile bool active;

    private AudioClip silenceClip;
    private bool ready;

    /// <summary>AudioSource 와 원본 클립을 확인하고 PCM 을 읽을 수 있는지 검사한다.</summary>
    private void Awake()
    {
        if (output == null)
            output = GetComponent<AudioSource>();

        if (sourceClip == null || output == null)
            return;

        // GetData 는 압축 상태로 메모리에 남는 로드 타입에서는 무음을 돌려준다.
        if (sourceClip.loadType != AudioClipLoadType.DecompressOnLoad)
        {
            Debug.LogWarning($"BigBangAudio: '{sourceClip.name}' 의 Load Type 이 Decompress On Load 가 " +
                             "아니라 PCM 을 읽을 수 없습니다. 임포트 설정을 확인하세요.", this);
            return;
        }

        if (sourceClip.loadState != AudioDataLoadState.Loaded && !sourceClip.LoadAudioData())
        {
            Debug.LogWarning($"BigBangAudio: '{sourceClip.name}' 의 오디오 데이터를 로드하지 못했습니다. " +
                             "Preload Audio Data 를 켜주세요.", this);
            return;
        }

        sourceChannels = sourceClip.channels;
        frameCount = sourceClip.samples;

        if (sourceChannels <= 0 || frameCount <= 1)
            return;

        samples = new float[frameCount * sourceChannels];
        sourceClip.GetData(samples, 0);

        outputSampleRate = AudioSettings.outputSampleRate;
        if (outputSampleRate <= 0)
            outputSampleRate = sourceClip.frequency;

        silenceClip = AudioClip.Create("BigBangSilence", SilenceClipFrames, 1, outputSampleRate, false);

        output.clip = silenceClip;
        output.loop = true;
        output.playOnAwake = false;
        output.spatialBlend = 0f;
        output.panStereo = 0f;
        output.pitch = 1f;

        ready = true;
    }

    /// <summary>런타임에 만든 무음 클립을 떼어내고 파괴한다.</summary>
    private void OnDestroy()
    {
        if (silenceClip == null)
            return;

        if (output != null && output.clip == silenceClip)
            output.clip = null;

        Destroy(silenceClip);
        silenceClip = null;
    }

    /// <summary>재생 시작. 지정된 위치로 즉시 점프한 뒤 무음에서 서서히 열린다.</summary>
    public void Begin(float normalizedPosition)
    {
        if (!ready)
            return;

        SnapTo(normalizedPosition);
        active = true;

        if (!output.isPlaying)
            output.Play();
    }

    /// <summary>
    /// 목표 위치(0~1)를 갱신한다. 매 프레임 호출해야 한다 — 호출 간격으로 재생 배속을 추정하므로
    /// 띄엄띄엄 부르면 배속이 그만큼 부정확해진다.
    /// </summary>
    public void SetPosition(float normalizedPosition)
    {
        if (!ready)
            return;

        float position = Mathf.Clamp01(normalizedPosition);
        float deltaTime = Time.deltaTime;

        // 스냅 직후 첫 호출은 위치가 크게 튀므로 속도 계산에서 제외한다.
        if (velocityPrimed && deltaTime > 0f)
        {
            float rawVelocity = (position - lastPosition) / deltaTime;

            // 프레임 간격이 흔들리면 속도도 같이 흔들린다. 1차 필터로 눌러 준다.
            float blend = rateSmoothingSeconds > 0f
                ? Mathf.Clamp01(deltaTime / rateSmoothingSeconds)
                : 1f;

            smoothedVelocity = Mathf.Lerp(smoothedVelocity, rawVelocity, blend);
        }

        velocityPrimed = true;
        lastPosition = position;

        targetPosition = position;

        // 정규화 속도(1/초) → 출력 샘플 하나당 나아갈 원본 샘플 수(= 재생 배속).
        // 출력 샘플레이트로 나누는 것이 핵심이다. 필터는 원본이 아니라 출력 스트림 위에서 돈다.
        playbackRate = smoothedVelocity * (frameCount - 1) / outputSampleRate;
    }

    /// <summary>재생을 멈추고 헤드를 지정 위치로 되돌린다. 재생 중이 아닐 때 불러도 안전하다(멱등).</summary>
    public void ResetAudio(float normalizedPosition)
    {
        if (!ready)
            return;

        active = false;
        output.Stop();
        SnapTo(normalizedPosition);
    }

    /// <summary>헤드를 지정 위치로 즉시 옮기고 속도 추정을 초기화한다.</summary>
    private void SnapTo(float normalizedPosition)
    {
        targetPosition = Mathf.Clamp01(normalizedPosition);
        playbackRate = 0f;

        lastPosition = targetPosition;
        smoothedVelocity = 0f;
        velocityPrimed = false;

        snapRequested = true;
    }

    /// <summary>
    /// 오디오 스레드에서 DSP 블록마다 정확히 한 번 호출된다. Unity API 는 부르지 않고 순수 계산만 한다.
    /// AudioSource 에 물려 둔 무음을 우리 파형으로 덮어쓴다.
    ///
    /// 훅 선택이 이 클래스에서 가장 중요한 결정이다. 처음에는
    /// <c>AudioClip.Create(..., stream: true, PCMReaderCallback)</c> 의 리더 콜백을 썼는데,
    /// 그 콜백은 호출 시점이 실제 재생 위치와 묶여 있지 않다. FMOD 가 스트림 버퍼를 채우려고
    /// 클립 길이만큼 통째로 미리 읽어 가므로, 한순간의 배속 값으로 생성된 긴 구간이 그대로
    /// 재생되고 다음 읽기에서 점프한다 — 소리가 뚝뚝 끊겼다.
    /// <c>OnAudioFilterRead</c> 는 DSP 블록당 한 번, 출력과 실시간 락스텝으로 불리므로 이 문제가 없다.
    /// </summary>
    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (!ready || channels <= 0)
            return;

        if (!active)
        {
            System.Array.Clear(data, 0, data.Length);
            return;
        }

        int wanted = data.Length / channels;
        if (wanted <= 0)
            return;

        int lastIndex = frameCount - 1;
        double target = (double)targetPosition * lastIndex;
        double rate = playbackRate;

        if (snapRequested)
        {
            snapRequested = false;
            head = target;
            gain = 0f;
        }

        // 위치 오차는 배속에 직접 반영하지 않고 천천히 흡수한다. 이 항이 세면 배속이 요동친다.
        double coupling = catchUpSeconds > 0f ? 1.0 / (catchUpSeconds * outputSampleRate) : 0.0;
        float gainStep = gainFadeSeconds > 0f ? 1f / (gainFadeSeconds * outputSampleRate) : 1f;
        float level = volume;

        for (int i = 0; i < wanted; i++)
        {
            double step = rate + (target - head) * coupling;
            if (step > maxRate)
                step = maxRate;
            else if (step < -maxRate)
                step = -maxRate;

            float targetGain = System.Math.Abs(step) < minAudibleRate ? 0f : 1f;
            gain = Mathf.MoveTowards(gain, targetGain, gainStep);

            head += step;
            if (head < 0.0)
                head = 0.0;
            else if (head > lastIndex)
                head = lastIndex;

            int i0 = (int)head;
            int i1 = i0 < lastIndex ? i0 + 1 : lastIndex;
            float frac = (float)(head - i0);

            int outOffset = i * channels;
            int a = i0 * sourceChannels;
            int b = i1 * sourceChannels;
            float amplitude = gain * level;

            // 출력 채널 수가 원본과 다를 수 있다(모노 원본 → 스테레오 출력 등).
            // 헤드가 샘플 사이에 걸쳐 있으므로 선형 보간해야 잡음이 끼지 않는다.
            for (int c = 0; c < channels; c++)
            {
                int sc = c < sourceChannels ? c : sourceChannels - 1;
                data[outOffset + c] = Mathf.Lerp(samples[a + sc], samples[b + sc], frac) * amplitude;
            }
        }
    }
}
