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
///   롤 종료 → 에필로그 표시 → (이미지 합류) → 이름 입력 탭 → 갤러리 저장 → onCreditsFinished → 갤러리 씬
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

    [Tooltip("대본 생성 타임아웃(초). 초과하면 폴백 텍스트로 진행한다")]
    [SerializeField] private int narratorTimeout = 20;

    [Tooltip("엔딩 이미지 생성처.\n" +
        "Pollinations: 키 불필요·무료 (기본)\n" +
        "Gemini: 결제를 활성화한 프로젝트에서만 동작 (무료 티어 일일 한도 0)")]
    [SerializeField] private EndingImageProvider imageProvider = EndingImageProvider.Pollinations;

    [Tooltip("imageProvider 가 Gemini 일 때 쓸 이미지 모델")]
    [SerializeField] private string imageModel = "gemini-3.1-flash-image";

    [Tooltip("이미지 생성 타임아웃(초)")]
    [SerializeField] private int imageTimeout = 60;

    [Tooltip("체크하면 API 를 호출하지 않고 항상 폴백 텍스트를 쓴다. 연출만 손볼 때 켜면 편하다.\n" +
        "이미지 생성도 함께 건너뛰므로 갤러리에는 저장되지 않는다")]
    [SerializeField] private bool forceFallback;

    [Tooltip("끄면 이번 엔딩을 갤러리에 남기지 않는다. 연출 테스트로 저장본이 지저분해질 때 끄면 된다")]
    [SerializeField] private bool saveToGallery = true;

    [Header("종료 후")]
    [Tooltip("이름 확정 뒤 넘어갈 씬 이름. 비워두면 씬을 옮기지 않는다. Build Settings 에 등록돼 있어야 한다")]
    [SerializeField] private string gallerySceneName = "00000_Gallery";

    [Header("이벤트")]
    [Tooltip("에필로그·이름 입력까지 끝나면 발화. 씬 이동 직전에 불린다")]
    public UnityEvent onCreditsFinished;

    /// <summary>생성에 성공한 엔딩 이미지. 갤러리 저장에 그대로 쓴다. 실패했으면 null.</summary>
    private Texture2D endingImage;

    private void Start()
    {
        StartCoroutine(RunEndingRoutine());
    }

    private IEnumerator RunEndingRoutine()
    {
        PrepareInitialState();

        RunResult result = RunResult.Instance;

        // 대본 생성과 암전 여운을 동시에 굴린다.
        EndingScript script = null;
        Coroutine narration = StartCoroutine(ResolveScriptRoutine(result, s => script = s));

        yield return new WaitForSeconds(leadInDuration);
        yield return narration;

        // ResolveScriptRoutine 은 항상 유효한 대본을 채워 넣는다(최악의 경우 폴백).
        // 방어적으로 한 번 더 확인해 엔딩이 멈추는 일만은 막는다.
        if (script == null || !script.IsValid)
            script = FallbackEndingNarrator.Build(result);

        // 이미지는 롤과 병렬로 생성한다 — 롤이 끝나기 전에만 도착하면 된다.
        Coroutine imaging = StartCoroutine(ResolveImageRoutine(result, script.image_prompt));

        yield return ScrollCreditsRoutine(script);

        yield return WaitSkippable(epilogueDelay);
        ShowEpilogue(script.epilogue);

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

    /// <summary>이름을 확정하고 나면 갤러리로 넘어간다.</summary>
    private void GoToGallery()
    {
        if (string.IsNullOrEmpty(gallerySceneName))
            return;

        SceneManager.LoadScene(gallerySceneName);
    }

    private void PrepareInitialState()
    {
        if (creditText != null)
            creditText.text = string.Empty;

        if (epilogueText != null)
            epilogueText.gameObject.SetActive(false);

        if (backgroundGroup != null)
            backgroundGroup.alpha = 0f;

        if (backgroundImage != null)
            backgroundImage.enabled = false;
    }

    /// <summary>Gemini → 실패 시 폴백 순서로 대본을 확보한다. 반드시 유효한 대본을 넘긴다.</summary>
    private IEnumerator ResolveScriptRoutine(RunResult result, Action<EndingScript> onResolved)
    {
        if (forceFallback)
        {
            onResolved(FallbackEndingNarrator.Build(result));
            yield break;
        }

        yield return GeminiApiConfig.Load();

        EndingScript script = null;
        string failure = null;

        IEndingNarrator narrator = new GeminiEndingNarrator(narratorModel, narratorTimeout);
        yield return narrator.Generate(result, s => script = s, e => failure = e);

        if (script == null || !script.IsValid)
        {
            Debug.LogWarning($"EndingCreditController: 대본 생성 실패 — 폴백으로 진행합니다. ({failure})");
            script = FallbackEndingNarrator.Build(result);
        }

        onResolved(script);
    }

    /// <summary>이미지를 생성해 배경에 페이드인한다. 실패하면 아무것도 하지 않는다(암전 유지).</summary>
    private IEnumerator ResolveImageRoutine(RunResult result, string imagePrompt)
    {
        if (forceFallback || backgroundImage == null)
            yield break;

        var generator = new EndingImageGenerator(imageProvider, imageModel, imageTimeout);
        Texture2D texture = null;

        yield return generator.GetOrCreate(result.CombinationKey, imagePrompt, t => texture = t);

        if (texture == null)
            yield break;

        endingImage = texture;
        backgroundImage.texture = texture;
        backgroundImage.enabled = true;

        yield return FadeBackgroundRoutine(0f, 1f, imageFadeDuration);
    }

    private IEnumerator FadeBackgroundRoutine(float from, float to, float duration)
    {
        if (backgroundGroup == null)
        {
            yield break;
        }

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

    /// <summary>크레딧 본문을 화면 아래에서 위로 흘려보낸다.</summary>
    private IEnumerator ScrollCreditsRoutine(EndingScript script)
    {
        if (creditText == null)
            yield break;

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

        while (travelled < distance)
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

    private static bool IsFastForwardHeld()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.isPressed)
            return true;

        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.isPressed;
    }

    /// <summary>대기 중에도 빨리감기가 먹히도록, 배율만큼 시간을 앞당겨 흘려보낸다.</summary>
    private IEnumerator WaitSkippable(float seconds)
    {
        float remaining = seconds;
        while (remaining > 0f)
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

    private void ShowEpilogue(string epilogue)
    {
        if (epilogueText == null || string.IsNullOrWhiteSpace(epilogue))
            return;

        epilogueText.text = epilogue;
        epilogueText.gameObject.SetActive(true);
    }
}
