using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 미니게임 프리팹들을 프리로드해 두었다가 명령이 오면 재생하는 플레이어.
/// 비활성 홀더의 자식으로 Instantiate 하므로, 프리팹 루트에 저장된 활성 상태와 무관하게
/// Instantiate 시점에 Awake 가 돌지 않고, 재생을 위해 꺼내 활성화하는 순간 처음 초기화된다.
/// </summary>
public class MiniGamePlayer : MonoBehaviour
{
    // 인스펙터에 확실히 노출되도록 제네릭 UnityEvent 는 구체 타입으로 선언한다.
    [Serializable] public class GameFinishedEvent : UnityEvent<MiniGame, bool> { }

    [Header("프리팹")]
    [Tooltip("재생할 미니게임 프리팹 목록. 루트의 저장된 활성 상태는 상관없다 (프리로드 시 자동으로 비활성 처리된다).\n" +
        "GameFlowController 처럼 게임 순서를 소유하는 컨트롤러가 있다면 여기 직접 채우지 말고 SetGamePrefabs() 로 주입받는 편을 권장한다 (이중 등록 방지).")]
    [SerializeField] private List<MiniGame> gamePrefabs = new List<MiniGame>();

    [Header("옵션")]
    [Tooltip("Awake 시점에 모든 프리팹을 미리 Instantiate 한다. " +
        "SetGamePrefabs() 로 외부 주입을 받을 계획이면 반드시 꺼둘 것 — 켜져 있으면 주입 전에 먼저 Preload 되어 무시된다")]
    [SerializeField] private bool preloadOnAwake = true;

    [Tooltip("테스트용. Start 에서 첫 번째 게임을 자동으로 재생한다")]
    [SerializeField] private bool playFirstOnStart = false;

    [Header("이벤트")]
    [Tooltip("게임이 끝나면 (인스턴스, 성공 여부) 를 넘긴다. 이 통지 이후 인스턴스는 자동으로 초기화·비활성화된다")]
    public GameFinishedEvent onGameFinished;

    [Header("실패 패널티")]
    [Tooltip("실패 패널티를 생성하고 한 판 동안 보존할 관리자. 비워두면 패널티 대기 없이 종료한다.")]
    [SerializeField] private FailurePenaltyController failurePenaltyController;

    [Header("게임 시작 프롬프트")]
    [Tooltip("설명과 키 가이드 표시를 담당하는 Presenter. 비워두면 프롬프트 없이 바로 게임을 시작한다")]
    [SerializeField] private MiniGameIntroPresenter introPresenter;

    [Header("화면 페이드")]
    [Tooltip("풀스크린 페이드 오버레이. 비워두면 페이드 연출 없이 보더 트랜지션만 진행한다")]
    [SerializeField] private CanvasGroup screenFadeCanvasGroup;

    [Tooltip("게임 종료 후 화면이 덮이는 데 걸리는 시간(초)")]
    [SerializeField] private float fadeOutDuration = 0.3f;

    [Tooltip("다음 게임 시작 시 화면이 드러나는 데 걸리는 시간(초)")]
    [SerializeField] private float fadeInDuration = 0.3f;

    [Header("게임 보더 트랜지션")]
    [Tooltip("GameBorder 의 RectTransform. 비워두면 보더 트랜지션 없이 페이드/대기만 진행한다.\n" +
        "Awake 시점의 값을 '확대(평상시)' 기준값으로 캐시하고, 트랜지션 중에는 0,0,0,0 으로 축소했다가 되돌아온다")]
    [SerializeField] private RectTransform gameBorderRect;

    [Tooltip("시대(스테이지)별 테마 교체 대상 Image. PlayGame 호출 시 넘긴 borderSprite 로 교체된다")]
    [SerializeField] private Image gameBorderImage;

    [Tooltip("보더 스프라이트 교체 시 투명도 전환에 사용할 CanvasGroup. 비워두면 GameBorder Image 오브젝트에서 자동으로 찾는다")]
    [SerializeField] private CanvasGroup gameBorderCanvasGroup;

    [Tooltip("기존 보더가 사라지고 새 보더가 나타나는 각각의 페이드 시간(초)")]
    [SerializeField] private float borderSpriteFadeDuration = 0.15f;

    [Tooltip("보더가 축소/재확대되는 데 걸리는 시간(초)")]
    [SerializeField] private float borderTransitionDuration = 0.35f;

    [Tooltip("보더가 축소된 채로 대기하는 시간(초) — 게임 사이 쉬어가는 타임")]
    [SerializeField] private float transitionHoldDuration = 0.6f;

    [Header("시대 전환 텍스트")]
    [Tooltip("시대가 바뀔 때 연도 문구를 표시할 TextMeshPro 텍스트")]
    [SerializeField] private TMP_Text eraYearText;

    [Tooltip("시대 전환 시 보더가 축소된 상태로 추가 유지되는 시간(초)")]
    [SerializeField] private float eraTransitionExtraHoldDuration = 1.5f;

    [Tooltip("각 글자가 무작위 문자로 변하다 원래 글자로 확정되기까지 걸리는 시간(초)")]
    [SerializeField] private float eraTextScrambleDurationPerCharacter = 0.12f;

    [Tooltip("현재 글자의 무작위 문자를 교체하는 간격(초). 0 이하면 매 프레임 교체한다")]
    [SerializeField] private float eraTextScrambleInterval = 0.035f;

    [Tooltip("글리치 애니메이션에 사용할 무작위 문자 목록")]
    [SerializeField] private string eraTextRandomCharacterPool =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%&*?";

    // 프리팹 인덱스 -> 프리로드된 인스턴스.
    private readonly Dictionary<int, MiniGame> instances = new Dictionary<int, MiniGame>();
    private bool preloaded;

    // 프리로드된 인스턴스를 담아 두는 비활성 홀더. 부모가 비활성이면 자식의
    // activeInHierarchy 도 false 가 되므로, 프리팹 루트의 저장된 활성 상태와 무관하게
    // Awake/OnEnable 이 돌지 않는 것을 보장한다.
    private Transform preloadHolder;

    // 직전 게임이 끝났지만 아직 정리(StopAndReset/비활성화)되지 않은 인스턴스.
    // 화면 페이드아웃이 끝나기 전까지는 화면에 계속 보여야 하므로 정리를 미뤄둔다.
    private MiniGame pendingCleanup;

    // 엔딩 전환처럼 "마지막 게임 화면을 암전이 끝날 때까지 그대로 보여줘야" 하는 경우,
    // onGameFinished 통지 직후의 자동 정리를 막아 두는 플래그.
    private bool holdPendingCleanup;

    // 현재 진행 중인 트랜지션(페이드/보더) 코루틴. StopCurrent() 로 중간에 취소할 수 있어야 한다.
    private Coroutine transitionRoutine;

    // 실패 패널티 최초 연출 완료 콜백이 중단된 게임을 다시 종료시키지 못하게 막는 토큰.
    private int finishSequence;
    private MiniGame finishingInstance;

    // GameBorder 의 "확대(평상시)" 기준값. Awake 시점에 씬에 세팅된 값을 그대로 캐시한다.
    private Vector2 borderExpandedAnchoredPosition;
    private Vector2 borderExpandedSizeDelta;

    // GameBorder 의 "축소(테두리 노출)" 목표값. 사용자 스펙에 따라 0,0 으로 고정한다.
    private static readonly Vector2 BorderFramedAnchoredPosition = Vector2.zero;
    private static readonly Vector2 BorderFramedSizeDelta = Vector2.zero;

    /// <summary>현재 재생 중인 인스턴스. 없으면 null.</summary>
    public MiniGame Current { get; private set; }

    /// <summary>현재 재생 중인 게임이 있는지 여부.</summary>
    public bool IsPlaying => Current != null;

    /// <summary>이미 Preload() 가 실행됐는지 여부. 외부 주입 실패 여부를 판단하는 용도.</summary>
    public bool IsPreloaded => preloaded;

    private void Awake()
    {
        if (failurePenaltyController == null)
            failurePenaltyController = GetComponent<FailurePenaltyController>();

        if (gameBorderRect != null)
        {
            borderExpandedAnchoredPosition = gameBorderRect.anchoredPosition;
            borderExpandedSizeDelta = gameBorderRect.sizeDelta;
        }

        if (gameBorderCanvasGroup == null && gameBorderImage != null)
            gameBorderCanvasGroup = gameBorderImage.GetComponent<CanvasGroup>();

        HideEraYearTextImmediate();

        if (preloadOnAwake)
            Preload();
    }

    private void OnDisable()
    {
        HideEraYearTextImmediate();
        RestoreBorderOpacityImmediate();
    }

    private void Start()
    {
        if (playFirstOnStart)
            PlayGame(0);
    }

    /// <summary>
    /// 모든 프리팹을 비활성 홀더의 자식으로 Instantiate 해 둔다. 멱등.
    /// 홀더가 비활성이므로 프리팹 루트의 저장된 활성 상태와 무관하게 Awake 가 돌지 않는다.
    /// </summary>
    public void Preload()
    {
        if (preloaded)
            return;

        EnsurePreloadHolder();

        for (int i = 0; i < gamePrefabs.Count; i++)
        {
            MiniGame prefab = gamePrefabs[i];
            if (prefab == null)
                continue;

            MiniGame instance = Instantiate(prefab, preloadHolder);
            instances[i] = instance;
        }

        preloaded = true;
    }

    /// <summary>
    /// gamePrefabs 목록을 외부에서 주입한다. GameFlowController 처럼 게임 순서를 소유하는
    /// 컨트롤러가 있다면, 프리팹은 그쪽에만 등록해 두고 이 메서드로 주입받으면
    /// 두 군데에 같은 프리팹을 중복 등록할 필요가 없어진다. 중복 프리팹은 한 번만 담긴다.
    /// 이미 Preload() 가 실행된 이후라면 무시하고 경고를 남긴다 — preloadOnAwake 를 꺼두고 호출할 것.
    /// </summary>
    public void SetGamePrefabs(IEnumerable<MiniGame> prefabs)
    {
        if (preloaded)
        {
            Debug.LogWarning("MiniGamePlayer: 이미 Preload 된 이후에는 gamePrefabs 를 주입할 수 없습니다. preloadOnAwake 를 꺼주세요.", this);
            return;
        }

        gamePrefabs.Clear();
        foreach (MiniGame prefab in prefabs)
        {
            if (prefab != null && !gamePrefabs.Contains(prefab))
                gamePrefabs.Add(prefab);
        }
    }

    /// <summary>비활성 상태의 프리로드 홀더를 (없으면) 만든다.</summary>
    private void EnsurePreloadHolder()
    {
        if (preloadHolder != null)
            return;

        var holder = new GameObject("[Preload Pool]");
        holder.transform.SetParent(transform, false);
        holder.SetActive(false);
        preloadHolder = holder.transform;
    }

    /// <summary>
    /// index 번째 게임을 재생한다. 성공 여부를 반환한다.
    /// 이미 재생 중이거나 인덱스 범위 밖이면 false.
    /// 프리로드가 안 돼 있으면 먼저 프리로드한다(지연 로드 허용).
    /// </summary>
    public bool PlayGame(int index, Sprite borderSprite = null, string eraYearLabel = null)
    {
        if (IsPlaying)
            return false;

        if (index < 0 || index >= gamePrefabs.Count)
            return false;

        if (!preloaded)
            Preload();

        if (!instances.TryGetValue(index, out MiniGame instance) || instance == null)
            return false;

        // 실제 활성화/재생은 트랜지션 코루틴 내부에서 처리한다. 여기서 Current 를
        // 먼저 채워 두는 이유는 트랜지션이 끝나기 전까지도 IsPlaying 가드로
        // 재진입(중복 PlayGame 호출)을 막기 위함이다.
        Current = instance;
        transitionRoutine = StartCoroutine(
            TransitionThenPlayRoutine(instance, borderSprite, eraYearLabel));
        return true;
    }

    /// <summary>
    /// 게임 종료 → 화면 페이드아웃 → 보더 축소(테두리 노출) → 대기 → 보더 재확대 →
    /// 화면 페이드인+설명 프롬프트 → 재생 순서로 진행하는 게임 사이 쉬어가는 타임 연출.
    /// 각 단계는 관련 필드가 비어 있으면 건너뛴다.
    /// </summary>
    private IEnumerator TransitionThenPlayRoutine(
        MiniGame instance, Sprite borderSprite, string eraYearLabel)
    {
        HideEraYearTextImmediate();

        if (pendingCleanup != null)
        {
            MiniGame previous = pendingCleanup;
            pendingCleanup = null;

            yield return FadeScreen(0f, 1f, fadeOutDuration);
            CleanupInstance(previous);
        }
        else if (screenFadeCanvasGroup != null)
        {
            // 이전 게임이 없는 최초 진입(빅뱅 등) — 페이드 애니메이션 없이 이미 덮인 상태로 시작한다.
            screenFadeCanvasGroup.alpha = 1f;
        }

        if (borderSprite != null && gameBorderImage != null)
            yield return ChangeBorderSprite(borderSprite);

        yield return AnimateBorder(borderExpandedAnchoredPosition, borderExpandedSizeDelta,
            BorderFramedAnchoredPosition, BorderFramedSizeDelta, borderTransitionDuration);

        if (borderSprite != null)
            yield return PresentEraYearText(eraYearLabel);

        yield return new WaitForSeconds(transitionHoldDuration);

        HideEraYearTextImmediate();

        yield return AnimateBorder(BorderFramedAnchoredPosition, BorderFramedSizeDelta,
            borderExpandedAnchoredPosition, borderExpandedSizeDelta, borderTransitionDuration);

        instance.transform.SetParent(transform, false);
        instance.gameObject.SetActive(true);
        instance.Finished += OnGameFinished;

        StartCoroutine(FadeScreen(1f, 0f, fadeInDuration));

        if (introPresenter != null)
            yield return introPresenter.Present(instance);

        // 프롬프트 대기 중 StopCurrent() 등으로 이미 중단됐다면 재생하지 않는다.
        if (Current == instance)
            instance.Play();

        transitionRoutine = null;
    }

    /// <summary>screenFadeCanvasGroup 의 alpha 를 보간한다. 비어 있으면 즉시 반환.</summary>
    private IEnumerator FadeScreen(float from, float to, float duration)
    {
        if (screenFadeCanvasGroup == null)
            yield break;

        if (duration <= 0f)
        {
            screenFadeCanvasGroup.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            screenFadeCanvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }

        screenFadeCanvasGroup.alpha = to;
    }

    /// <summary>기존 보더를 숨긴 뒤 스프라이트를 교체하고 새 보더를 다시 표시한다.</summary>
    private IEnumerator ChangeBorderSprite(Sprite sprite)
    {
        if (gameBorderCanvasGroup == null || borderSpriteFadeDuration <= 0f)
        {
            gameBorderImage.sprite = sprite;
            yield break;
        }

        yield return FadeBorder(gameBorderCanvasGroup.alpha, 0f, borderSpriteFadeDuration);
        gameBorderImage.sprite = sprite;
        yield return FadeBorder(0f, 1f, borderSpriteFadeDuration);
    }

    /// <summary>GameBorder CanvasGroup 의 alpha 를 보간한다.</summary>
    private IEnumerator FadeBorder(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            gameBorderCanvasGroup.alpha =
                Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        gameBorderCanvasGroup.alpha = to;
    }

    /// <summary>중단된 보더 페이드가 다음 재생에 영향을 주지 않도록 완전히 표시된 상태로 복원한다.</summary>
    private void RestoreBorderOpacityImmediate()
    {
        if (gameBorderCanvasGroup != null)
            gameBorderCanvasGroup.alpha = 1f;
    }

    /// <summary>gameBorderRect 의 anchoredPosition/sizeDelta 를 보간한다. 비어 있으면 즉시 반환.</summary>
    private IEnumerator AnimateBorder(Vector2 fromPos, Vector2 fromSize, Vector2 toPos, Vector2 toSize, float duration)
    {
        if (gameBorderRect == null)
            yield break;

        if (duration <= 0f)
        {
            gameBorderRect.anchoredPosition = toPos;
            gameBorderRect.sizeDelta = toSize;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float ratio = Mathf.Clamp01(t / duration);
            gameBorderRect.anchoredPosition = Vector2.Lerp(fromPos, toPos, ratio);
            gameBorderRect.sizeDelta = Vector2.Lerp(fromSize, toSize, ratio);
            yield return null;
        }

        gameBorderRect.anchoredPosition = toPos;
        gameBorderRect.sizeDelta = toSize;
    }

    /// <summary>
    /// 시대 연도 문구를 왼쪽부터 한 글자씩 해독되는 것처럼 표시한다.
    /// 현재 글자는 무작위 문자로 빠르게 교체하다 원래 글자로 확정한다.
    /// </summary>
    private IEnumerator PresentEraYearText(string label)
    {
        float elapsed = 0f;
        bool canAnimate = eraYearText != null && !string.IsNullOrWhiteSpace(label);

        if (canAnimate)
        {
            eraYearText.gameObject.SetActive(true);
            SetEraYearTextProgress(label, 0);

            for (int index = 0; index < label.Length; index++)
            {
                if (char.IsWhiteSpace(label[index]))
                {
                    SetEraYearTextProgress(label, index + 1);
                    continue;
                }

                float characterElapsed = 0f;
                float nextScrambleTime = 0f;

                while (characterElapsed < eraTextScrambleDurationPerCharacter)
                {
                    if (eraTextScrambleInterval <= 0f || characterElapsed >= nextScrambleTime)
                    {
                        eraYearText.text = BuildScrambledEraText(
                            label, index, PickRandomEraCharacter());
                        nextScrambleTime += Mathf.Max(eraTextScrambleInterval, 0f);
                    }

                    float deltaTime = Time.deltaTime;
                    characterElapsed += deltaTime;
                    elapsed += deltaTime;
                    yield return null;
                }

                SetEraYearTextProgress(label, index + 1);
            }

            eraYearText.text = label;
        }

        float remainingHold = eraTransitionExtraHoldDuration - elapsed;
        if (remainingHold > 0f)
            yield return new WaitForSeconds(remainingHold);
    }

    /// <summary>현재까지 확정된 접두사만 표시한다.</summary>
    private void SetEraYearTextProgress(string label, int confirmedCharacterCount)
    {
        int confirmedCount = Mathf.Clamp(confirmedCharacterCount, 0, label.Length);
        eraYearText.text = label.Substring(0, confirmedCount);
    }

    /// <summary>확정된 글자와 현재 무작위 글자만 표시한다.</summary>
    private static string BuildScrambledEraText(string label, int currentIndex, char randomCharacter)
    {
        return label.Substring(0, currentIndex) + randomCharacter;
    }

    /// <summary>인스펙터에 지정된 풀에서 글리치용 문자를 하나 고른다.</summary>
    private char PickRandomEraCharacter()
    {
        if (string.IsNullOrEmpty(eraTextRandomCharacterPool))
            return '?';

        int index = UnityEngine.Random.Range(0, eraTextRandomCharacterPool.Length);
        return eraTextRandomCharacterPool[index];
    }

    /// <summary>시대 연도 텍스트를 즉시 숨기고 초기화한다.</summary>
    private void HideEraYearTextImmediate()
    {
        if (eraYearText == null)
            return;

        eraYearText.text = string.Empty;
        eraYearText.gameObject.SetActive(false);
    }

    /// <summary>
    /// gamePrefabs 에 등록된 프리팹 에셋으로 게임을 재생한다. 내부적으로 인덱스를 찾아 PlayGame(int) 를 호출한다.
    /// 등록되지 않은 프리팹이면 false.
    /// </summary>
    public bool PlayGame(MiniGame prefab, Sprite borderSprite = null, string eraYearLabel = null)
    {
        int index = gamePrefabs.IndexOf(prefab);
        if (index < 0)
            return false;

        return PlayGame(index, borderSprite, eraYearLabel);
    }

    /// <summary>현재 게임을 강제로 중단·초기화하고 비활성화한다. (onGameFinished 는 발화하지 않는다)</summary>
    public void StopCurrent()
    {
        CancelPendingFinish();

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        HideEraYearTextImmediate();
        RestoreBorderOpacityImmediate();

        if (introPresenter != null)
            introPresenter.HideImmediate();

        holdPendingCleanup = false;

        if (pendingCleanup != null)
        {
            CleanupInstance(pendingCleanup);
            pendingCleanup = null;
        }

        if (Current == null)
            return;

        MiniGame instance = Current;
        Current = null;
        CleanupInstance(instance);
    }

    /// <summary>현재 한 판에 누적된 실패 패널티를 모두 제거한다.</summary>
    public void ClearFailurePenalties()
    {
        CancelPendingFinish();

        if (failurePenaltyController != null)
            failurePenaltyController.ClearAll();
    }

    /// <summary>
    /// 게임 종료 통지 수신. Current 를 먼저 비운 뒤 onGameFinished 를 발화한다.
    /// 인스턴스 정리는 즉시 하지 않고 pendingCleanup 에 보관한다 — 화면 페이드아웃이
    /// 끝날 때까지 이전 게임 화면이 그대로 보여야 하기 때문이다(다음 PlayGame 의 트랜지션
    /// 코루틴이 페이드아웃 완료 후 소비해서 정리한다). 리스너가 다음 게임을 재생하지
    /// 않았다면(흐름 종료 등) 아래에서 즉시 정리해 인스턴스가 방치되지 않게 한다.
    /// </summary>
    private void OnGameFinished(MiniGame instance, bool success)
    {
        if (!success && failurePenaltyController != null &&
            instance != null && instance.FailurePenaltyPrefab != null)
        {
            finishingInstance = instance;
            int sequence = ++finishSequence;

            bool started = failurePenaltyController.Apply(
                instance.FailurePenaltyPrefab,
                () => CompletePenalizedFailure(instance, sequence));

            if (started)
                return;
        }

        CompleteGameFinished(instance, success);
    }

    private void CompletePenalizedFailure(MiniGame instance, int sequence)
    {
        if (sequence != finishSequence || finishingInstance != instance)
            return;

        finishingInstance = null;
        CompleteGameFinished(instance, false);
    }

    private void CompleteGameFinished(MiniGame instance, bool success)
    {
        // Invoke 이전에 Current 를 먼저 비워서, 콜백 안에서 즉시 다음 게임을 재생해도(재진입)
        // IsPlaying 가드에 걸려 PlayGame 이 막히지 않도록 한다.
        if (Current == instance)
            Current = null;

        pendingCleanup = instance;
        onGameFinished.Invoke(instance, success);

        if (pendingCleanup == instance && !holdPendingCleanup)
        {
            CleanupInstance(instance);
            pendingCleanup = null;
        }
    }

    /// <summary>
    /// 종료된 인스턴스의 자동 정리(StopAndReset/비활성화)를 보류한다.
    /// 엔딩 전환처럼 마지막 게임 화면과 실패 연출을 암전이 끝날 때까지 유지해야 할 때
    /// <see cref="onGameFinished"/> 콜백 안에서 호출한다.
    /// 정리를 다시 진행하려면 <see cref="ReleaseFinishedInstance"/> 를 호출한다.
    /// </summary>
    public void HoldFinishedInstance()
    {
        holdPendingCleanup = true;
    }

    /// <summary>보류해 둔 인스턴스를 지금 정리한다. 보류 중이 아니어도 호출 가능하다(멱등).</summary>
    public void ReleaseFinishedInstance()
    {
        holdPendingCleanup = false;

        if (pendingCleanup == null)
            return;

        CleanupInstance(pendingCleanup);
        pendingCleanup = null;
    }

    private void CancelPendingFinish()
    {
        finishSequence++;
        finishingInstance = null;
    }

    /// <summary>인스턴스를 초기화·비활성화하고 구독을 해제한 뒤 프리로드 풀로 되돌린다.</summary>
    private void CleanupInstance(MiniGame instance)
    {
        instance.Finished -= OnGameFinished;
        instance.StopAndReset();
        instance.gameObject.SetActive(false);
        instance.transform.SetParent(preloadHolder, false);
    }
}
