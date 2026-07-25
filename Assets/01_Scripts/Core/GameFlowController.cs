using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MiniGamePlayer 를 제어하여 인스펙터에 등록된 미니게임들을 순서대로 재생하는 게임 흐름 컨트롤러.
/// 크리티컬로 표시된 게임에서 실패하면 흐름을 멈추고 게임 엔딩으로 진행한다(onGameEnding 발화).
/// 크리티컬이 아닌 게임은 실패해도 다음 게임으로 계속 진행한다.
/// </summary>
public class GameFlowController : MonoBehaviour
{
    [Serializable]
    private class GameEntry
    {
        [Tooltip("재생할 미니게임 프리팹. player 쪽에는 따로 등록할 필요 없다 — Start 시점에 이 목록이 자동으로 주입된다")]
        public MiniGame prefab;

        [Tooltip("미니게임의 서사적 성격.\n" +
            "Normal: 실패해도 아무 영향 없음\n" +
            "Change: 실패해도 흐름은 이어지지만 엔딩 서사·엔딩 이미지에 반영됨\n" +
            "Critical: 실패 시 즉시 엔딩으로 이동")]
        public MiniGameKind kind = MiniGameKind.Normal;

        [Tooltip("이 게임이 속한 시대")]
        public Era era = Era.Prehistoric;

        [Tooltip("이 게임 시작 시 GameBorder 에 적용할 시대 테마 스프라이트. 비워두면 기존 테마를 유지한다")]
        public Sprite borderSprite;

        public string eraYearText;

        [Header("엔딩 맥락 (Change: 성공/실패 모두 반영, Critical: 실패 결과 반영)")]
        [Tooltip("사건 이름. 예: '마녀사냥에서 마녀를 색출'")]
        public string eventLabel;

        [Tooltip("성공했을 때의 역사적 의미(한국어). 크레딧 문장의 재료 (Change 전용)")]
        [TextArea(2, 4)] public string successMeaning;

        [Tooltip("실패했을 때의 역사적 의미(한국어). 크레딧 문장의 재료.\n" +
            "Change: 크레딧 본문에 그대로 삽입됨\n" +
            "Critical: 흐름 중단 서사에 반영됨(RunResult.EndedFailureMeaning)")]
        [TextArea(2, 4)] public string failureMeaning;

        [Tooltip("성공했을 때 엔딩 이미지에 들어갈 시각 요소(영문 구절).\n" +
            "예: a witch burned at the stake, shown only as a faded illustration in a children's storybook")]
        [TextArea(2, 4)] public string successVisual;

        [Tooltip("실패했을 때 엔딩 이미지에 들어갈 시각 요소(영문 구절).\n" +
            "예: a young witch in a modern school uniform sitting among ordinary students")]
        [TextArea(2, 4)] public string failureVisual;

        [Tooltip("서사 비중. 높을수록 엔딩 이미지 전경에 배치되도록 유도한다")]
        [Range(0, 10)] public int visualWeight = 5;

        /// <summary>실패 시 즉시 엔딩으로 가야 하는 게임인지.</summary>
        public bool IsCritical => kind == MiniGameKind.Critical;

        /// <summary>결과에 따라 이 항목을 엔딩 재료(<see cref="MiniGameOutcome"/>)로 변환한다.</summary>
        public MiniGameOutcome ToOutcome(bool success) => new MiniGameOutcome
        {
            eventLabel = string.IsNullOrWhiteSpace(eventLabel) && prefab != null ? prefab.name : eventLabel,
            era = era.ToKorean(),
            success = success,
            meaning = success ? successMeaning : failureMeaning,
            visual = success ? successVisual : failureVisual,
            visualWeight = visualWeight,
        };
    }

    [Header("참조")]
    [SerializeField] private MiniGamePlayer player;

    [Tooltip("시대별 BGM 디렉터. 비워두면 씬에 놓인 것을 자동으로 찾는다. 없어도 흐름은 그대로 돌아간다")]
    [SerializeField] private EraBgmDirector bgmDirector;

    [Header("게임 순서")]
    [Tooltip("순서대로 재생할 게임 목록")]
    [SerializeField] private List<GameEntry> games = new List<GameEntry>();

    [Header("옵션")]
    [Tooltip("Start 에서 자동으로 흐름을 시작한다")]
    [SerializeField] private bool playOnStart = true;

    [Header("엔딩")]
    [Tooltip("크리티컬 실패 또는 모든 미니게임 완료 시 전환할 엔딩 씬 이름")]
    [SerializeField] private string endingSceneName = "99_Ending";

    [Header("엔딩 전환 연출")]
    [Tooltip("엔딩 전환에 사용할 암전 오버레이 CanvasGroup. MiniGamePlayer 의 ScreenFade 를 그대로 재사용해도 된다")]
    [SerializeField] private CanvasGroup endingFadeCanvasGroup;

    [Tooltip("암전 색. 게임 간 전환용 오버레이를 공유하더라도 엔딩에서는 이 색으로 덮는다 (99_Ending 이 검게 시작하므로 검정 권장)")]
    [SerializeField] private Color endingFadeColor = Color.black;

    [Tooltip("암전 오버레이에 임시로 부여할 정렬 순서. 실패 패널티 오버레이(20~100)보다 커야 화면 전체가 덮인다")]
    [SerializeField] private int endingFadeSortingOrder = 500;

    [Tooltip("마지막 결과 연출과 카메라 셰이크를 보여 줄 시간(초)")]
    [Min(0f)]
    [SerializeField] private float endingPresentationHoldDuration = 0.5f;

    [Tooltip("검은 화면으로 암전하는 시간(초)")]
    [Min(0f)]
    [SerializeField] private float endingFadeDuration = 0.6f;

    [Tooltip("완전 암전 후 씬을 로드하기 전 유지 시간(초)")]
    [Min(0f)]
    [SerializeField] private float endingBlackHoldDuration = 0.1f;

    [Header("이벤트")]
    [Tooltip("크리티컬 게임 실패 시 발화. 게임 엔딩 씬 전환 등에 연결한다")]
    public UnityEvent onGameEnding;

    [Tooltip("등록된 모든 게임을 클리어(또는 통과)하면 발화")]
    public UnityEvent onAllGamesCleared;

    private int currentIndex = -1;
    private bool ended;
    private Coroutine endingTransitionRoutine;

    /// <summary>현재 흐름이 진행 중인지 여부.</summary>
    public bool IsRunning => currentIndex >= 0 && currentIndex < games.Count && !ended;

    private void OnEnable()
    {
        if (player != null)
            player.onGameFinished.AddListener(OnGameFinished);
    }

    private void OnDisable()
    {
        if (player != null)
            player.onGameFinished.RemoveListener(OnGameFinished);
    }

    private void Start()
    {
        InjectGamePrefabs();

        if (playOnStart)
            StartFlow();
    }

    /// <summary>
    /// games 에 등록된 프리팹들을 player 에 주입하고 Preload 한다.
    /// player.gamePrefabs 를 별도로 채우지 않아도 되게 하여 이중 등록을 없앤다.
    /// player 의 preloadOnAwake 가 켜져 있으면 이 시점엔 이미 Preload 가 끝나 주입이 무시되므로 꺼둘 것.
    /// </summary>
    private void InjectGamePrefabs()
    {
        if (player == null)
            return;

        if (player.IsPreloaded)
        {
            Debug.LogWarning("GameFlowController: player 가 이미 Preload 되어 프리팹 주입이 무시됩니다. player 의 Preload On Awake 를 꺼주세요.", this);
            return;
        }

        var prefabs = new List<MiniGame>(games.Count);
        foreach (GameEntry entry in games)
        {
            if (entry.prefab != null)
                prefabs.Add(entry.prefab);
        }

        player.SetGamePrefabs(prefabs);
        player.Preload();
    }

    /// <summary>처음부터 등록된 순서대로 게임 흐름을 시작한다.</summary>
    public void StartFlow()
    {
        if (player != null)
        {
            player.StopCurrent();
            player.ClearFailurePenalties();
        }

        ended = false;
        currentIndex = -1;

        // 재시작 시 이전 판의 엔딩 재료가 섞이지 않도록 비운다.
        RunResult.Instance.Clear();

        PlayNext();
    }

    private void PlayNext()
    {
        currentIndex++;

        if (currentIndex >= games.Count)
        {
            ended = true;
            onAllGamesCleared.Invoke();
            BeginEndingTransition();
            return;
        }

        GameEntry entry = games[currentIndex];

        // 시대가 바뀌면 BGM 도 같이 넘긴다. 같은 시대가 이어지면 디렉터가 알아서 곡을 유지한다.
        PlayEraBgm(entry.era);

        if (entry.prefab == null || player == null ||
            !player.PlayGame(entry.prefab, entry.borderSprite, entry.eraYearText))
        {
            Debug.LogWarning($"GameFlowController: index {currentIndex} 게임을 재생할 수 없습니다.", this);
            PlayNext();
        }
    }

    /// <summary>해당 시대의 BGM 을 요청한다. 디렉터가 없으면 조용히 넘어간다.</summary>
    private void PlayEraBgm(Era era)
    {
        EraBgmDirector director = bgmDirector != null ? bgmDirector : EraBgmDirector.Instance;
        if (director != null)
            director.Play(era);
    }

    private void OnGameFinished(MiniGame instance, bool success)
    {
        if (ended)
            return;

        if (currentIndex < 0 || currentIndex >= games.Count)
            return;

        GameEntry entry = games[currentIndex];

        // 변화 미니게임은 성공/실패 모두 엔딩 재료가 된다 — 실패만 기록하는 게 아니다.
        if (entry.kind == MiniGameKind.Change)
            RunResult.Instance.Record(entry.ToOutcome(success));

        if (!success && entry.IsCritical)
        {
            ended = true;
            // 조기 엔딩을 일으킨 Critical 실패도 엔딩 재료로 기록해야
            // failureVisual 이 대본 생성과 이미지 생성 프롬프트까지 전달된다.
            RunResult.Instance.Record(entry.ToOutcome(false));
            RunResult.Instance.MarkEarlyEnding(entry.era.ToKorean(), entry.eventLabel, entry.failureMeaning);
            onGameEnding.Invoke();
            BeginEndingTransition();
            return;
        }

        PlayNext();
    }

    /// <summary>현재 판의 <see cref="RunResult"/>를 유지한 채 엔딩 씬으로 전환한다.</summary>
    private void BeginEndingTransition()
    {
        if (endingTransitionRoutine != null)
            return;

        // 마지막 게임 인스턴스를 즉시 정리하면 화면이 툭 비어 버리고 실패 셰이크도 눈에 안 띈다.
        // 암전이 끝날 때까지 정리를 미뤄 마지막 연출을 그대로 보여 준다.
        if (player != null)
            player.HoldFinishedInstance();

        endingTransitionRoutine = StartCoroutine(EndingTransitionRoutine());
    }

    /// <summary>마지막 결과 연출을 보존한 뒤 암전하여 엔딩 씬으로 전환한다.</summary>
    private IEnumerator EndingTransitionRoutine()
    {
        if (endingFadeCanvasGroup != null)
        {
            endingFadeCanvasGroup.alpha = 0f;
            endingFadeCanvasGroup.blocksRaycasts = true;

            // 공유 오버레이가 흰색일 수 있으므로 엔딩 직전에 암전 색으로 바꿔 둔다.
            // 이 시점 이후로는 게임 간 전환이 더 없어서 원복할 필요가 없다.
            var fadeGraphic = endingFadeCanvasGroup.GetComponent<Graphic>();
            if (fadeGraphic != null)
                fadeGraphic.color = endingFadeColor;

            // 실패 패널티 오버레이들은 자체 Canvas 를 높은 sortingOrder 로 띄우므로,
            // 오버레이에도 중첩 Canvas 를 붙여 정렬을 덮어써야 화면이 완전히 가려진다.
            var fadeCanvas = endingFadeCanvasGroup.GetComponent<Canvas>();
            if (fadeCanvas == null)
                fadeCanvas = endingFadeCanvasGroup.gameObject.AddComponent<Canvas>();

            fadeCanvas.overrideSorting = true;
            fadeCanvas.sortingOrder = endingFadeSortingOrder;
        }

        if (endingPresentationHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(endingPresentationHoldDuration);

        if (endingFadeCanvasGroup != null)
        {
            float duration = Mathf.Max(0f, endingFadeDuration);
            if (duration <= 0f)
            {
                endingFadeCanvasGroup.alpha = 1f;
            }
            else
            {
                float elapsed = 0f;
                float startAlpha = endingFadeCanvasGroup.alpha;

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    endingFadeCanvasGroup.alpha =
                        Mathf.Lerp(startAlpha, 1f, Mathf.Clamp01(elapsed / duration));
                    yield return null;
                }

                endingFadeCanvasGroup.alpha = 1f;
            }
        }

        // 완전히 덮인 뒤에 정리한다 — 화면이 검을 때 치워야 전환 티가 안 난다.
        if (player != null)
            player.ReleaseFinishedInstance();

        if (player != null)
            player.ClearFailurePenalties();

        if (CameraEffectManager.TryGetInstance != null)
            CameraEffectManager.TryGetInstance.StopAll();

        if (endingBlackHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(endingBlackHoldDuration);

        LoadEndingScene();
    }

    private void LoadEndingScene()
    {
        if (string.IsNullOrWhiteSpace(endingSceneName))
        {
            Debug.LogError("GameFlowController: 엔딩 씬 이름이 비어 있어 전환할 수 없습니다.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(endingSceneName))
        {
            Debug.LogError(
                $"GameFlowController: 엔딩 씬 '{endingSceneName}'을 로드할 수 없습니다. Build Settings 등록 여부를 확인하세요.",
                this);
            return;
        }

        SceneManager.LoadScene(endingSceneName);
    }
}
