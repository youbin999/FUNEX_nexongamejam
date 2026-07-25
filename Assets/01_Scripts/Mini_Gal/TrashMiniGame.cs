using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// "우주 쓰레기 버리기" 미니게임.
/// 쓰레기들이 천천히 둥둥 떠다니고, 마우스로 잡아 끌어 <see cref="pointArea"/>(Point) 안에 넣으면 버려진다.
/// 버려진 쓰레기는 빙빙 돌며 작아지다 사라진다. 제한 시간 안에 전부 버리면 클리어.
/// Point 밖에서 놓으면 실패 없이 그 자리에서 다시 떠다닌다.
/// 프리팹으로 만들어 <see cref="MiniGamePlayer"/> 가 재생한다.
/// </summary>
public class TrashMiniGame : TimedMiniGame
{
    // 인스펙터에 확실히 노출되도록 제네릭 UnityEvent 는 구체 타입으로 선언한다.
    [Serializable] public class TrashEvent : UnityEvent<int> { }

    private enum State
    {
        Idle,
        Playing,
        Cleared,
        Failed,
    }

    [Header("카메라")]
    [Tooltip("클릭 좌표 변환에 쓸 카메라. 비워두면 Camera.main 을 쓴다")]
    [SerializeField] private Camera targetCamera;

    [Header("쓰레기")]
    [Tooltip("떠다닐 쓰레기들. 여기 등록된 걸 전부 버려야 클리어된다")]
    [SerializeField] private SpaceTrash[] trashes;

    [Header("버리는 곳")]
    [Tooltip("쓰레기를 버릴 영역(Point). 이 스프라이트의 영역이 곧 판정 범위다")]
    [SerializeField] private SpriteRenderer pointArea;

    [Tooltip("판정을 이만큼 넓혀준다(월드 유닛). 5초 안에 끝나도록 넉넉하게 잡는다")]
    [SerializeField] private float pointPadding = 0.3f;

    [Header("성공 연출")]
    [Tooltip("성공 시 팝업으로 튀어나올 Clear! 이미지")]
    [SerializeField] private SpriteRenderer clearPopup;

    [Tooltip("Clear! 가 튀어나오는 데 걸리는 시간(초)")]
    [SerializeField] private float popupDuration = 0.3f;

    [Tooltip("Clear! 가 튀어나오기 시작할 때의 크기 배율")]
    [SerializeField] private float popupStartScale = 0.3f;

    [Tooltip("성공 시 밑에서 올라올 썸네일들. 배치해 둔 자리가 최종 위치가 된다")]
    [SerializeField] private SpriteRenderer[] riseThumbs;

    [Tooltip("썸네일이 얼마나 아래에서부터 올라오는지(월드 유닛)")]
    [SerializeField] private float riseDistance = 6f;

    [Tooltip("썸네일 하나가 올라오는 데 걸리는 시간(초)")]
    [SerializeField] private float riseDuration = 0.4f;

    [Tooltip("썸네일들이 차례로 올라오는 간격(초). 0이면 동시에 올라온다")]
    [SerializeField] private float riseStagger = 0.12f;

    [Header("실패 연출")]
    [Tooltip("실패 시 밑에서 올라올 썸네일들. 배치해 둔 자리가 최종 위치가 된다")]
    [SerializeField] private SpriteRenderer[] failThumbs;

    [Tooltip("실패 시 남은 쓰레기가 날아가는 방향. 기본값은 왼쪽")]
    [SerializeField] private Vector2 trashFlyDirection = new Vector2(-1f, 0.15f);

    [Tooltip("실패 시 남은 쓰레기가 날아가는 속도(월드 유닛/초)")]
    [SerializeField] private float trashFlySpeed = 16f;

    [Tooltip("썸네일이 다 올라온 뒤 빨려 들어가기 시작할 때까지 기다리는 시간(초)")]
    [SerializeField] private float failThumbHold = 0.5f;

    [Tooltip("실패 썸네일이 Point 로 빨려 들어가는 데 걸리는 시간(초)")]
    [SerializeField] private float failSuckDuration = 0.5f;

    [Tooltip("빨려 들어가면서 도는 속도(도/초)")]
    [SerializeField] private float failSuckSpinSpeed = 720f;

    [Tooltip("실패 연출이 끝난 뒤 종료를 통지하기까지 기다리는 시간(초)")]
    [SerializeField] private float failHoldDuration = 0.4f;

    [Header("사운드")]
    [Tooltip("효과음을 재생할 AudioSource. 쓰레기가 아니라 이 오브젝트에 붙여야 " +
        "버려질 때 오브젝트가 꺼져도 소리가 끊기지 않는다")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("쓰레기를 집는 순간 나는 소리")]
    [SerializeField] private AudioClip pickUpClip;

    [Tooltip("쓰레기를 Point 에 넣어 빨려 들어갈 때 나는 소리. 실패 썸네일이 빨려들 때도 같이 쓴다")]
    [SerializeField] private AudioClip suctionClip;

    [Tooltip("클리어했을 때 나는 소리(박수)")]
    [SerializeField] private AudioClip clearClip;

    [Tooltip("실패했을 때 나는 소리(야유)")]
    [SerializeField] private AudioClip failClip;

    [Tooltip("집는 소리 크기. 자주 반복되므로 조금 작게 잡는다")]
    [Range(0f, 1f)]
    [SerializeField] private float pickUpVolume = 0.7f;

    [Tooltip("빨려 들어가는 소리 크기")]
    [Range(0f, 1f)]
    [SerializeField] private float suctionVolume = 1f;

    [Tooltip("클리어 소리 크기")]
    [Range(0f, 1f)]
    [SerializeField] private float clearVolume = 1f;

    [Tooltip("실패 소리 크기")]
    [Range(0f, 1f)]
    [SerializeField] private float failVolume = 1f;

    [Tooltip("재생할 때마다 이 범위 안에서 음높이를 무작위로 바꾼다. 같은 소리가 반복돼도 덜 기계적으로 들린다")]
    [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    [Header("이벤트")]
    [Tooltip("하나 버릴 때마다 지금까지 버린 개수를 넘긴다")]
    public TrashEvent onDisposed;

    public UnityEvent onClear;
    public UnityEvent onFail;

    private State state = State.Idle;
    private SpaceTrash held;
    private Coroutine clearRoutine;

    private Coroutine failRoutine;

    // 연출 전 원래 자세. 리셋 때 여기로 되돌린다.
    private Vector3 popupRestScale;
    private Vector3[] thumbRestPositions;
    private Vector3[] failThumbRestPositions;
    private Vector3[] thumbRestScales;
    private Vector3[] failThumbRestScales;

    // 잡은 지점과 쓰레기 중심의 차이. 잡은 부분이 손에서 튀지 않게 유지한다.
    private Vector2 grabOffset;
    private int disposedCount;

    /// <summary>지금까지 버린 개수.</summary>
    public int DisposedCount => disposedCount;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (trashes != null)
        {
            foreach (SpaceTrash trash in trashes)
            {
                if (trash != null)
                    trash.SetCamera(targetCamera);
            }
        }

        // 성공 연출이 목표로 삼을 원래 자세를 기억해 둔다.
        if (clearPopup != null)
            popupRestScale = clearPopup.transform.localScale;

        CacheThumbPoses(riseThumbs, out thumbRestPositions, out thumbRestScales);
        CacheThumbPoses(failThumbs, out failThumbRestPositions, out failThumbRestScales);
    }

    /// <summary>썸네일들이 배치된 자리·크기를 기억해 둔다. 연출의 목표이자 리셋 복원값이 된다.</summary>
    private static void CacheThumbPoses(SpriteRenderer[] thumbs, out Vector3[] positions, out Vector3[] scales)
    {
        if (thumbs == null)
        {
            positions = null;
            scales = null;
            return;
        }

        positions = new Vector3[thumbs.Length];
        scales = new Vector3[thumbs.Length];

        for (int i = 0; i < thumbs.Length; i++)
        {
            if (thumbs[i] == null)
                continue;

            positions[i] = thumbs[i].transform.localPosition;
            scales[i] = thumbs[i].transform.localScale;
        }
    }

    protected override void OnTimedPlay()
    {
        ResetInternal();
        state = State.Playing;
    }

    protected override void OnTimedStopAndReset()
    {
        ResetInternal();
        state = State.Idle;
    }

    /// <summary>쓰레기들을 처음 자리로 되돌리고 카운트를 0으로 만든다. 멱등.</summary>
    private void ResetInternal()
    {
        if (clearRoutine != null)
        {
            StopCoroutine(clearRoutine);
            clearRoutine = null;
        }

        if (failRoutine != null)
        {
            StopCoroutine(failRoutine);
            failRoutine = null;
        }

        held = null;
        disposedCount = 0;

        if (trashes != null)
        {
            foreach (SpaceTrash trash in trashes)
            {
                if (trash != null)
                    trash.ResetState();
            }
        }

        HideClearVisuals();
        onDisposed.Invoke(0);
    }

    /// <summary>성공·실패 연출을 모두 숨기고 원래 자세로 되돌린다. 멱등.</summary>
    private void HideClearVisuals()
    {
        if (clearPopup != null)
        {
            clearPopup.transform.localScale = popupRestScale;
            clearPopup.gameObject.SetActive(false);
        }

        HideThumbs(riseThumbs, thumbRestPositions, thumbRestScales);
        HideThumbs(failThumbs, failThumbRestPositions, failThumbRestScales);
    }

    /// <summary>썸네일들을 숨기고 배치된 자리·크기·자세로 되돌린다.</summary>
    private static void HideThumbs(SpriteRenderer[] thumbs, Vector3[] restPositions, Vector3[] restScales)
    {
        if (thumbs == null)
            return;

        for (int i = 0; i < thumbs.Length; i++)
        {
            if (thumbs[i] == null)
                continue;

            Transform t = thumbs[i].transform;

            if (restPositions != null && i < restPositions.Length)
                t.localPosition = restPositions[i];

            // 빨려 들어가며 작아지고 돌았을 수 있으니 크기·회전도 되돌린다.
            if (restScales != null && i < restScales.Length)
                t.localScale = restScales[i];

            t.localRotation = Quaternion.identity;
            thumbs[i].gameObject.SetActive(false);
        }
    }

    protected override void OnTimedUpdate()
    {
        if (state != State.Playing)
            return;

        DriftAll();
        HandlePointer();
    }

    /// <summary>잡히지 않은 쓰레기들을 떠다니게 한다.</summary>
    private void DriftAll()
    {
        if (trashes == null)
            return;

        float dt = Time.deltaTime;
        foreach (SpaceTrash trash in trashes)
        {
            if (trash != null)
                trash.Drift(dt);
        }
    }

    /// <summary>누르면 잡고, 끌면 따라오고, 놓으면 버리거나 다시 떠다니게 한다.</summary>
    private void HandlePointer()
    {
        if (!TryGetPointer(out Vector2 screenPos, out bool pressed, out bool released, out bool isPressed))
            return;

        Vector2 world = ScreenToWorld(screenPos);

        if (held == null)
        {
            if (pressed)
                TryGrab(world);

            return;
        }

        // 잡고 있는 동안은 포인터를 그대로 따라온다(지연 없이 즉시 추종).
        if (isPressed && !released)
        {
            Vector3 position = held.transform.position;
            Vector2 next = world - grabOffset;
            held.transform.position = new Vector3(next.x, next.y, position.z);
            return;
        }

        ReleaseHeld();
    }

    /// <summary>포인터 아래의 쓰레기를 잡는다. 겹쳐 있으면 위에 그려진 것부터 잡는다.</summary>
    private void TryGrab(Vector2 world)
    {
        if (trashes == null)
            return;

        SpaceTrash best = null;
        foreach (SpaceTrash trash in trashes)
        {
            if (trash == null || trash.IsDisposed || !trash.ContainsPoint(world))
                continue;

            if (best == null || trash.SortingOrder >= best.SortingOrder)
                best = trash;
        }

        if (best == null)
            return;

        held = best;
        held.Grab();
        grabOffset = world - (Vector2)held.transform.position;

        PlaySfx(pickUpClip, pickUpVolume);
    }

    /// <summary>효과음을 한 번 재생한다. 매번 음높이를 살짝 달리해 반복돼도 지루하지 않게 한다.</summary>
    private void PlaySfx(AudioClip clip, float volume)
    {
        if (sfxSource == null || clip == null)
            return;

        sfxSource.pitch = UnityEngine.Random.Range(pitchRange.x, pitchRange.y);
        sfxSource.PlayOneShot(clip, volume);
    }

    /// <summary>결과음처럼 한 판에 한 번만 나는 소리를 원음 그대로 재생한다(음높이 변형 없음).</summary>
    private void PlayResultSfx(AudioClip clip, float volume)
    {
        if (sfxSource == null || clip == null)
            return;

        sfxSource.pitch = 1f;
        sfxSource.PlayOneShot(clip, volume);
    }

    /// <summary>잡고 있던 쓰레기를 놓는다. Point 안이면 버리고, 아니면 그 자리에서 다시 떠다닌다.</summary>
    private void ReleaseHeld()
    {
        SpaceTrash trash = held;
        held = null;

        if (trash == null)
            return;

        if (IsOverPoint(trash))
        {
            trash.Dispose(pointArea != null ? pointArea.bounds.center : trash.transform.position);
            PlaySfx(suctionClip, suctionVolume);

            disposedCount++;
            onDisposed.Invoke(disposedCount);

            if (disposedCount >= CountTrashes())
            {
                state = State.Cleared;
                onClear.Invoke();
                clearRoutine = StartCoroutine(ClearRoutine());
                SucceedWhenTimeUp();
            }

            return;
        }

        trash.Release();
    }

    /// <summary>쓰레기가 Point 영역과 겹치는지. 정중앙까지 넣을 필요 없이 걸치기만 해도 인정한다.</summary>
    private bool IsOverPoint(SpaceTrash trash)
    {
        if (pointArea == null)
            return false;

        Bounds area = pointArea.bounds;
        area.Expand(new Vector3(pointPadding * 2f, pointPadding * 2f, 0f));

        if (!trash.TryGetBounds(out Bounds trashBounds))
            return area.Contains(trash.transform.position);

        // 2D 로만 판정한다. z 는 서로 다를 수 있어 비교에서 제외한다.
        bool overlapX = trashBounds.min.x <= area.max.x && trashBounds.max.x >= area.min.x;
        bool overlapY = trashBounds.min.y <= area.max.y && trashBounds.max.y >= area.min.y;

        return overlapX && overlapY;
    }

    /// <summary>등록된 쓰레기 개수(비어 있는 칸은 세지 않는다).</summary>
    private int CountTrashes()
    {
        if (trashes == null)
            return 0;

        int count = 0;
        foreach (SpaceTrash trash in trashes)
        {
            if (trash != null)
                count++;
        }

        return count;
    }

    /// <summary>성공 연출: Clear! 가 팝업으로 튀어나오고, 썸네일들이 밑에서 차례로 올라온다.</summary>
    private IEnumerator ClearRoutine()
    {
        PlayResultSfx(clearClip, clearVolume);

        // 썸네일들은 화면 아래에서 시작해 제자리로 올라온다.
        StartCoroutine(RiseThumbsRoutine(riseThumbs, thumbRestPositions));

        yield return PopupClearRoutine();

        clearRoutine = null;
    }

    /// <summary>
    /// 실패 연출: 실패 썸네일들이 밑에서 올라온 다음,
    /// 우주 쓰레기처럼 Point 로 빙빙 돌며 빨려 들어간다.
    /// </summary>
    private IEnumerator FailRoutine()
    {
        held = null;

        // 야유는 썸네일이 올라오는 동안 깔린다.
        PlayResultSfx(failClip, failVolume);

        // 실패한 순간 남은 쓰레기는 왼쪽으로 슉 날아가 버린다.
        FlyAwayRemainingTrash();

        // 1단계: 썸네일들이 차례로 올라온다. 마지막 것이 다 올라올 때까지 기다린다.
        yield return RiseThumbsRoutine(failThumbs, failThumbRestPositions);
        yield return new WaitForSeconds(riseDuration);

        // 2단계: 잠깐 보여준 뒤
        if (failThumbHold > 0f)
            yield return new WaitForSeconds(failThumbHold);

        // 3단계: 우주 쓰레기처럼 Point 로 빨려 들어간다.
        yield return SuckThumbsRoutine();

        if (failHoldDuration > 0f)
            yield return new WaitForSeconds(failHoldDuration);

        failRoutine = null;

        // 시간 초과 경로에서는 이미 결과가 잠겨 FailImmediately 가 무시되므로 직접 통지한다.
        ReportFinished(false);
    }

    /// <summary>아직 안 버린 쓰레기들을 화면 밖(왼쪽)으로 날려 보낸다.</summary>
    private void FlyAwayRemainingTrash()
    {
        if (trashes == null)
            return;

        // 썸네일 연출이 끝날 때까지는 확실히 날아가도록 넉넉히 잡는다.
        float duration = riseDuration + failThumbHold + failSuckDuration + failHoldDuration;

        foreach (SpaceTrash trash in trashes)
        {
            if (trash == null || trash.IsDisposed)
                continue;

            trash.FlyAway(trashFlyDirection, trashFlySpeed, duration);
        }
    }

    /// <summary>실패 썸네일들을 Point 로 빨아들인다(빙빙 돌며 작아져 사라짐).</summary>
    private IEnumerator SuckThumbsRoutine()
    {
        if (failThumbs == null)
            yield break;

        Vector3 target = pointArea != null ? pointArea.bounds.center : transform.position;

        // 쓰레기를 버릴 때와 같은 연출이므로 같은 소리를 쓴다.
        PlaySfx(suctionClip, suctionVolume);

        for (int i = 0; i < failThumbs.Length; i++)
        {
            if (failThumbs[i] != null)
                StartCoroutine(SuckOneThumbRoutine(failThumbs[i], target));
        }

        if (failSuckDuration > 0f)
            yield return new WaitForSeconds(failSuckDuration);
    }

    /// <summary>썸네일 하나를 목표 지점으로 빨아들인다. 우주 쓰레기 버릴 때와 같은 연출.</summary>
    private IEnumerator SuckOneThumbRoutine(SpriteRenderer thumb, Vector3 target)
    {
        Transform t = thumb.transform;
        Vector3 from = t.position;
        Vector3 fromScale = t.localScale;
        target.z = from.z;

        float time = 0f;
        while (time < failSuckDuration)
        {
            time += Time.deltaTime;
            float k = failSuckDuration > 0f ? Mathf.Clamp01(time / failSuckDuration) : 1f;

            // 처음엔 천천히, 갈수록 빨려들듯 가속하며 돌고 작아진다.
            t.position = Vector3.Lerp(from, target, k * k);
            t.localScale = Vector3.Lerp(fromScale, Vector3.zero, k);
            t.Rotate(0f, 0f, failSuckSpinSpeed * Time.deltaTime);

            yield return null;
        }

        t.localScale = Vector3.zero;
        thumb.gameObject.SetActive(false);
    }

    /// <summary>Clear! 이미지를 작게 띄워 제 크기까지 통통 튀며 커지게 한다.</summary>
    private IEnumerator PopupClearRoutine()
    {
        if (clearPopup == null)
            yield break;

        Transform t = clearPopup.transform;
        t.localScale = popupRestScale * popupStartScale;
        clearPopup.gameObject.SetActive(true);

        float time = 0f;
        while (time < popupDuration)
        {
            time += Time.deltaTime;
            float k = popupDuration > 0f ? Mathf.Clamp01(time / popupDuration) : 1f;
            t.localScale = Vector3.LerpUnclamped(popupRestScale * popupStartScale, popupRestScale, EaseOutBack(k));

            yield return null;
        }

        t.localScale = popupRestScale;
    }

    /// <summary>썸네일들을 화면 아래에서 제자리로 차례차례 올린다.</summary>
    private IEnumerator RiseThumbsRoutine(SpriteRenderer[] thumbs, Vector3[] restPositions)
    {
        if (thumbs == null)
            yield break;

        for (int i = 0; i < thumbs.Length; i++)
        {
            if (thumbs[i] == null)
                continue;

            Vector3 restPos = restPositions != null && i < restPositions.Length
                ? restPositions[i]
                : thumbs[i].transform.localPosition;

            StartCoroutine(RiseOneThumbRoutine(thumbs[i], restPos));

            if (riseStagger > 0f && i < thumbs.Length - 1)
                yield return new WaitForSeconds(riseStagger);
        }
    }

    /// <summary>썸네일 하나를 아래에서 제자리로 올린다.</summary>
    private IEnumerator RiseOneThumbRoutine(SpriteRenderer thumb, Vector3 restPos)
    {
        Transform t = thumb.transform;
        Vector3 fromPos = restPos + Vector3.down * riseDistance;

        t.localPosition = fromPos;
        thumb.gameObject.SetActive(true);

        float time = 0f;
        while (time < riseDuration)
        {
            time += Time.deltaTime;
            float k = riseDuration > 0f ? Mathf.Clamp01(time / riseDuration) : 1f;

            // 끝에서 살짝 넘쳤다 자리잡아 쑥 올라오는 느낌을 준다.
            t.localPosition = Vector3.LerpUnclamped(fromPos, restPos, EaseOutBack(k));

            yield return null;
        }

        t.localPosition = restPos;
    }

    /// <summary>끝에서 살짝 넘쳤다가 제자리로 돌아오는 이징(통통 튀는 느낌).</summary>
    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    /// <summary>제한 시간을 다 써서 실패로 확정될 때 호출된다.</summary>
    protected override void OnTimeUp()
    {
        if (state == State.Cleared)
            return;

        state = State.Failed;
        onFail.Invoke();

        // 기본 OnTimeUp(즉시 실패 통지) 대신 실패 연출을 보여준 뒤 통지한다.
        failRoutine = StartCoroutine(FailRoutine());
    }

    private Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
            return screenPosition;

        return cam.ScreenToWorldPoint(screenPosition);
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
