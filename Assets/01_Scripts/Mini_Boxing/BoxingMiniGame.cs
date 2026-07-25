using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// "때려라!" 외계인 복싱 미니게임.
/// A(왼손) / D(오른손) 를 연타해 제한 시간 안에 외계인을 정해진 횟수만큼 때리면 클리어.
/// 번갈아 누를 필요는 없다 — 같은 키를 연타해도 매번 인정된다.
/// 맞을 때마다 해당 글러브가 슉 뻗고, 외계인은 맞은 방향에 맞는 Ouch 표정이 잠깐 나타난다.
/// 프리팹으로 만들어 <see cref="MiniGamePlayer"/> 가 재생한다.
/// </summary>
public class BoxingMiniGame : TimedMiniGame
{
    // 인스펙터에 확실히 노출되도록 제네릭 UnityEvent 는 구체 타입으로 선언한다.
    [Serializable] public class PunchEvent : UnityEvent<int> { }

    private enum State
    {
        Idle,
        Playing,
        Cleared,
        Failed,
    }

    [Header("글러브")]
    [SerializeField] private GlovePunch leftGlove;
    [SerializeField] private GlovePunch rightGlove;

    [Header("외계인")]
    [Tooltip("표정을 바꿀 외계인 스프라이트 렌더러")]
    [SerializeField] private SpriteRenderer alienRenderer;

    [Tooltip("평소 얼굴")]
    [SerializeField] private Sprite alienNormalSprite;

    [Tooltip("맞았을 때 얼굴")]
    [SerializeField] private Sprite alienOuchSprite;

    [Tooltip("Ouch 표정을 유지하는 시간(초)")]
    [SerializeField] private float ouchDuration = 0.15f;

    [Tooltip("오른손에 맞았을 때 Ouch 를 좌우 반전해 반대편 표정처럼 보이게 한다")]
    [SerializeField] private bool mirrorOuchForRight = true;

    [Header("규칙")]
    [Tooltip("클리어에 필요한 펀치 횟수")]
    [SerializeField] private int clearCount = 20;

    [Header("조작")]
    [Tooltip("왼손 펀치 키")]
    [SerializeField] private Key leftKey = Key.A;

    [Tooltip("오른손 펀치 키")]
    [SerializeField] private Key rightKey = Key.D;

    [Header("결과 연출")]
    [Tooltip("성공 시 나타나는 이미지(Alien_Clear). 이 이미지가 흔들린다")]
    [SerializeField] private SpriteRenderer clearImage;

    [Tooltip("실패 시 나타나는 이미지(Alien_Fail). 이때는 화면이 흔들린다")]
    [SerializeField] private SpriteRenderer failImage;

    [Tooltip("결과 이미지를 보여준 뒤 종료까지 유지하는 시간(초)")]
    [SerializeField] private float resultHoldDuration = 1.2f;

    [Tooltip("성공 시 이미지가 흔들리는 시간(초)")]
    [SerializeField] private float imageShakeDuration = 0.5f;

    [Tooltip("성공 시 이미지 흔들림 세기(월드 유닛)")]
    [SerializeField] private float imageShakeMagnitude = 0.3f;

    [Tooltip("실패 시 화면이 흔들리는 시간(초)")]
    [SerializeField] private float cameraShakeDuration = 0.4f;

    [Tooltip("실패 시 화면 흔들림 세기(월드 유닛)")]
    [SerializeField] private float cameraShakeMagnitude = 0.6f;

    [Header("실패 임팩트 플래시")]
    [Tooltip("실패 순간 화면을 붉게 덮는 플래시 유지 시간(초)")]
    [SerializeField] private float failureFlashDuration = 0.16f;

    [Tooltip("실패 순간 붉은 플래시의 최대 불투명도")]
    [Range(0f, 1f)]
    [SerializeField] private float failureFlashAlpha = 0.55f;

    [Tooltip("실패 순간 화면을 덮을 플래시 색상")]
    [SerializeField] private Color failureFlashColor = new Color(0.72f, 0.015f, 0.015f, 1f);

    [Header("이벤트")]
    [Tooltip("한 대 때릴 때마다 현재 횟수를 넘긴다")]
    public PunchEvent onPunch;

    [Tooltip("재생을 시작할 때 발화한다. 연출 컴포넌트들의 초기화를 연결한다")]
    public UnityEvent onReset;

    public UnityEvent onClear;
    public UnityEvent onFail;

    private int punchCount;
    private State state = State.Idle;
    private Coroutine ouchRoutine;
    private Coroutine resultRoutine;
    private Coroutine failureFlashRoutine;
    private CanvasGroup failureFlashCanvasGroup;

    // 외계인이 평소에 바라보던 방향. Ouch 미러링 후 되돌리기 위해 캐시한다.
    private bool alienDefaultFlipX;

    private Vector3 clearImageRestorePos;

    /// <summary>지금까지 때린 횟수.</summary>
    public int PunchCount => punchCount;

    /// <summary>실패 이미지가 나타나는 순간 자체 임팩트를 재생하므로 공통 종료 셰이크는 생략한다.</summary>
    protected override bool HasOwnFailureCameraImpact => true;

    private void Awake()
    {
        if (alienRenderer != null)
        {
            alienDefaultFlipX = alienRenderer.flipX;

            // 평소 얼굴이 비어 있으면 현재 스프라이트를 평소 얼굴로 삼는다.
            if (alienNormalSprite == null)
                alienNormalSprite = alienRenderer.sprite;
        }

        if (clearImage != null)
            clearImageRestorePos = clearImage.transform.localPosition;
    }

    protected override void OnTimedPlay()
    {
        ResetInternal();

        // 지난 판의 연출은 종료 직후가 아니라 다음 판을 시작할 때 지운다(결과를 볼 수 있도록).
        onReset.Invoke();

        state = State.Playing;
    }

    protected override void OnTimedStopAndReset()
    {
        ResetInternal();
        state = State.Idle;
    }

    /// <summary>횟수·글러브 자세·외계인 표정·결과 연출을 처음으로 되돌린다. 멱등.</summary>
    private void ResetInternal()
    {
        if (ouchRoutine != null)
        {
            StopCoroutine(ouchRoutine);
            ouchRoutine = null;
        }

        if (resultRoutine != null)
        {
            StopCoroutine(resultRoutine);
            resultRoutine = null;
        }

        if (failureFlashRoutine != null)
        {
            StopCoroutine(failureFlashRoutine);
            failureFlashRoutine = null;
        }

        if (failureFlashCanvasGroup != null)
        {
            failureFlashCanvasGroup.alpha = 0f;
            failureFlashCanvasGroup.gameObject.SetActive(false);
        }

        punchCount = 0;

        if (leftGlove != null)
            leftGlove.ResetPose();

        if (rightGlove != null)
            rightGlove.ResetPose();

        // 결과 이미지는 숨기고 위치도 복원한다.
        if (clearImage != null)
        {
            clearImage.transform.localPosition = clearImageRestorePos;
            clearImage.gameObject.SetActive(false);
        }

        if (failImage != null)
            failImage.gameObject.SetActive(false);

        // 결과 연출 때 숨겼던 외계인을 다시 보이게 한다.
        if (alienRenderer != null)
            alienRenderer.gameObject.SetActive(true);

        ShowNormalFace();
    }

    protected override void OnTimedUpdate()
    {
        HandleKeys();
    }

    /// <summary>A / D 입력을 각각 검사한다. 번갈이 강제가 없으므로 눌린 쪽마다 그대로 한 대씩 인정한다.</summary>
    private void HandleKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        // 한 프레임에 둘 다 눌렸다면 두 대로 인정한다(연타 게임이라 손해 볼 이유가 없다).
        if (keyboard[leftKey].wasPressedThisFrame)
            Punch(true);

        if (keyboard[rightKey].wasPressedThisFrame)
            Punch(false);
    }

    /// <summary>키 입력 없이 한 대 때리게 하고 싶을 때 직접 호출해도 된다.</summary>
    /// <param name="isLeft">왼손이면 true, 오른손이면 false.</param>
    public void Punch(bool isLeft)
    {
        if (state != State.Playing)
            return;

        GlovePunch glove = isLeft ? leftGlove : rightGlove;
        if (glove != null)
            glove.Punch();

        ShowOuch(isLeft);

        punchCount++;
        onPunch.Invoke(punchCount);

        if (punchCount >= clearCount)
        {
            state = State.Cleared;
            onClear.Invoke();
            resultRoutine = StartCoroutine(ResultRoutine(true));
        }
    }

    /// <summary>맞은 방향에 맞는 Ouch 표정을 잠깐 보여준다. 연타로 다시 불리면 유지 시간이 갱신된다.</summary>
    private void ShowOuch(bool isLeft)
    {
        if (alienRenderer == null || alienOuchSprite == null)
            return;

        alienRenderer.sprite = alienOuchSprite;

        // 왼손에 맞으면 원본 그대로, 오른손에 맞으면 좌우 반전해 반대편 표정처럼 보이게 한다.
        alienRenderer.flipX = mirrorOuchForRight && !isLeft ? !alienDefaultFlipX : alienDefaultFlipX;

        if (ouchRoutine != null)
            StopCoroutine(ouchRoutine);

        ouchRoutine = StartCoroutine(OuchRoutine());
    }

    private IEnumerator OuchRoutine()
    {
        yield return new WaitForSeconds(ouchDuration);

        ShowNormalFace();
        ouchRoutine = null;
    }

    /// <summary>외계인을 평소 얼굴·평소 방향으로 되돌린다.</summary>
    private void ShowNormalFace()
    {
        if (alienRenderer == null)
            return;

        if (alienNormalSprite != null)
            alienRenderer.sprite = alienNormalSprite;

        alienRenderer.flipX = alienDefaultFlipX;
    }

    /// <summary>
    /// 제한 시간을 다 써서 실패로 확정될 때 호출된다.
    /// 이미 클리어해서 성공 연출이 돌고 있다면 아무것도 하지 않는다(그 코루틴이 종료를 통지한다).
    /// 기본 <see cref="TimedMiniGame.OnTimeUp"/>(즉시 실패 통지)을 부르지 않고, 실패 연출 후 통지한다.
    /// </summary>
    protected override void OnTimeUp()
    {
        if (state == State.Cleared || state == State.Failed)
            return;

        state = State.Failed;
        onFail.Invoke();
        resultRoutine = StartCoroutine(ResultRoutine(false));
    }

    /// <summary>결과 이미지를 띄우고 흔들기 연출을 한 뒤, 잠깐 보여주고 종료를 통지한다.</summary>
    private IEnumerator ResultRoutine(bool success)
    {
        // 결과 이미지가 외계인을 대신하므로 평소 외계인은 숨긴다.
        if (alienRenderer != null)
            alienRenderer.gameObject.SetActive(false);

        if (success)
        {
            // 성공: 이미지가 나타나 이미지 자체가 흔들린다.
            if (clearImage != null)
            {
                clearImage.transform.localPosition = clearImageRestorePos;
                clearImage.gameObject.SetActive(true);
                yield return ShakeImageRoutine(clearImage);
            }
        }
        else
        {
            // 실패: 펀치 이미지로 바뀌는 바로 그 프레임에 셰이크와 붉은 플래시를 터뜨린다.
            if (failImage != null)
                failImage.gameObject.SetActive(true);

            PlayFailureImpact();
        }

        if (resultHoldDuration > 0f)
            yield return new WaitForSeconds(resultHoldDuration);

        resultRoutine = null;

        // 시간 초과 경로에서는 이미 resultLocked=true 라 FailImmediately 가 무시되므로
        // 베이스의 ReportFinished 를 직접 호출한다.
        ReportFinished(success);
    }

    /// <summary>결과 이미지를 제자리에서 부르르 흔든다. 시간이 지날수록 잦아든다.</summary>
    private IEnumerator ShakeImageRoutine(SpriteRenderer image)
    {
        Transform t = image.transform;
        Vector3 restore = clearImageRestorePos;

        float time = 0f;
        while (time < imageShakeDuration)
        {
            time += Time.deltaTime;
            float damper = 1f - Mathf.Clamp01(time / imageShakeDuration);
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (imageShakeMagnitude * damper);
            t.localPosition = restore + offset;
            yield return null;
        }

        t.localPosition = restore;
    }

    /// <summary>실패 이미지 전환과 동시에 일회성 카메라 셰이크와 붉은 플래시를 재생한다.</summary>
    private void PlayFailureImpact()
    {
        if (cameraShakeDuration > 0f && cameraShakeMagnitude > 0f)
            CameraEffectManager.Instance.Shake(cameraShakeDuration, cameraShakeMagnitude);

        if (failureFlashDuration <= 0f || failureFlashAlpha <= 0f)
            return;

        EnsureFailureFlash();
        failureFlashRoutine = StartCoroutine(FailureFlashRoutine());
    }

    /// <summary>프리팹 연결 없이 사용할 수 있는 최상단 풀스크린 플래시를 준비한다.</summary>
    private void EnsureFailureFlash()
    {
        if (failureFlashCanvasGroup != null)
            return;

        GameObject canvasObject = new GameObject(
            "FailureImpactFlash",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        failureFlashCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
        failureFlashCanvasGroup.interactable = false;
        failureFlashCanvasGroup.blocksRaycasts = false;
        failureFlashCanvasGroup.alpha = 0f;

        GameObject imageObject = new GameObject("RedFlash", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = failureFlashColor;
        image.raycastTarget = false;
    }

    /// <summary>첫 프레임에 강하게 나타난 뒤 빠르게 사라지는 붉은 타격 플래시.</summary>
    private IEnumerator FailureFlashRoutine()
    {
        failureFlashCanvasGroup.gameObject.SetActive(true);

        float duration = Mathf.Max(0.0001f, failureFlashDuration);
        float peakAlpha = Mathf.Clamp01(failureFlashAlpha);
        float elapsed = 0f;
        failureFlashCanvasGroup.alpha = peakAlpha;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);
            failureFlashCanvasGroup.alpha = Mathf.Lerp(peakAlpha, 0f, ratio * ratio);
            yield return null;
        }

        failureFlashCanvasGroup.alpha = 0f;
        failureFlashCanvasGroup.gameObject.SetActive(false);
        failureFlashRoutine = null;
    }
}
