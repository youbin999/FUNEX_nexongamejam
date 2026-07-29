using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 빅뱅 영상을 0~1 진행도에 맞춰 스크럽(임의 프레임 탐색)하는 연출 컴포넌트.
/// 영상이 스스로 흐르지 않고 <see cref="SetProgress"/> 로 지정된 프레임에 멈춰 있는 것이 핵심이다.
/// 게임 로직(<see cref="BigBangMiniGame"/>)과 분리해 두었으므로,
/// 스크럽이 버거우면 이 컴포넌트만 이미지 시퀀스 방식 등으로 갈아끼우면 된다.
/// </summary>
[DisallowMultipleComponent]
public class BigBangVisual : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("비워두면 같은 오브젝트의 VideoPlayer 를 찾아 쓴다")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Tooltip("영상과 같은 위치로 스크럽할 효과음. 비워두면 무음으로 동작한다")]
    [SerializeField] private BigBangAudio audioScrub;

    [Header("구간")]
    [Tooltip("진행도 0 이 대응할 영상 위치(0~1). 앞쪽 암전 구간을 잘라낼 때 쓴다")]
    [SerializeField, Range(0f, 1f)] private float startRatio = 0f;

    [Tooltip("진행도 1 이 대응할 영상 위치(0~1)")]
    [SerializeField, Range(0f, 1f)] private float endRatio = 1f;

    [Tooltip("진행도 → 영상 위치 보정 곡선. 영상의 시각적 변화가 뒤쪽에 몰려 있으면 " +
             "앞 구간을 빠르게 지나가도록 위로 볼록한 곡선을 준다. 직선이면 보정 없음")]
    [SerializeField] private AnimationCurve progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private long frameCount;
    private long lastFrame = -1;
    private float pendingProgress;
    private bool prepared;

    /// <summary>영상이 스스로 재생되지 않도록 막는다. 프레임은 SetProgress 가 전적으로 지정한다.</summary>
    private void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (videoPlayer == null)
            return;

        // 스스로 재생되면 안 된다. 프레임은 전적으로 SetProgress 가 지정한다.
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.skipOnDrop = true;
        videoPlayer.waitForFirstFrame = true;
    }

    /// <summary>준비 완료 구독을 해제하고 프레임 캐시를 비운다.</summary>
    private void OnDisable()
    {
        if (videoPlayer != null)
            videoPlayer.prepareCompleted -= OnPrepared;

        prepared = false;
        lastFrame = -1;
    }

    /// <summary>
    /// 재생 시작. 영상을 준비시키고 첫 프레임(어둠)에서 멈춘 상태로 만든다.
    /// 준비는 비동기라, 완료 전에 들어온 <see cref="SetProgress"/> 값은 보관했다가 완료 시 반영한다.
    /// </summary>
    public void Begin()
    {
        pendingProgress = 0f;
        lastFrame = -1;

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
        lastFrame = -1;

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
        frameCount = (long)videoPlayer.frameCount;
        videoPlayer.Pause();
        ApplyProgress(0f, true);
    }

    /// <summary>영상 준비가 끝났다. 첫 프레임을 강제로 렌더한 뒤 밀린 진행도를 반영한다.</summary>
    private void OnPrepared(VideoPlayer source)
    {
        prepared = true;
        frameCount = (long)source.frameCount;

        // Pause 만으로는 첫 프레임이 렌더되지 않는 경우가 있어, 한 번 재생했다가 즉시 멈춘다.
        source.Play();
        source.Pause();

        ApplyProgress(pendingProgress, true);
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

    /// <summary>진행도를 프레임 번호로 바꿔 영상을 스크럽한다. 같은 프레임이면 탐색 요청을 건너뛴다.</summary>
    private void ApplyProgress(float progress, bool force)
    {
        if (videoPlayer == null || frameCount <= 0)
            return;

        float mapped = MapProgress(progress);
        long lastIndex = frameCount - 1;
        long target = (long)Mathf.Round(mapped * lastIndex);

        if (target < 0)
            target = 0;
        else if (target > lastIndex)
            target = lastIndex;

        // 값이 그대로인데 매 프레임 대입하면 탐색 요청이 쌓여 버벅인다.
        if (!force && target == lastFrame)
            return;

        lastFrame = target;
        videoPlayer.frame = target;
    }
}
