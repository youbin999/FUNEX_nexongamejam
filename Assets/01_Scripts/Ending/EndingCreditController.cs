using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 엔딩 씬의 진행을 총괄한다.
///
/// 연출 순서:
///   암전 여운(leadIn) ─┬─ (병렬) 대본 생성: Gemini → 실패 시 폴백
///                      │
///   대본 도착 → 크레딧 롤 시작 ─┬─ (병렬) 이미지 생성 → 도착 시 배경에 페이드인
///                              │
///   롤 종료 → 에필로그 표시 및 계속 버튼 대기 → (이미지 합류) → 이름 입력 탭 → 갤러리 저장 → onCreditsFinished → 갤러리 씬
///
/// 이미지가 0초에 있을 필요가 없다는 점을 이용해, 생성 지연을 롤 시간에 흡수시킨다.
/// 다만 갤러리에는 텍스트와 이미지가 한 쌍으로 들어가야 하므로, 저장 직전에 이미지 쪽을 합류시킨다.
///
/// 저장 시점이 "이름 확정 후"이므로, 탭을 띄운 채 게임을 끄면 그 판은 갤러리에 남지 않는다.
/// </summary>
public class EndingCreditController : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("크레딧 본문 텍스트. 이 오브젝트의 RectTransform 이 위로 스크롤된다")]
    [SerializeField] private TMP_Text creditText;

    [Tooltip("스크롤 대상 RectTransform. 비워두면 creditText 의 RectTransform 을 쓴다")]
    [SerializeField] private RectTransform creditRect;

    [Tooltip("롤이 끝난 뒤 화면 중앙에 표시할 마무리 문장")]
    [SerializeField] private TMP_Text epilogueText;

    [Tooltip("에필로그를 닫는 계속 버튼 오브젝트. OnClick 에 ContinueFromEpilogue 를 연결한다")]
    [SerializeField] private GameObject epilogueContinueButton;

    [Tooltip("크레딧 롤을 건너뛰는 버튼. 비워두면 화면 오른쪽 상단에 코드로 만들어 붙인다")]
    [SerializeField] private Button skipButton;

    [Tooltip("자동 생성되는 건너뛰기 버튼의 문구")]
    [SerializeField] private string skipButtonLabel = "건너뛰기 ▶▶";

    [Tooltip("자동 생성되는 건너뛰기 버튼과 화면 오른쪽 상단 모서리 사이의 여백(px, 1920x1080 기준)")]
    [SerializeField] private Vector2 skipButtonMargin = new Vector2(48f, 48f);

    [Tooltip("엔딩 이미지를 표시할 RawImage")]
    [SerializeField] private RawImage backgroundImage;

    [Tooltip("배경 이미지 페이드용 CanvasGroup. 비워두면 페이드 없이 즉시 표시된다")]
    [SerializeField] private CanvasGroup backgroundGroup;

    [Tooltip("크레딧이 끝난 뒤 world 이름을 받는 탭. 비워두면 이름 없이 저장하고 넘어간다")]
    [SerializeField] private WorldNamePrompt namePrompt;

    [Header("연출 타이밍")]
    [Tooltip("크레딧이 시작되기 전 암전 상태로 머무는 최소 시간(초). 대본이 더 빨리 와도 이만큼은 기다린다")]
    [SerializeField] private float leadInDuration = 3f;

    [Tooltip("크레딧이 초당 올라가는 픽셀 수")]
    [SerializeField] private float scrollSpeed = 60f;

    [Tooltip("스페이스바나 마우스 좌클릭을 누르고 있는 동안 스크롤 속도에 곱해지는 배율. 1 이하면 빨리감기가 꺼진다")]
    [SerializeField] private float fastForwardMultiplier = 5f;

    [Tooltip("롤 종료 후 에필로그가 나타나기까지의 간격(초)")]
    [SerializeField] private float epilogueDelay = 1f;

    [Tooltip("배경 이미지 페이드인 시간(초)")]
    [SerializeField] private float imageFadeDuration = 2f;

    [Header("생성기 설정")]
    [Tooltip("대본 생성에 쓸 Gemini 텍스트 모델. gemini-2.5-flash 는 신규 사용자에게 제공되지 않으므로 쓰지 말 것")]
    [SerializeField] private string narratorModel = "gemini-3.6-flash";

    [Tooltip("대본 생성 타임아웃(초). 초과하면 폴백 텍스트로 진행한다.\n" +
        "생각하는 모델은 응답이 느릴 수 있어 너무 짧으면 서버는 성공했는데 여기서 끊긴다")]
    [SerializeField] private int narratorTimeout = 40;

    [Tooltip("대본 생성 온도. 높을수록 같은 결과 조합이라도 문장이 더 흔들린다.\n" +
        "판마다 크레딧이 비슷하게 나올 때 올려 보면 된다")]
    [Range(0f, 2f)]
    [SerializeField] private float narratorTemperature = 1.1f;

    [Tooltip("엔딩 이미지 생성처.\n" +
        "Pollinations: 키 불필요·무료 (기본)\n" +
        "Gemini: 결제를 활성화한 프로젝트에서만 동작 (무료 티어 일일 한도 0)")]
    [SerializeField] private EndingImageProvider imageProvider = EndingImageProvider.Pollinations;

    [Tooltip("imageProvider 가 Gemini 일 때 쓸 이미지 모델")]
    [SerializeField] private string imageModel = "gemini-3.1-flash-image";

    [Tooltip("이미지 생성 타임아웃(초)")]
    [SerializeField] private int imageTimeout = 60;

    [Tooltip("켜면 같은 미니게임 결과 조합의 엔딩 이미지를 재사용한다. 끄면 매번 새 이미지를 생성한다")]
    [SerializeField] private bool useImageCache;

    [Tooltip("체크하면 API 를 호출하지 않고 항상 폴백 텍스트를 쓴다. 연출만 손볼 때 켜면 편하다.\n" +
        "이미지 생성도 함께 건너뛰므로 갤러리에는 저장되지 않는다")]
    [SerializeField] private bool forceFallback;

    [Tooltip("끄면 이번 엔딩을 갤러리에 남기지 않는다. 연출 테스트로 저장본이 지저분해질 때 끄면 된다")]
    [SerializeField] private bool saveToGallery = true;

    [Header("생성 중 표시")]
    [Tooltip("대본을 만드는 동안 암전 화면 가운데에 띄울 문구. 비워두면 코드로 만들어 붙인다")]
    [SerializeField] private TMP_Text generatingText;

    [Tooltip("생성 중에 보여 줄 문구. 뒤에 점이 하나씩 늘어난다")]
    [SerializeField] private string generatingLabel = "세계 생성중";

    [Tooltip("점이 하나씩 늘어나는 간격(초). 0 이하면 점 애니메이션을 끈다")]
    [SerializeField] private float generatingDotInterval = 0.5f;

    [Header("AI 크레딧 불가 안내")]
    [Tooltip("API 로 크레딧을 만들지 못했을 때 띄울 선택창. 비워두면 이 오브젝트에 붙여 쓴다")]
    [SerializeField] private ChoicePrompt fallbackNotice;

    [Tooltip("API 실패 시 보여 줄 안내 문구")]
    [TextArea(2, 4)]
    [SerializeField] private string fallbackNoticeHeadline =
        "현재 api한도가 끝나 개발자가 그지가 될것같아\nai가 아닌 기본크래딧이 생성될겁니다";

    [SerializeField] private string fallbackNoticeQuestion = "그래도 이 세계를 만드시겠습니까?";
    [SerializeField] private string fallbackNoticeConfirmLabel = "월드생성하기";
    [SerializeField] private string fallbackNoticeCancelLabel = "그만두기";

    [Tooltip("'그만두기' 를 골랐을 때 이동할 씬. 이 판은 갤러리에 남지 않는다")]
    [SerializeField] private string quitSceneName = "00000_Title";

    [Header("종료 후")]
    [Tooltip("이름 확정 뒤 넘어갈 씬 이름. 비워두면 씬을 옮기지 않는다. Build Settings 에 등록돼 있어야 한다")]
    [SerializeField] private string gallerySceneName = "00000_Gallery";

    [Header("이벤트")]
    [Tooltip("에필로그·이름 입력까지 끝나면 발화. 씬 이동 직전에 불린다")]
    public UnityEvent onCreditsFinished;

    /// <summary>생성에 성공한 엔딩 이미지. 갤러리 저장에 그대로 쓴다. 실패했으면 null.</summary>
    private Texture2D endingImage;

    private bool isWaitingForEpilogueContinue;

    /// <summary>건너뛰기 버튼이 눌렸는지. 롤과 대기 구간이 이 값을 보고 즉시 빠져나간다.</summary>
    private bool skipRequested;

    /// <summary>이번 판의 대본이 API 가 아니라 폴백으로 만들어졌는지. 안내창을 띄울지 판단하는 근거다.</summary>
    private bool scriptCameFromFallback;


    /// <summary>"세계 생성중" 점 애니메이션 코루틴. 감출 때 멈춘다.</summary>
    private Coroutine generatingRoutine;


    // ── 전체 진행 ──

    /// <summary>건너뛰기 버튼을 준비하고 엔딩 연출 전체를 굴리는 코루틴을 띄운다.</summary>
    private void Start()
    {
        EnsureSkipButton();
        StartCoroutine(RunEndingRoutine());
    }

    /// <summary>
    /// 엔딩 한 판의 전체 순서를 지휘한다.
    /// - 암전 여운과 대본 생성을 병렬로 굴려 생성 지연을 감춘다
    /// - 롤이 도는 동안 이미지를 병렬 생성하고, 저장 직전에 합류시킨다
    /// - 이름은 이미지를 보고 짓는 것이라 이미지 합류 뒤에 물어본다
    /// </summary>
    private IEnumerator RunEndingRoutine()
    {
        PrepareInitialState();

        RunResult result = RunResult.Instance;

        // 암전이 길어질 수 있으므로(대본 생성 최대 narratorTimeout 초) 멈춘 게 아니라는 표시를 띄운다.
        SetGeneratingVisible(true);

        // 대본 생성과 암전 여운을 동시에 굴린다.
        EndingScript script = null;
        Coroutine narration = StartCoroutine(ResolveScriptRoutine(result, s => script = s));

        yield return new WaitForSeconds(leadInDuration);
        yield return narration;

        // ResolveScriptRoutine 은 항상 유효한 대본을 채워 넣는다(최악의 경우 폴백).
        // 방어적으로 한 번 더 확인해 엔딩이 멈추는 일만은 막는다.
        if (script == null || !script.IsValid)
        {
            script = FallbackEndingNarrator.Build(result);
            scriptCameFromFallback = true;
        }

        // 대본이 준비됐으니 표시를 걷는다. 안내창이 뜬다면 그 화면도 깨끗해야 한다.
        SetGeneratingVisible(false);

        // API 로 크레딧을 못 만들었으면, 롤을 시작하기 전에 사실대로 알리고 계속할지 묻는다.
        // 여기서 그만두면 이 판은 갤러리에 남지 않는다.
        bool proceed = true;
        yield return ConfirmFallbackRoutine(ok => proceed = ok);

        if (!proceed)
        {
            GoToScene(quitSceneName);
            yield break;
        }

        // 이미지는 롤과 병렬로 생성한다 — 롤이 끝나기 전에만 도착하면 된다.
        Coroutine imaging = StartCoroutine(ResolveImageRoutine(result, script.image_prompt));

        yield return ScrollCreditsRoutine(script);

        yield return WaitSkippable(epilogueDelay);
        ShowEpilogue(script.epilogue);
        yield return WaitForEpilogueContinueRoutine();

        // 롤이 짧았거나 생성이 느렸으면 이미지가 아직 안 떠 있을 수 있다.
        // 갤러리에는 텍스트와 이미지가 한 쌍으로 들어가야 하므로 여기서 합류시킨다.
        // 생성기에 타임아웃이 걸려 있어 무한정 기다리지는 않는다.
        yield return imaging;

        // 이름은 이미지를 보고 짓는 것이므로 이미지가 합류한 뒤에 물어본다.
        string worldName = null;
        yield return PromptForNameRoutine(n => worldName = n);

        SaveToGallery(script, result, worldName);

        onCreditsFinished.Invoke();

        GoToGallery();
    }

    /// <summary>크레딧·에필로그·배경을 모두 감춘 암전 상태로 초기화한다.</summary>
    private void PrepareInitialState()
    {
        if (creditText != null)
        {
            creditText.gameObject.SetActive(true);
            creditText.text = string.Empty;
        }

        if (epilogueText != null)
            epilogueText.gameObject.SetActive(false);

        if (epilogueContinueButton != null)
            epilogueContinueButton.SetActive(false);

        isWaitingForEpilogueContinue = false;

        if (backgroundGroup != null)
            backgroundGroup.alpha = 0f;

        if (backgroundImage != null)
            backgroundImage.enabled = false;
    }


    // ── 생성 중 표시 ──

    /// <summary>
    /// "세계 생성중" 표시를 켜고 끈다.
    /// 켤 때 없으면 만들어 붙이고, 점 애니메이션 코루틴도 같이 돌린다.
    /// </summary>
    private void SetGeneratingVisible(bool visible)
    {
        if (visible && generatingText == null)
            generatingText = BuildGeneratingText();

        if (generatingText == null)
            return;

        if (generatingRoutine != null)
        {
            StopCoroutine(generatingRoutine);
            generatingRoutine = null;
        }

        generatingText.gameObject.SetActive(visible);

        if (!visible)
            return;

        generatingText.text = generatingLabel;

        if (generatingDotInterval > 0f)
            generatingRoutine = StartCoroutine(AnimateGeneratingDotsRoutine());
    }

    /// <summary>화면 가운데에 놓인 생성 중 문구를 만든다.</summary>
    private TMP_Text BuildGeneratingText()
    {
        // 크레딧·배경 위에 뜨되 안내창(400)보다는 아래에 둔다.
        Canvas canvas = SimpleUiBuilder.CreateOverlayCanvas("[Generating Canvas]", 150);
        canvas.transform.SetParent(transform, false);

        TMP_Text label = SimpleUiBuilder.CreateLabel(canvas.transform, "GeneratingText", generatingLabel, 44f);
        SimpleUiBuilder.AnchorToCenter(label.rectTransform, Vector2.zero);
        label.rectTransform.sizeDelta = new Vector2(1200f, 120f);

        return label;
    }

    /// <summary>
    /// 문구 뒤의 점을 하나씩 늘린다. 최대 20초까지 검은 화면이 이어질 수 있어,
    /// 멈춘 게 아니라 진행 중이라는 신호가 필요하다.
    /// </summary>
    private IEnumerator AnimateGeneratingDotsRoutine()
    {
        int dots = 0;

        while (true)
        {
            yield return new WaitForSecondsRealtime(generatingDotInterval);

            dots = (dots + 1) % 4;
            generatingText.text = generatingLabel + new string('.', dots);
        }
    }


    // ── 건너뛰기 ──

    /// <summary>
    /// 건너뛰기 버튼을 확보하고 리스너를 묶는다. 인스펙터 배선이 있으면 그걸 쓰고,
    /// 없으면 화면 오른쪽 상단에 만들어 붙인다 — 배선이 빠졌다고 건너뛸 방법이 없어지면 안 된다.
    /// </summary>
    private void EnsureSkipButton()
    {
        if (skipButton == null)
            skipButton = BuildSkipButton();

        if (skipButton == null)
            return;

        skipButton.onClick.AddListener(SkipCredits);

        // 롤이 시작될 때만 보여 준다.
        skipButton.gameObject.SetActive(false);
    }

    /// <summary>화면 오른쪽 상단에 고정된 건너뛰기 버튼을 만든다.</summary>
    private Button BuildSkipButton()
    {
        SimpleUiBuilder.EnsureEventSystem();

        // 크레딧·배경 위에 뜨되, 이름 입력 탭(그 시점엔 이미 숨는다)과 다투지 않을 정도의 순서.
        Canvas canvas = SimpleUiBuilder.CreateOverlayCanvas("[Skip Credits Canvas]", 200);
        canvas.transform.SetParent(transform, false);

        Button button = SimpleUiBuilder.CreateButton(
            canvas.transform, "SkipButton", skipButtonLabel, new Vector2(240f, 72f), 30f);

        // 앵커·피벗을 모두 우상단에 두므로 해상도가 바뀌어도 오른쪽 위에 붙어 있는다.
        SimpleUiBuilder.AnchorToCorner((RectTransform)button.transform, Vector2.one, skipButtonMargin);

        return button;
    }

    /// <summary>
    /// 크레딧 롤을 건너뛴다. 버튼 OnClick 에 연결하며, 인스펙터 배선용으로 public 이다.
    /// 롤과 에필로그 대기만 건너뛴다 — 에필로그·이름 입력·갤러리 저장은 그대로 진행된다.
    /// </summary>
    public void SkipCredits()
    {
        skipRequested = true;
    }

    /// <summary>건너뛰기 버튼을 보이거나 감춘다.</summary>
    private void SetSkipButtonVisible(bool visible)
    {
        if (skipButton != null)
            skipButton.gameObject.SetActive(visible);
    }


    // ── 대본·이미지 생성 ──

    /// <summary>Gemini → 실패 시 폴백 순서로 대본을 확보한다. 반드시 유효한 대본을 넘긴다.</summary>
    private IEnumerator ResolveScriptRoutine(RunResult result, Action<EndingScript> onResolved)
    {
        if (forceFallback)
        {
            // 개발용 강제 폴백이다. 의도한 것이므로 안내창을 띄우지 않는다.
            onResolved(FallbackEndingNarrator.Build(result));
            yield break;
        }

        yield return GeminiApiConfig.Load();

        EndingScript script = null;
        string failure = null;

        IEndingNarrator narrator = new GeminiEndingNarrator(narratorModel, narratorTimeout, narratorTemperature);
        yield return narrator.Generate(result, s => script = s, e => failure = e);

        if (script == null || !script.IsValid)
        {
            Debug.LogWarning($"EndingCreditController: 대본 생성 실패 — 폴백으로 진행합니다.\n  사유: {failure}");
            script = FallbackEndingNarrator.Build(result);
            scriptCameFromFallback = true;
        }

        onResolved(script);
    }

    /// <summary>
    /// API 로 크레딧을 만들지 못했으면 안내창을 띄우고 계속할지 묻는다.
    /// 정상 생성됐거나 강제 폴백 모드면 아무것도 묻지 않고 그대로 진행한다.
    ///
    /// 대본 생성을 미리 한 번 시도한 뒤에 묻는 이유는, 별도의 확인용 호출을 더 보내지 않고도
    /// "실제로 되는지"를 가장 정확하게 알 수 있기 때문이다 — 키 누락이든 한도 초과든 여기서 다 걸린다.
    /// </summary>
    private IEnumerator ConfirmFallbackRoutine(Action<bool> onResolved)
    {
        if (!scriptCameFromFallback || forceFallback)
        {
            onResolved(true);
            yield break;
        }

        if (fallbackNotice == null)
            fallbackNotice = gameObject.AddComponent<ChoicePrompt>();

        // 실패 사유는 플레이어에게 보여 주지 않는다 — 개발자용 문자열이라 화면에 나올 것이 아니다.
        // 원인은 ResolveScriptRoutine 이 콘솔에 남긴다.
        bool? choice = null;
        fallbackNotice.Show(
            fallbackNoticeHeadline,
            fallbackNoticeQuestion,
            fallbackNoticeConfirmLabel,
            fallbackNoticeCancelLabel,
            c => choice = c);

        yield return new WaitUntil(() => choice.HasValue);

        onResolved(choice.Value);
    }

    /// <summary>이미지를 생성해 배경에 페이드인한다. 실패하면 아무것도 하지 않는다(암전 유지).</summary>
    private IEnumerator ResolveImageRoutine(RunResult result, string imagePrompt)
    {
        if (forceFallback || backgroundImage == null)
            yield break;

        var generator = new EndingImageGenerator(imageProvider, imageModel, imageTimeout, useImageCache);
        Texture2D texture = null;

        yield return generator.GetOrCreate(result.CombinationKey, imagePrompt, t => texture = t);

        if (texture == null)
            yield break;

        endingImage = texture;
        backgroundImage.texture = texture;
        backgroundImage.enabled = true;

        yield return FadeBackgroundRoutine(0f, 1f, imageFadeDuration);
    }

    /// <summary>배경 이미지의 투명도를 보간한다. CanvasGroup 이 없으면 즉시 반환.</summary>
    private IEnumerator FadeBackgroundRoutine(float from, float to, float duration)
    {
        if (backgroundGroup == null)
            yield break;

        if (duration <= 0f)
        {
            backgroundGroup.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            backgroundGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }

        backgroundGroup.alpha = to;
    }


    // ── 크레딧 롤 ──

    /// <summary>
    /// 크레딧 본문을 화면 아래에서 위로 흘려보낸다.
    /// 건너뛰기 버튼이 눌리면 롤을 즉시 끝낸다.
    /// </summary>
    private IEnumerator ScrollCreditsRoutine(EndingScript script)
    {
        if (creditText == null)
            yield break;

        SetSkipButtonVisible(true);

        // 롤을 빠져나가는 경로가 여러 갈래(완주/건너뛰기/creditText 없음)라
        // 버튼 숨기기는 try-finally 로 한 곳에 모은다.
        try
        {
            yield return ScrollCreditsCore(script);
        }
        finally
        {
            SetSkipButtonVisible(false);
        }
    }

    /// <summary>실제 스크롤 루프. 버튼 표시/숨김은 <see cref="ScrollCreditsRoutine"/> 이 감싼다.</summary>
    private IEnumerator ScrollCreditsCore(EndingScript script)
    {
        creditText.text = string.Join("\n", script.credit_lines);

        RectTransform rect = creditRect != null ? creditRect : creditText.rectTransform;

        // 레이아웃이 갱신돼야 preferredHeight 가 실제 값이 된다.
        creditText.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();

        float viewportHeight = GetViewportHeight(rect);
        float contentHeight = creditText.preferredHeight;

        // 화면 아래(보이지 않는 곳)에서 시작해, 마지막 줄이 화면 위로 완전히 빠져나갈 때까지 올린다.
        float startY = -viewportHeight * 0.5f - contentHeight * 0.5f;
        float endY = viewportHeight * 0.5f + contentHeight * 0.5f;

        Vector2 pos = rect.anchoredPosition;
        pos.y = startY;
        rect.anchoredPosition = pos;

        float distance = endY - startY;
        float speed = Mathf.Max(scrollSpeed, 1f);
        float travelled = 0f;

        while (travelled < distance && !skipRequested)
        {
            travelled += speed * CurrentScrollMultiplier * Time.deltaTime;
            pos.y = Mathf.Min(startY + travelled, endY);
            rect.anchoredPosition = pos;
            yield return null;
        }
    }

    /// <summary>빨리감기 입력(스페이스바 / 마우스 좌클릭)을 누르고 있으면 배율, 아니면 1.</summary>
    private float CurrentScrollMultiplier =>
        fastForwardMultiplier > 1f && IsFastForwardHeld() ? fastForwardMultiplier : 1f;

    /// <summary>빨리감기 입력이 눌려 있는지. 스페이스바 또는 마우스 좌클릭.</summary>
    private static bool IsFastForwardHeld()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.isPressed)
            return true;

        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.isPressed;
    }

    /// <summary>
    /// 대기 중에도 빨리감기가 먹히도록, 배율만큼 시간을 앞당겨 흘려보낸다.
    /// 건너뛰기가 눌렸으면 기다리지 않는다 — 롤을 건너뛰고 나서 지연만 남아 있으면 멈춘 것처럼 보인다.
    /// </summary>
    private IEnumerator WaitSkippable(float seconds)
    {
        float remaining = seconds;
        while (remaining > 0f && !skipRequested)
        {
            remaining -= CurrentScrollMultiplier * Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>스크롤 범위 계산에 쓸 화면 높이. 부모 RectTransform 기준, 없으면 화면 해상도.</summary>
    private static float GetViewportHeight(RectTransform rect)
    {
        var parent = rect.parent as RectTransform;
        float height = parent != null ? parent.rect.height : 0f;
        return height > 1f ? height : Screen.height;
    }


    // ── 에필로그 ──

    /// <summary>크레딧을 감추고 마무리 한 문장을 화면 중앙에 띄운다.</summary>
    private void ShowEpilogue(string epilogue)
    {
        if (epilogueText == null || string.IsNullOrWhiteSpace(epilogue))
            return;

        if (creditText != null)
            creditText.gameObject.SetActive(false);

        epilogueText.text = epilogue;
        epilogueText.gameObject.SetActive(true);
    }

    /// <summary>
    /// 에필로그의 계속 버튼 OnClick 에서 호출한다.
    /// 엔딩 롤 빨리감기 입력으로는 호출되지 않는다.
    /// </summary>
    public void ContinueFromEpilogue()
    {
        if (!isWaitingForEpilogueContinue)
            return;

        isWaitingForEpilogueContinue = false;
    }

    /// <summary>에필로그를 표시한 뒤 계속 버튼이 눌릴 때까지 기다린다.</summary>
    private IEnumerator WaitForEpilogueContinueRoutine()
    {
        isWaitingForEpilogueContinue = true;

        if (epilogueContinueButton != null)
            epilogueContinueButton.SetActive(true);
        else
            Debug.LogWarning("EndingCreditController: epilogueContinueButton 이 연결되지 않아 에필로그를 진행할 수 없습니다.");

        yield return new WaitUntil(() => !isWaitingForEpilogueContinue);

        if (epilogueContinueButton != null)
            epilogueContinueButton.SetActive(false);

        if (epilogueText != null)
            epilogueText.gameObject.SetActive(false);
    }


    // ── 저장과 종료 ──

    /// <summary>
    /// 이름 입력 탭을 띄우고 확정될 때까지 기다린다.
    /// 탭이 없거나 저장 자체를 하지 않는 판이면 묻지 않고 그냥 통과한다 —
    /// 저장되지 않을 이름을 받아봐야 갈 곳이 없다.
    /// </summary>
    private IEnumerator PromptForNameRoutine(Action<string> onResolved)
    {
        if (namePrompt == null || !saveToGallery || endingImage == null)
            yield break;

        bool done = false;
        namePrompt.Show(endingImage, n =>
        {
            onResolved(n);
            done = true;
        });

        yield return new WaitUntil(() => done);
    }

    /// <summary>
    /// 이번 엔딩을 갤러리에 남긴다. 이미지가 없으면(생성 실패·폴백 모드) 저장하지 않는다 —
    /// 갤러리는 크레딧 텍스트와 이미지가 한 쌍일 때만 의미가 있다.
    /// </summary>
    private void SaveToGallery(EndingScript script, RunResult result, string worldName)
    {
        if (!saveToGallery)
            return;

        if (endingImage == null)
        {
            Debug.LogWarning("EndingCreditController: 엔딩 이미지가 없어 갤러리에 남기지 않습니다.");
            return;
        }

        GalleryStore.Save(script, endingImage, result, worldName);
    }

    /// <summary>이름을 확정하고 나면 갤러리로 넘어간다.</summary>
    private void GoToGallery()
    {
        GoToScene(gallerySceneName);
    }

    /// <summary>씬을 옮긴다. 이름이 비어 있으면 그 자리에 머문다.</summary>
    private static void GoToScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        SceneManager.LoadScene(sceneName);
    }
}
