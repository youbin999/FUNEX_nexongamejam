using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 삽으로 흙을 파는 미니게임.
/// S 를 눌러 삽을 흙 밑으로 밀어 넣고, 충분히 깊게 박힌 뒤(<see cref="requiredHold"/> 이상 누른 뒤)
/// W 를 눌러 퍼올려야 제대로 파진다. 덜 박힌 상태에서 퍼올리면 흙만 긁고 헛수고가 된다.
/// 정해진 횟수를 파내면 클리어.
///
/// 프리팹으로 만들어 <see cref="MiniGamePlayer"/> 가 재생한다.
/// </summary>
public class DigMiniGame : MiniGame
{
    // 인스펙터에 확실히 노출되도록 제네릭 UnityEvent 는 구체 타입으로 선언한다.
    [Serializable] public class DigEvent : UnityEvent<int> { }
    [Serializable] public class HoldEvent : UnityEvent<float> { }
    [Serializable] public class TimerEvent : UnityEvent<float> { }

    /// <summary>성공 횟수에 따라 갈아 끼울 흙 그림 한 단계.</summary>
    [Serializable]
    public class GroundStage
    {
        [Tooltip("이 횟수만큼 팠을 때부터 아래 그림으로 바뀐다")]
        public int digCount;

        [Tooltip("이 단계에서 보여줄 흙 그림")]
        public Sprite sprite;
    }

    private enum State
    {
        Idle,
        Playing,
        Cleared,
        Failed,
    }

    /// <summary>삽이 지금 어디에 있는지.</summary>
    private enum ShovelState
    {
        Up,       // 흙 위. S 를 기다리는 상태
        Inserted, // 흙 속에 박혀 있음. W 를 기다리는 상태
        Lifted,   // 퍼올린 직후. 잠깐 뒤 Up 으로 돌아온다
    }

    [Header("삽")]
    [SerializeField] private ShovelMotion shovel;

    [Header("흙")]
    [Tooltip("파인 정도에 따라 그림이 바뀔 흙 오브젝트")]
    [SerializeField] private SpriteRenderer groundRenderer;

    [Tooltip("성공 횟수별 흙 그림. 예: 0회 → ground_edit, 2회 → ground_edit2")]
    [SerializeField] private List<GroundStage> groundStages = new List<GroundStage>();

    [Header("규칙")]
    [Tooltip("클리어에 필요한 성공 횟수")]
    [SerializeField] private int clearCount = 3;

    [Tooltip("이만큼 S 를 누르고 있어야 삽이 제대로 박힌다(초)")]
    [SerializeField] private float requiredHold = 0.3f;

    [Tooltip("S 에서 손을 떼고도 이 시간 안에 W 를 누르면 인정한다(초)")]
    [SerializeField] private float releaseGrace = 0.2f;

    [Tooltip("퍼올린 자세를 유지하는 시간(초). 지나면 삽이 기본 자세로 돌아온다")]
    [SerializeField] private float liftHoldTime = 0.3f;

    [Header("제한 시간")]
    [Tooltip("끄면 시간 제한 없이 성공할 때까지 계속 할 수 있다")]
    [SerializeField] private bool useTimeLimit = true;

    [Tooltip("제한 시간(초). 게임이 시작되면 조작과 상관없이 흐른다")]
    [SerializeField] private float timeLimit = 8f;

    [Header("입력")]
    [Tooltip("켜면 방향키 아래/위도 S/W 와 똑같이 동작한다")]
    [SerializeField] private bool allowArrowKeys = true;

    [Header("이벤트")]
    [Tooltip("제대로 팠을 때 지금까지의 성공 횟수를 넘긴다")]
    public DigEvent onDig;

    [Tooltip("덜 박힌 상태로 퍼올렸을 때. 실패 연출에 쓴다")]
    public UnityEvent onDigFailed;

    [Tooltip("삽이 박히는 정도. 0에서 1로 차오르고 1이 되면 퍼올릴 수 있다")]
    public HoldEvent onHoldChanged;

    [Tooltip("0에서 1로 차오르는 제한 시간 게이지. Image.fillAmount 에 그대로 연결하면 된다")]
    public TimerEvent onTimerChanged;

    public UnityEvent onClear;
    public UnityEvent onFail;

    private State state = State.Idle;
    private ShovelState shovelState = ShovelState.Up;

    private int digCount;
    private float holdTime;
    private float graceLeft;
    private float liftLeft;
    private float elapsed;

    /// <summary>지금까지 제대로 파낸 횟수.</summary>
    public int DigCount => digCount;

    /// <summary>삽이 박힌 정도. 1이면 퍼올릴 준비가 됐다는 뜻.</summary>
    public float HoldRatio => requiredHold > 0f ? Mathf.Clamp01(holdTime / requiredHold) : 1f;

    /// <summary>제한 시간을 쓰는 판에서만 씬 공용 타이머 UI에 표시한다.</summary>
    public override bool HasTimer => useTimeLimit && timeLimit > 0f;

    /// <summary>0에서 1로 차오르는 제한 시간 값.</summary>
    public override float TimerRatio => useTimeLimit && timeLimit > 0f ? Mathf.Clamp01(elapsed / timeLimit) : 0f;

    /// <summary>
    /// 실패 패널티가 강한 일렁임과 셰이크를 직접 재생하므로 공통 실패 셰이크는 생략한다.
    /// </summary>
    protected override bool HasOwnFailureCameraImpact => true;

    private void Start()
    {
        // 게임 진행이 아니라 게이지 초기 표시만 한다.
        onHoldChanged.Invoke(0f);
        onTimerChanged.Invoke(0f);
    }

    private void Update()
    {
        if (state != State.Playing)
            return;

        float dt = Time.deltaTime;

        if (useTimeLimit)
        {
            TickTimer(dt);

            // 이번 프레임에 시간이 다 됐으면 입력은 안 받는다.
            if (state != State.Playing)
                return;
        }

        TickShovel(dt);
        ReadInput(dt);
    }

    /// <summary>게임을 시작한다. 상태를 초기화하고 Playing 으로 전환한다.</summary>
    protected override void OnPlay()
    {
        ResetInternal();
        state = State.Playing;
        LogVisibilityDiagnostics();
    }

    /// <summary>
    /// [버그 추적용 임시 로깅] "삽 스프라이트가 때때로 표출되지 않는" 문제 진단.
    /// 의심 1: 삽(order 0)과 배경(order 0)이 같은 정렬 레이어·순서라 그리기 순서가 미정의 —
    ///         활성화 순서/z 값에 따라 삽이 배경 뒤로 그려질 수 있다.
    /// 의심 2: 다른 미니게임의 카메라 셰이크가 복원되지 않아 카메라가 어긋난 채 시작.
    /// </summary>
    private void LogVisibilityDiagnostics()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            Debug.Log($"[DigMiniGame][진단] 시작 시 카메라 localPos={cam.transform.localPosition}, " +
                $"orthoSize={cam.orthographicSize}", this);
        }

        if (shovel != null)
        {
            foreach (SpriteRenderer r in shovel.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Debug.Log($"[DigMiniGame][진단] 삽 렌더러 '{r.name}' activeInHierarchy={r.gameObject.activeInHierarchy}, " +
                    $"enabled={r.enabled}, sprite={(r.sprite != null ? r.sprite.name : "null")}, " +
                    $"sortingLayer={r.sortingLayerName}, order={r.sortingOrder}, pos={r.transform.position}", r);
            }
        }
        else
        {
            Debug.LogWarning("[DigMiniGame][진단] shovel 참조가 비어 있다!", this);
        }

        if (groundRenderer != null)
        {
            Debug.Log($"[DigMiniGame][진단] 흙 렌더러 sortingLayer={groundRenderer.sortingLayerName}, " +
                $"order={groundRenderer.sortingOrder}, pos={groundRenderer.transform.position}", groundRenderer);
        }

        // 삽·배경이 같은 레이어·같은 order 면 그리기 순서가 미정의 — 이번 판에서 재현될 수 있는 조건.
        foreach (SpriteRenderer r in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (shovel != null && r.transform.IsChildOf(shovel.transform))
                continue;

            foreach (SpriteRenderer s in shovel != null
                ? shovel.GetComponentsInChildren<SpriteRenderer>(true)
                : Array.Empty<SpriteRenderer>())
            {
                if (r.sortingLayerID == s.sortingLayerID && r.sortingOrder == s.sortingOrder)
                {
                    Debug.LogWarning($"[DigMiniGame][진단] 정렬 순서 동률 발견! '{r.name}'(order {r.sortingOrder}, z {r.transform.position.z:F3}) " +
                        $"vs 삽 '{s.name}'(order {s.sortingOrder}, z {s.transform.position.z:F3}) — " +
                        "동률이면 그리기 순서가 미정의라 삽이 뒤로 그려질 수 있다.", r);
                }
            }
        }
    }

    /// <summary>게임을 강제 중단하고 초기 상태(Idle)로 되돌린다.</summary>
    protected override void OnStopAndReset()
    {
        ResetInternal();
        state = State.Idle;
    }

    /// <summary>횟수, 타이머, 삽 자세를 처음으로 되돌린다.</summary>
    private void ResetInternal()
    {
        digCount = 0;
        holdTime = 0f;
        graceLeft = 0f;
        liftLeft = 0f;
        elapsed = 0f;
        shovelState = ShovelState.Up;

        onHoldChanged.Invoke(0f);
        onTimerChanged.Invoke(0f);
        ApplyGroundStage();

        if (shovel != null)
            shovel.ResetPose();
    }

    /// <summary>지금 성공 횟수에 맞는 흙 그림으로 갈아 끼운다.</summary>
    private void ApplyGroundStage()
    {
        if (groundRenderer == null || groundStages.Count == 0)
            return;

        // 지금 횟수를 넘지 않는 단계 중 가장 높은 것을 고른다. 목록 순서는 상관없다.
        GroundStage picked = null;
        foreach (GroundStage stage in groundStages)
        {
            if (stage == null || stage.sprite == null || stage.digCount > digCount)
                continue;

            if (picked == null || stage.digCount > picked.digCount)
                picked = stage;
        }

        if (picked != null)
            groundRenderer.sprite = picked.sprite;
    }

    private void TickTimer(float dt)
    {
        elapsed += dt;
        onTimerChanged.Invoke(TimerRatio);

        if (elapsed < timeLimit)
            return;

        elapsed = timeLimit;
        state = State.Failed;
        onFail.Invoke();
        ReportFinished(false);
    }

    /// <summary>퍼올린 자세를 잠깐 보여준 뒤 기본 자세로 되돌린다.</summary>
    private void TickShovel(float dt)
    {
        if (shovelState != ShovelState.Lifted)
            return;

        liftLeft -= dt;
        if (liftLeft > 0f)
            return;

        shovelState = ShovelState.Up;

        if (shovel != null)
            shovel.MoveToIdle();
    }

    private void ReadInput(float dt)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        bool insertPressed = keyboard.sKey.wasPressedThisFrame
            || (allowArrowKeys && keyboard.downArrowKey.wasPressedThisFrame);

        bool insertHeld = keyboard.sKey.isPressed
            || (allowArrowKeys && keyboard.downArrowKey.isPressed);

        bool liftPressed = keyboard.wKey.wasPressedThisFrame
            || (allowArrowKeys && keyboard.upArrowKey.wasPressedThisFrame);

        if (insertPressed && shovelState == ShovelState.Up)
            Insert();

        if (shovelState == ShovelState.Inserted)
            TickHold(insertHeld, dt);

        if (liftPressed)
            Lift();
    }

    /// <summary>삽을 흙 밑으로 밀어 넣는다.</summary>
    private void Insert()
    {
        shovelState = ShovelState.Inserted;
        holdTime = 0f;
        graceLeft = releaseGrace;

        onHoldChanged.Invoke(0f);

        if (shovel != null)
            shovel.MoveToInserted();
    }

    /// <summary>S 를 누르고 있는 동안 박힌 정도를 채운다. 손을 떼고 유예가 끝나면 삽이 빠진다.</summary>
    private void TickHold(bool insertHeld, float dt)
    {
        if (insertHeld)
        {
            holdTime += dt;
            graceLeft = releaseGrace;
            onHoldChanged.Invoke(HoldRatio);
            return;
        }

        graceLeft -= dt;
        if (graceLeft > 0f)
            return;

        // 누르다 말고 손을 놔버리면 삽이 도로 빠진다.
        PullOut();
    }

    /// <summary>삽을 퍼올린다. 충분히 박혔으면 성공, 아니면 헛수고.</summary>
    private void Lift()
    {
        // 흙에 박지도 않고 W 만 누르면 아무 일도 없다.
        if (shovelState != ShovelState.Inserted)
            return;

        if (holdTime < requiredHold)
        {
            FailDig();
            return;
        }

        SucceedDig();
    }

    private void SucceedDig()
    {
        digCount++;
        holdTime = 0f;
        graceLeft = 0f;
        liftLeft = liftHoldTime;
        shovelState = ShovelState.Lifted;

        onHoldChanged.Invoke(0f);
        onDig.Invoke(digCount);
        ApplyGroundStage();

        if (shovel != null)
            shovel.MoveToLifted();

        if (digCount < clearCount)
            return;

        state = State.Cleared;
        onClear.Invoke();
        ReportFinished(true);
    }

    /// <summary>덜 박힌 상태로 퍼올렸다. 횟수는 오르지 않고 삽만 도로 빠진다.</summary>
    private void FailDig()
    {
        PullOut();
        onDigFailed.Invoke();
    }

    private void PullOut()
    {
        shovelState = ShovelState.Up;
        holdTime = 0f;
        graceLeft = 0f;

        onHoldChanged.Invoke(0f);

        if (shovel != null)
            shovel.MoveToIdle();
    }
}
