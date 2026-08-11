using System.Collections;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 빅뱅 영상을 0~1 진행도에 맞춰 스크럽(임의 위치 탐색)하는 연출 컴포넌트.
/// 영상이 스스로 흐르지 않고 <see cref="SetProgress"/> 로 지정된 위치에 멈춰 있는 것이 핵심이다.
/// 게임 로직(<see cref="BigBangMiniGame"/>)과 분리해 두었으므로,
/// 스크럽이 버거우면 이 컴포넌트만 이미지 시퀀스 방식 등으로 갈아끼우면 된다.
///
/// 탐색은 프레임 번호가 아니라 <see cref="VideoPlayer.time"/>(초) 로 한다.
/// Web(WebGL) 은 프레임 정밀도를 지원하지 않아 <c>frameCount</c> 가 0 을 돌려주고
/// <c>frame</c> 대입이 무시되기 때문이다. 초 단위 탐색은 모든 플랫폼에서 동작하므로
/// 플랫폼별로 경로를 나누지 않는다.
///
/// 탐색 요청은 반드시 <b>한 번에 하나만</b> 띄운다. Web 의 비디오는 브라우저 &lt;video&gt;
/// 엘리먼트이고, 탐색이 끝나지 않은 동안에는 화면 갱신이 통째로 거부된다. 매 프레임
/// 새 탐색을 걸면 그림이 영원히 올라오지 않는다. 자세한 근거는 docs/BigBang_MiniGame.md 참고.
/// </summary>
[DisallowMultipleComponent]
public class BigBangVisual : MonoBehaviour
{
    // 영상 한 프레임(30fps)에 해당하는 시간. 이보다 작은 차이는 탐색해 봐야 같은 그림이다.
    private const double SeekEpsilon = 1.0 / 30.0;

    // 탐색 완료 통지가 오지 않을 때 다음 탐색을 풀어 주기까지의 시간(초).
    // seekCompleted 는 플랫폼 구현에 기대는 이벤트라, 한 번 유실되면 영상이 그 자리에
    // 영구히 멈춘다. 그 최악을 막는 안전장치이므로 튜닝 값이 아니다.
    private const float SeekTimeout = 0.5f;

    [Header("참조")]
    [Tooltip("비워두면 같은 오브젝트의 VideoPlayer 를 찾아 쓴다")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Tooltip("영상과 같은 위치로 스크럽할 효과음. 비워두면 무음으로 동작한다")]
    [SerializeField] private BigBangAudio audioScrub;

    [Tooltip("StreamingAssets 안의 영상 파일 이름. Web 은 VideoClip 을 지원하지 않아 URL 로 읽는다")]
    [SerializeField] private string videoFileName = "BigBang.mp4";

    [Header("구간")]
    [Tooltip("진행도 0 이 대응할 영상 위치(0~1). 앞쪽 암전 구간을 잘라낼 때 쓴다")]
    [SerializeField, Range(0f, 1f)] private float startRatio = 0f;

    [Tooltip("진행도 1 이 대응할 영상 위치(0~1)")]
    [SerializeField, Range(0f, 1f)] private float endRatio = 1f;

    [Tooltip("진행도 → 영상 위치 보정 곡선. 영상의 시각적 변화가 뒤쪽에 몰려 있으면 " +
             "앞 구간을 빠르게 지나가도록 위로 볼록한 곡선을 준다. 직선이면 보정 없음")]
    [SerializeField] private AnimationCurve progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private double duration;
    private double lastSeekTime = -1.0;
    private float pendingProgress;
    private bool prepared;

    // 탐색이 하나 떠 있는 동안에는 새 요청을 보내지 않는다. 최신 목표는 pendingProgress 에
    // 남으므로, 완료 통지를 받은 뒤 그 값으로 한 번만 따라잡는다(중간 값은 버린다).
    private bool seekPending;
    private float seekIssuedTime;

    /// <summary>영상이 스스로 재생되지 않도록 막고, 소스를 StreamingAssets 의 URL 로 고정한다.</summary>
    private void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (videoPlayer == null)
            return;

        // VideoClip 은 Web 에서 지원되지 않는다. StreamingAssets 에 원본을 두고 URL 로 읽는다.
        // Path.Combine 은 에디터에서 역슬래시를 섞으므로 직접 이어 붙인다.
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = $"{Application.streamingAssetsPath}/{videoFileName}";

        // 스스로 재생되면 안 된다. 재생 위치는 전적으로 SetProgress 가 지정한다.
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.skipOnDrop = true;
        videoPlayer.waitForFirstFrame = true;

        // 탐색 완료는 Begin 이 아니라 여기서 구독한다. ResetVisual 이 Begin 보다 먼저
        // 탐색을 걸 수 있어서, 구독을 재생 시점에 묶으면 그 첫 탐색의 완료를 놓친다.
        videoPlayer.seekCompleted += OnSeekCompleted;
    }

    /// <summary>탐색 완료 구독을 해제한다. VideoPlayer 와 수명이 같으므로 여기서 정리한다.</summary>
    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.seekCompleted -= OnSeekCompleted;
    }

    /// <summary>준비 완료 구독을 해제하고 탐색 위치 캐시를 비운다.</summary>
    private void OnDisable()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnPrepared;

            // 첫 프레임 확보용 재생이 코루틴 도중에 잘렸을 수 있다. 멈춰 두지 않으면
            // 비활성 상태에서 영상만 계속 흘러간다.
            videoPlayer.Pause();
        }

        prepared = false;
        lastSeekTime = -1.0;
        seekPending = false;
    }

    /// <summary>
    /// 재생 시작. 영상을 준비시키고 첫 프레임(어둠)에서 멈춘 상태로 만든다.
    /// 준비는 비동기라, 완료 전에 들어온 <see cref="SetProgress"/> 값은 보관했다가 완료 시 반영한다.
    /// </summary>
    public void Begin()
    {
        pendingProgress = 0f;
        lastSeekTime = -1.0;
        seekPending = false;

        // 효과음은 영상 준비(Prepare)를 기다릴 필요가 없으므로 먼저 띄운다.
        if (audioScrub != null)
            audioScrub.Begin(MapProgress(0f));

        if (videoPlayer == null)
            return;

        if (videoPlayer.isPrepared)
        {
            OnPrepared(videoPlayer);
            return;
        }

        videoPlayer.prepareCompleted -= OnPrepared;
        videoPlayer.prepareCompleted += OnPrepared;
        videoPlayer.Prepare();
    }

    /// <summary>진행도(0~1)에 해당하는 프레임을 보여준다. 매 프레임 호출해도 된다.</summary>
    public void SetProgress(float progress)
    {
        pendingProgress = Mathf.Clamp01(progress);

        // 효과음은 영상 준비 여부와 무관하게, 그리고 프레임 격자로 스냅되기 전의 연속적인
        // 위치를 받아야 한다. ApplyProgress 안(프레임 중복 체크 뒤)에 두면 30fps 단위로
        // 끊긴 위치가 넘어가 소리가 계단처럼 들린다.
        if (audioScrub != null)
            audioScrub.SetPosition(MapProgress(pendingProgress));

        if (prepared)
            ApplyProgress(pendingProgress, false);
    }

    /// <summary>영상을 첫 프레임(어둠)으로 되돌린다. 준비 전에 호출해도 안전하다(멱등).</summary>
    public void ResetVisual()
    {
        pendingProgress = 0f;
        lastSeekTime = -1.0;
        seekPending = false;

        if (audioScrub != null)
            audioScrub.ResetAudio(MapProgress(0f));

        if (videoPlayer == null)
            return;

        videoPlayer.prepareCompleted -= OnPrepared;

        if (!videoPlayer.isPrepared)
        {
            prepared = false;
            return;
        }

        prepared = true;
        duration = videoPlayer.length;
        videoPlayer.Pause();
        ApplyProgress(0f, true);
    }

    /// <summary>영상 준비가 끝났다. 첫 프레임을 강제로 렌더한 뒤 밀린 진행도를 반영한다.</summary>
    private void OnPrepared(VideoPlayer source)
    {
        prepared = true;
        duration = source.length;

        // Pause 만으로는 첫 프레임이 렌더되지 않으므로, 한 번 재생해 프레임을 한 장 뽑아낸 뒤 멈춘다.
        // 이 처리는 Web 에서 특히 중요하다. Unity 의 Web 비디오 구현은 프레임이 실제로 한 번
        // 표시되기 전까지 텍스처 업로드를 통째로 거부하는데, 그 표시를 일으키는 유일한 방법이
        // 재생이다. 엘리먼트는 항상 muted 로 생성되므로 브라우저 자동재생 정책에도 걸리지 않는다.
        source.Play();

        // 같은 프레임에 바로 Pause 하면 브라우저가 프레임을 내놓기 전에 멈출 수 있어 한 프레임 준다.
        // 그동안에는 탐색을 걸지 않는다 — 첫 프레임이 표시되기 전에 탐색이 끼면 그 표시가 밀린다.
        if (isActiveAndEnabled)
        {
            StartCoroutine(PauseAfterFirstFrame());
            return;
        }

        source.Pause();
        ApplyProgress(pendingProgress, true);
    }

    /// <summary>첫 프레임이 올라올 틈을 한 프레임 준 뒤 멈추고, 그동안 밀린 진행도를 반영한다.</summary>
    private IEnumerator PauseAfterFirstFrame()
    {
        yield return null;

        if (videoPlayer == null)
            yield break;

        videoPlayer.Pause();
        ApplyProgress(pendingProgress, true);
    }

    /// <summary>탐색이 끝났다. 그사이 목표가 움직였을 수 있으므로 최신 값으로 한 번 더 따라간다.</summary>
    private void OnSeekCompleted(VideoPlayer source)
    {
        seekPending = false;

        if (prepared)
            ApplyProgress(pendingProgress, false);
    }

    /// <summary>
    /// 진행도(0~1)를 영상과 효과음이 공유하는 미디어 위치(0~1)로 변환한다.
    /// 영상의 밝기 변화가 균등하지 않으므로 곡선으로 한 번 보정한 뒤 구간에 매핑한다.
    /// 두 매체가 반드시 이 함수 하나만 거치게 해서 서로 어긋날 여지를 없앤다.
    /// </summary>
    private float MapProgress(float progress)
    {
        float shaped = progressCurve != null && progressCurve.length > 0
            ? Mathf.Clamp01(progressCurve.Evaluate(progress))
            : progress;

        return Mathf.Lerp(startRatio, endRatio, shaped);
    }

    /// <summary>진행도를 재생 시각(초)으로 바꿔 영상을 스크럽한다. 같은 그림이면 탐색 요청을 건너뛴다.</summary>
    private void ApplyProgress(float progress, bool force)
    {
        if (videoPlayer == null || duration <= 0.0)
            return;

        // 탐색이 아직 떠 있으면 새로 걸지 않는다. 끝나기 전에 다음 요청을 얹으면 탐색이
        // 계속 갱신되기만 하고 완료되지 않아, 브라우저가 화면 갱신을 영구히 보류한다.
        // 버린 중간 값은 OnSeekCompleted 가 pendingProgress 로 따라잡는다.
        if (seekPending && Time.unscaledTime - seekIssuedTime < SeekTimeout)
            return;

        double target = MapProgress(progress) * duration;

        // 끝에 정확히 닿으면 재생 종료로 처리돼 그림이 날아갈 수 있어 한 프레임 물려 둔다.
        double limit = duration - SeekEpsilon;
        if (limit < 0.0)
            limit = 0.0;

        if (target < 0.0)
            target = 0.0;
        else if (target > limit)
            target = limit;

        // 값이 그대로인데 매 프레임 대입하면 탐색 요청이 쌓여 버벅인다.
        if (!force && System.Math.Abs(target - lastSeekTime) < SeekEpsilon)
            return;

        lastSeekTime = target;
        seekPending = true;
        seekIssuedTime = Time.unscaledTime;
        videoPlayer.time = target;
    }
}
