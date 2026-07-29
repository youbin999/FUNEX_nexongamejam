using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// "새겨라!" 상형문자 따라 긋기 미니게임.
/// 화면에 점선으로 된 획이 떠 있고, 마우스를 누른 채(Hold &amp; Drag) 시작점부터 끝점까지 훑고 지나간다.
/// 마우스를 떼거나 제한 시간이 끝나는 순간, 시작점부터 연속으로 지나온 비율이 기준 이상이면 성공.
/// 획 경로는 <see cref="pathAnchors"/> 를 지나는 곡선을 따라 점(히트박스)을 자동 생성한다.
/// 프리팹으로 만들어 <see cref="MiniGamePlayer"/> 가 재생한다.
/// </summary>
public class SangMiniGame : TimedMiniGame
{
    // 인스펙터에 노출할 제네릭 UnityEvent 는 구체 타입 서브클래스로 선언한다(Unity 직렬화 제약).
    [Serializable] public class ProgressEvent : UnityEvent<float> { }

    private enum State
    {
        Idle,
        Playing,
        Resolved,
    }

    [Header("카메라")]
    [Tooltip("클릭 좌표 변환에 쓸 카메라. 비워두면 Camera.main 을 쓴다")]
    [SerializeField] private Camera targetCamera;

    [Header("획 경로")]
    [Tooltip("획의 꼭짓점(제어점). 시작 → 끝 순서로 배치한다. 이 점들을 지나는 곡선을 따라 점선이 깔린다")]
    [SerializeField] private Transform[] pathAnchors;

    [Tooltip("생성된 점들을 담을 부모. 비워두면 이 오브젝트 아래에 만든다")]
    [SerializeField] private Transform dotContainer;

    [Header("점선")]
    [Tooltip("점 하나에 쓸 스프라이트")]
    [SerializeField] private Sprite dotSprite;

    [Tooltip("점 사이 간격(월드 유닛). 경로 길이에 따라 점 개수가 자동으로 정해진다")]
    [SerializeField] private float dotSpacing = 0.4f;

    [Tooltip("점 하나의 스케일(월드 유닛)")]
    [SerializeField] private float dotScale = 0.25f;

    [Tooltip("점이 그려질 정렬 순서")]
    [SerializeField] private int dotSortingOrder = 7;

    [Header("판정 규칙")]
    [Tooltip("포인터가 이 반경 안에 들어오면 해당 점을 지난 것으로 인정한다(월드 유닛)")]
    [SerializeField] private float hitRadius = 0.5f;

    [Tooltip("빠른 드래그로 점을 건너뛰어도 인정하도록, 앞쪽으로 한 번에 스냅 허용할 점 개수")]
    [SerializeField] private int lookAhead = 4;

    [Tooltip("성공으로 인정할 최소 진행 비율(0~1)")]
    [Range(0f, 1f)]
    [SerializeField] private float successThreshold = 0.8f;

    [Tooltip("점선 경로에서 이 반경보다 더 벗어나면 즉시 실패한다(월드 유닛). hitRadius 보다 크게 잡는다")]
    [SerializeField] private float failRadius = 0.8f;

    [Header("색")]
    [Tooltip("아직 지나지 않은 점 색")]
    [SerializeField] private Color uncoveredColor = new Color(1f, 1f, 1f, 0.35f);

    [Tooltip("지나온 점 색")]
    [SerializeField] private Color coveredColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Tooltip("시작점 강조 색")]
    [SerializeField] private Color startColor = new Color(0.3f, 1f, 0.4f, 1f);

    [Header("긋는 선")]
    [Tooltip("선에 쓸 머티리얼. 비워두면 Sprites/Default 로 런타임 생성한다")]
    [SerializeField] private Material lineMaterial;

    [Tooltip("선 두께(월드 유닛)")]
    [SerializeField] private float lineWidth = 0.12f;

    [Tooltip("선 색")]
    [SerializeField] private Color lineColor = new Color(0.15f, 0.1f, 0.05f, 1f);

    [Tooltip("선이 그려질 정렬 순서(점보다 위)")]
    [SerializeField] private int lineSortingOrder = 6;

    [Tooltip("이 거리 이상 움직였을 때만 선에 점을 추가한다(월드 유닛)")]
    [SerializeField] private float lineMinPointDistance = 0.05f;

    [Header("결과 연출 (성공/실패)")]
    [Tooltip("평소 배경 오브젝트. 성공 시 끈다")]
    [SerializeField] private GameObject normalBackground;

    [Tooltip("성공 배경 오브젝트(Sang_ClearBG). 성공 시 켠다")]
    [SerializeField] private GameObject clearBackground;

    [Tooltip("성공 시 페이드인할 썸네일(Sang_ClearThumb)")]
    [SerializeField] private SpriteRenderer clearThumb;

    [Tooltip("실패 시 페이드인할 썸네일(Sang_FailThumb)")]
    [SerializeField] private SpriteRenderer failThumb;

    [Tooltip("썸네일이 스르륵 페이드인되는 시간(초)")]
    [SerializeField] private float resultFadeDuration = 0.6f;

    [Tooltip("결과를 보여준 뒤 종료까지 유지하는 시간(초)")]
    [SerializeField] private float resultHoldDuration = 0.9f;

    [Header("긋는 소리")]
    [Tooltip("긋는 동안 계속 재생할 AudioSource. 이 프리팹 안에 두어야 게임이 끝나면 같이 멈춘다.\n" +
        "Loop 를 켜 두고, 마우스를 누르고 있는 동안에만 재생된다")]
    [SerializeField] private AudioSource drawLoopSource;

    [Header("이벤트")]
    [Tooltip("진행 비율(0~1)을 넘긴다. Image.fillAmount 등에 연결")]
    public ProgressEvent onProgressChanged;

    public UnityEvent onClear;
    public UnityEvent onFail;

    // 자동 생성된 점들. 인덱스 순서 = 시작 → 끝.
    private readonly List<SpriteRenderer> dots = new List<SpriteRenderer>();
    private readonly List<Vector2> dotPositions = new List<Vector2>();

    private State state = State.Idle;
    private int progressIndex = -1; // 시작점부터 연속으로 지나온 마지막 점 인덱스. -1 = 아직 없음.
    private bool dragging;
    private bool dotsBuilt;

    // 드래그 궤적을 그리는 선.
    private LineRenderer lineRenderer;
    private Vector2 lastLinePoint;
    private bool hasLinePoint;

    // 성공/실패 결과 연출 코루틴.
    private Coroutine resultRoutine;

    /// <summary>카메라를 확보하고 점과 선을 만들어 둔다.</summary>
    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        BuildDots();
        BuildLine();
    }

    /// <summary>게임을 시작한다. 호출 시점에 타이머는 이미 0으로 초기화돼 있다.</summary>
    protected override void OnTimedPlay()
    {
        ResetInternal();
        state = State.Playing;
    }

    /// <summary>게임을 중단하고 초기 상태로 되돌린다. 멱등하다.</summary>
    protected override void OnTimedStopAndReset()
    {
        if (resultRoutine != null)
        {
            StopCoroutine(resultRoutine);
            resultRoutine = null;
        }

        ResetInternal();
        state = State.Idle;
    }

    /// <summary>진행 상태·점 색·배경·썸네일을 초기값으로 되돌린다. 멱등.</summary>
    private void ResetInternal()
    {
        progressIndex = -1;
        dragging = false;
        StopDrawSound();

        // 점·선을 다시 보이게 하고 색을 초기화한다.
        SetDotsVisible(true);
        for (int i = 0; i < dots.Count; i++)
        {
            if (dots[i] != null)
                dots[i].color = i == 0 ? startColor : uncoveredColor;
        }

        if (lineRenderer != null)
            lineRenderer.positionCount = 0;
        hasLinePoint = false;

        // 배경은 평소 배경으로, 결과 썸네일은 숨긴다.
        if (normalBackground != null)
            normalBackground.SetActive(true);
        if (clearBackground != null)
            clearBackground.SetActive(false);
        HideThumb(clearThumb);
        HideThumb(failThumb);

        onProgressChanged.Invoke(0f);
    }

    /// <summary>긋는 소리를 시작한다. 이미 재생 중이면 그대로 둔다.</summary>
    private void StartDrawSound()
    {
        if (drawLoopSource == null || drawLoopSource.isPlaying)
            return;

        drawLoopSource.Play();
    }

    /// <summary>긋는 소리를 멈춘다. 멱등.</summary>
    private void StopDrawSound()
    {
        if (drawLoopSource != null && drawLoopSource.isPlaying)
            drawLoopSource.Stop();
    }

    /// <summary>점(과 궤적 선)을 통째로 보이거나 숨긴다. 둘 다 dotContainer 아래에 있다.</summary>
    private void SetDotsVisible(bool visible)
    {
        if (dotContainer != null)
        {
            dotContainer.gameObject.SetActive(visible);
            return;
        }

        for (int i = 0; i < dots.Count; i++)
        {
            if (dots[i] != null)
                dots[i].gameObject.SetActive(visible);
        }
    }

    /// <summary>썸네일을 알파 0으로 되돌리고 비활성화한다.</summary>
    private static void HideThumb(SpriteRenderer thumb)
    {
        if (thumb == null)
            return;

        Color c = thumb.color;
        c.a = 0f;
        thumb.color = c;
        thumb.gameObject.SetActive(false);
    }

    /// <summary>매 프레임 입력과 진행을 처리한다. 결과가 확정된 뒤에는 불리지 않는다.</summary>
    protected override void OnTimedUpdate()
    {
        if (state != State.Playing || dots.Count == 0)
            return;

        if (!TryGetPointer(out Vector2 screenPos, out bool pressed, out bool released, out bool isPressed))
            return;

        // 드래그 시작: 시작점 근처(look-ahead 범위)에서 눌러야 인정한다.
        if (!dragging)
        {
            if (pressed)
            {
                dragging = true;
                StartDrawSound();
            }
            else
            {
                return;
            }
        }

        // 드래그 중 손을 떼면 그 순간의 진행 비율로 즉시 판정한다.
        if (released || !isPressed)
        {
            dragging = false;
            StopDrawSound();
            EvaluateAndResolve();
            return;
        }

        Vector2 world = ScreenToWorld(screenPos);
        AppendLinePoint(world);
        AdvanceAlongPath(world);

        // 획을 타기 시작한 뒤(progressIndex >= 0) 점선 경로에서 크게 벗어나면 즉시 실패.
        if (progressIndex >= 0 && IsOffPath(world))
            Resolve(false);
    }

    /// <summary>
    /// 포인터 위치 기준으로 진행 인덱스를 앞으로 전진시킨다(뒤로는 가지 않는다).
    /// 프레임 사이 빠른 드래그를 허용하도록 앞쪽 <see cref="lookAhead"/> 개까지 스냅한다.
    /// </summary>
    private void AdvanceAlongPath(Vector2 worldPoint)
    {
        int limit = Mathf.Min(progressIndex + Mathf.Max(1, lookAhead), dots.Count - 1);
        float sqrRadius = hitRadius * hitRadius;

        int newIndex = progressIndex;
        for (int i = progressIndex + 1; i <= limit; i++)
        {
            if ((dotPositions[i] - worldPoint).sqrMagnitude <= sqrRadius)
                newIndex = i; // 범위 안에서 가장 앞선 점까지 인정
        }

        if (newIndex == progressIndex)
            return;

        for (int i = progressIndex + 1; i <= newIndex; i++)
        {
            if (dots[i] != null)
                dots[i].color = coveredColor;
        }

        progressIndex = newIndex;
        onProgressChanged.Invoke(Coverage);
    }

    /// <summary>시작점부터 연속으로 지나온 비율(0~1).</summary>
    private float Coverage => dots.Count > 0 ? (progressIndex + 1) / (float)dots.Count : 0f;

    /// <summary>현재 진행 비율로 성공/실패를 가르고 통지한다(떼기/시간종료 시).</summary>
    private void EvaluateAndResolve()
    {
        Resolve(Coverage >= successThreshold);
    }

    /// <summary>성공/실패를 확정하고 결과 연출을 시작한다. 한 번만 실행된다.</summary>
    private void Resolve(bool success)
    {
        if (state != State.Playing)
            return;

        state = State.Resolved;
        dragging = false;
        StopDrawSound();

        if (success)
            onClear.Invoke();
        else
            onFail.Invoke();

        resultRoutine = StartCoroutine(ResultRoutine(success));
    }

    /// <summary>결과 연출(점 숨김 → 성공 시 배경 교체 → 썸네일 페이드인 → 유지) 후 종료를 통지한다.</summary>
    private IEnumerator ResultRoutine(bool success)
    {
        // 성공/실패 모두 점(과 궤적 선)을 없앤다.
        SetDotsVisible(false);

        // 성공일 때만 평소 배경을 성공 배경(Sang_ClearBG)으로 바꾼다.
        if (success)
        {
            if (normalBackground != null)
                normalBackground.SetActive(false);
            if (clearBackground != null)
                clearBackground.SetActive(true);
        }

        SpriteRenderer thumb = success ? clearThumb : failThumb;
        if (thumb != null)
            yield return FadeInThumb(thumb);

        if (resultHoldDuration > 0f)
            yield return new WaitForSeconds(resultHoldDuration);

        resultRoutine = null;

        // 시간 초과 경로에서는 이미 resultLocked=true 라 FailImmediately 가 무시되므로
        // 베이스의 ReportFinished 를 직접 호출한다.
        ReportFinished(success);
    }

    /// <summary>썸네일을 알파 0에서 1로 스르륵 페이드인한다.</summary>
    private IEnumerator FadeInThumb(SpriteRenderer thumb)
    {
        thumb.gameObject.SetActive(true);
        Color c = thumb.color;
        c.a = 0f;
        thumb.color = c;

        float t = 0f;
        while (t < resultFadeDuration)
        {
            t += Time.deltaTime;
            c.a = resultFadeDuration > 0f ? Mathf.Clamp01(t / resultFadeDuration) : 1f;
            thumb.color = c;
            yield return null;
        }

        c.a = 1f;
        thumb.color = c;
    }

    /// <summary>
    /// 포인터가 현재 진행 구간의 점선에서 <see cref="failRadius"/> 보다 더 멀리 떨어졌는지 검사한다.
    /// 진행 인덱스 주변 창(window)의 점들과의 최소 거리로 판정한다.
    /// </summary>
    private bool IsOffPath(Vector2 worldPoint)
    {
        int from = Mathf.Max(0, progressIndex - 2);
        int to = Mathf.Min(dots.Count - 1, progressIndex + Mathf.Max(1, lookAhead) + 2);

        float minSqr = float.MaxValue;
        for (int i = from; i <= to; i++)
        {
            float sqr = (dotPositions[i] - worldPoint).sqrMagnitude;
            if (sqr < minSqr)
                minSqr = sqr;
        }

        return minSqr > failRadius * failRadius;
    }

    /// <summary>제한 시간이 끝나는 순간에도 같은 기준으로 판정한다(기본 무조건 실패를 대체).</summary>
    protected override void OnTimeUp()
    {
        EvaluateAndResolve();
    }

    /// <summary>드래그 궤적을 그릴 LineRenderer 를 한 번만 만든다.</summary>
    private void BuildLine()
    {
        if (lineRenderer != null)
            return;

        Transform parent = dotContainer != null ? dotContainer : transform;
        var go = new GameObject("DrawLine");
        go.transform.SetParent(parent, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 0;
        lr.widthMultiplier = lineWidth;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
        lr.textureMode = LineTextureMode.Stretch;
        lr.alignment = LineAlignment.View;
        lr.sortingOrder = lineSortingOrder;
        lr.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lineColor;
        lr.endColor = lineColor;

        lineRenderer = lr;
    }

    /// <summary>드래그 궤적 선에 점을 추가한다. 너무 촘촘하지 않게 최소 간격을 둔다.</summary>
    private void AppendLinePoint(Vector2 worldPoint)
    {
        if (lineRenderer == null)
            return;

        if (hasLinePoint && (worldPoint - lastLinePoint).sqrMagnitude < lineMinPointDistance * lineMinPointDistance)
            return;

        int count = lineRenderer.positionCount;
        lineRenderer.positionCount = count + 1;
        lineRenderer.SetPosition(count, new Vector3(worldPoint.x, worldPoint.y, 0f));

        lastLinePoint = worldPoint;
        hasLinePoint = true;
    }

    /// <summary>
    /// <see cref="pathAnchors"/> 를 지나는 Catmull-Rom 곡선을 <see cref="dotSpacing"/> 간격으로 샘플해
    /// 점(SpriteRenderer)들을 한 번만 생성한다.
    /// </summary>
    private void BuildDots()
    {
        if (dotsBuilt)
            return;

        dotsBuilt = true;

        if (pathAnchors == null || pathAnchors.Length < 2)
        {
            Debug.LogWarning("SangMiniGame: pathAnchors 가 2개 미만이라 획을 만들 수 없습니다.", this);
            return;
        }

        Transform parent = dotContainer != null ? dotContainer : transform;

        List<Vector2> anchors = new List<Vector2>(pathAnchors.Length);
        foreach (Transform anchor in pathAnchors)
        {
            if (anchor != null)
                anchors.Add(anchor.position);
        }

        if (anchors.Count < 2)
            return;

        SampleCatmullRom(anchors, Mathf.Max(0.01f, dotSpacing), dotPositions);

        for (int i = 0; i < dotPositions.Count; i++)
        {
            var go = new GameObject($"Dot_{i}");
            go.transform.SetParent(parent, false);
            go.transform.position = dotPositions[i];
            go.transform.localScale = Vector3.one * dotScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dotSprite;
            sr.sortingOrder = dotSortingOrder;
            sr.color = i == 0 ? startColor : uncoveredColor;

            dots.Add(sr);
        }
    }

    /// <summary>
    /// 제어점들을 지나는 Catmull-Rom 스플라인을 대략 <paramref name="spacing"/> 간격으로 균등 샘플한다.
    /// 끝점을 복제해 양 끝의 접선을 만든다.
    /// </summary>
    private static void SampleCatmullRom(List<Vector2> anchors, float spacing, List<Vector2> result)
    {
        result.Clear();

        // 곡선을 촘촘히 샘플한 뒤, 누적 길이를 따라 spacing 간격으로 재추출한다.
        List<Vector2> fine = new List<Vector2>();
        const int stepsPerSegment = 24;

        for (int seg = 0; seg < anchors.Count - 1; seg++)
        {
            Vector2 p0 = anchors[Mathf.Max(seg - 1, 0)];
            Vector2 p1 = anchors[seg];
            Vector2 p2 = anchors[seg + 1];
            Vector2 p3 = anchors[Mathf.Min(seg + 2, anchors.Count - 1)];

            for (int s = 0; s < stepsPerSegment; s++)
            {
                float t = s / (float)stepsPerSegment;
                fine.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }
        fine.Add(anchors[anchors.Count - 1]);

        // 균등 간격 재추출.
        result.Add(fine[0]);
        float acc = 0f;
        for (int i = 1; i < fine.Count; i++)
        {
            acc += Vector2.Distance(fine[i - 1], fine[i]);
            if (acc >= spacing)
            {
                result.Add(fine[i]);
                acc = 0f;
            }
        }

        Vector2 last = fine[fine.Count - 1];
        if (result[result.Count - 1] != last)
            result.Add(last);
    }

    /// <summary>네 점을 지나는 Catmull-Rom 곡선 위의 한 점. 점들을 부드럽게 이을 때 쓴다.</summary>
    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2f * p1)
            + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    /// <summary>화면 좌표를 월드 좌표로 바꾼다. 카메라가 없으면 그대로 돌려준다.</summary>
    private Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        if (targetCamera == null)
            return screenPosition;

        return targetCamera.ScreenToWorldPoint(screenPosition);
    }

    /// <summary>마우스/터치 포인터 상태를 통합해서 읽는다.</summary>
    private static bool TryGetPointer(out Vector2 screenPosition, out bool pressedThisFrame, out bool releasedThisFrame, out bool isPressed)
    {
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            screenPosition = mouse.position.ReadValue();
            pressedThisFrame = mouse.leftButton.wasPressedThisFrame;
            releasedThisFrame = mouse.leftButton.wasReleasedThisFrame;
            isPressed = mouse.leftButton.isPressed;
            return true;
        }

        Touchscreen touch = Touchscreen.current;
        if (touch != null)
        {
            screenPosition = touch.primaryTouch.position.ReadValue();
            pressedThisFrame = touch.primaryTouch.press.wasPressedThisFrame;
            releasedThisFrame = touch.primaryTouch.press.wasReleasedThisFrame;
            isPressed = touch.primaryTouch.press.isPressed;
            return true;
        }

        screenPosition = default;
        pressedThisFrame = false;
        releasedThisFrame = false;
        isPressed = false;
        return false;
    }
}
