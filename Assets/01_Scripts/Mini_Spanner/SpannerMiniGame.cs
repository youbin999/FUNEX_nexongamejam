using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 나사 조이기 미니게임.
/// 마우스로 스패너를 잡고 축 둘레로 원을 그리면 그 각도만큼 나사가 돌아간다.
/// 제한 시간 안에 정해진 바퀴 수를 채우면 클리어, 못 채우면 실패.
/// 프리팹으로 만들어 <see cref="MiniGamePlayer"/> 가 재생한다.
/// </summary>
public class SpannerMiniGame : MiniGame
{
    // 인스펙터에 확실히 노출되도록 제네릭 UnityEvent 는 구체 타입으로 선언한다.
    [Serializable] public class TurnEvent : UnityEvent<float> { }
    [Serializable] public class RatioEvent : UnityEvent<float> { }

    private enum State
    {
        Idle,
        Playing,
        Cleared,
        Failed,
    }

    /// <summary>진행도로 인정할 회전 방향.</summary>
    public enum TurnDirection
    {
        /// <summary>시계 방향으로 돌려야 조인 것으로 본다.</summary>
        Clockwise,

        /// <summary>반시계 방향으로 돌려야 조인 것으로 본다.</summary>
        CounterClockwise,

        /// <summary>먼저 돌린 쪽을 그 판의 정방향으로 삼는다. 어느 쪽으로 돌려도 된다.</summary>
        Any,
    }

    [Header("축")]
    [Tooltip("나사가 박힌 축. 이 점을 기준으로 마우스 각도를 잰다. 볼트 한가운데에 맞춰 둔다")]
    [SerializeField] private Transform pivot;

    [Header("규칙")]
    [Tooltip("클리어에 필요한 회전 바퀴 수")]
    [SerializeField] private float clearTurns = 2f;

    [Tooltip("제한 시간(초). 게임이 시작되면 조작과 상관없이 흐른다")]
    [SerializeField] private float timeLimit = 5f;

    [Tooltip("진행도로 인정할 방향. Any 면 플레이어가 먼저 돌린 쪽이 그 판의 정방향이 된다")]
    [SerializeField] private TurnDirection turnDirection = TurnDirection.Any;

    [Tooltip("켜면 반대로 돌려도 진행도가 깎이지 않는다. 좌우로 흔들기만 해도 차오르므로 보통은 꺼 둔다")]
    [SerializeField] private bool ratchet = false;

    [Header("잡기 판정")]
    [Tooltip("비워두면 Camera.main 을 쓴다")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("켜면 아래 레이어의 콜라이더를 눌렀을 때만 돌릴 수 있다. 끄면 화면 아무 데나 눌러도 잡힌다")]
    [SerializeField] private bool requireGrab = false;

    [Tooltip("스패너를 잡을 수 있는 콜라이더가 있는 레이어")]
    [SerializeField] private LayerMask grabbableLayers = ~0;

    [Header("헛도는 것 방지")]
    [Tooltip("축에서 이 거리 안쪽에서는 회전을 먹지 않는다. 축 근처에서는 조금만 움직여도 각도가 확 튀기 때문이다")]
    [SerializeField] private float deadRadius = 1.5f;

    [Tooltip("이 속도(도/초)보다 빠르게 각도가 변하면 축을 가로지른 것으로 보고 버린다. 좌우로 흔들어서 한 바퀴를 공짜로 먹는 것을 막는다")]
    [SerializeField] private float maxTurnSpeed = 1080f;

    [Header("이벤트")]
    [Tooltip("지금까지 돌린 바퀴 수를 넘긴다. 0.5 면 반 바퀴")]
    public TurnEvent onTurn;

    [Tooltip("돌아간 각도(도)를 그대로 넘긴다. ScrewRotator.SetAngle 에 연결한다")]
    public TurnEvent onAngleChanged;

    [Tooltip("0에서 1로 차오르는 진행도. Image.fillAmount 에 연결한다")]
    public RatioEvent onProgressChanged;

    [Tooltip("0에서 1로 차오르는 타이머 게이지. Image.fillAmount 에 연결한다")]
    public RatioEvent onTimerChanged;

    public UnityEvent onClear;
    public UnityEvent onFail;

    private readonly List<Collider2D> hitBuffer = new List<Collider2D>();
    private ContactFilter2D filter;

    // 조인 정도(도). 진행도 판정에 쓴다.
    private float turnedDegrees;

    // 화면에 보여줄 누적 각도(도). 되돌리면 같이 되돌아간다.
    private float visualAngle;

    private bool dragging;
    private float lastPointerAngle;

    // TurnDirection.Any 일 때 이번 판의 정방향. 0이면 아직 정해지지 않았다.
    private int lockedDirection;

    private float elapsed;
    private State state = State.Idle;

    /// <summary>클리어에 필요한 총 각도(도).</summary>
    public float ClearDegrees => clearTurns * 360f;

    /// <summary>지금까지 조인 바퀴 수.</summary>
    public float Turns => turnedDegrees / 360f;

    /// <summary>0에서 1로 차오르는 진행도.</summary>
    public float Progress => ClearDegrees > 0f ? Mathf.Clamp01(turnedDegrees / ClearDegrees) : 0f;

    /// <summary>제한 시간이 잡혀 있으면 씬 공용 타이머 UI에 표시한다.</summary>
    public override bool HasTimer => timeLimit > 0f;

    /// <summary>0에서 1로 차오르는 타이머 값.</summary>
    public override float TimerRatio => timeLimit > 0f ? Mathf.Clamp01(elapsed / timeLimit) : 0f;

    /// <summary>카메라와 드래그 판정 필터를 준비하고 게이지를 0으로 비운다.</summary>
    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        filter = new ContactFilter2D
        {
            useTriggers = true,
        };
        filter.SetLayerMask(grabbableLayers);

        onProgressChanged.Invoke(0f);
        onTimerChanged.Invoke(0f);
    }

    /// <summary>제한 시간을 흘린 뒤 드래그를 처리한다. 시간이 다 되면 조작을 받지 않는다.</summary>
    private void Update()
    {
        if (state != State.Playing)
            return;

        TickTimer();

        // 이번 프레임에 시간이 다 됐으면 조작은 안 받는다.
        if (state != State.Playing)
            return;

        HandleDrag();
    }

    /// <summary>게임을 시작한다. 상태를 초기화하고 Playing 으로 전환한다.</summary>
    protected override void OnPlay()
    {
        ResetInternal();

        // 게이지는 재생을 시작하는 이 시점에만 0으로 되돌린다.
        // 끝난 뒤에도 마지막 상태가 화면에 남아야 결과가 보인다.
        onAngleChanged.Invoke(0f);
        onProgressChanged.Invoke(0f);
        onTimerChanged.Invoke(0f);

        state = State.Playing;
    }

    /// <summary>게임을 강제 중단하고 초기 상태(Idle)로 되돌린다.</summary>
    protected override void OnStopAndReset()
    {
        ResetInternal();
        state = State.Idle;
    }

    /// <summary>
    /// 각도와 타이머를 처음으로 되돌린다.
    /// 게이지 이벤트는 여기서 쏘지 않는다. 종료 직후에도 결과가 화면에 남아야 하기 때문이고,
    /// 다음 판을 위한 초기화는 <see cref="OnPlay"/> 가 책임진다.
    /// </summary>
    private void ResetInternal()
    {
        turnedDegrees = 0f;
        visualAngle = 0f;
        elapsed = 0f;
        dragging = false;
        lockedDirection = 0;
    }

    /// <summary>제한 시간을 흘리고 게이지를 갱신한다. 다 쓰면 실패로 끝낸다.</summary>
    private void TickTimer()
    {
        elapsed += Time.deltaTime;
        onTimerChanged.Invoke(TimerRatio);

        if (elapsed < timeLimit)
            return;

        elapsed = timeLimit;
        state = State.Failed;
        onFail.Invoke();
        ReportFinished(false);
    }

    /// <summary>잡은 지점을 기준으로 돌린 각도를 계산한다. 손을 떼면 다음 접점을 새 기준으로 삼는다.</summary>
    private void HandleDrag()
    {
        if (targetCamera == null)
            return;

        if (!TryGetPointer(out Vector2 screenPosition, out bool held) || !held)
        {
            // 손을 떼면 다음에 다시 잡을 때 그 지점을 새 기준으로 삼는다.
            dragging = false;
            return;
        }

        Vector2 worldPosition = targetCamera.ScreenToWorldPoint(screenPosition);

        if (!dragging)
        {
            if (requireGrab && !IsOnGrabbable(worldPosition))
                return;

            // 잡은 첫 프레임은 기준 각도만 잡는다. 여기서 각도를 더하면 스패너가 순간이동한다.
            dragging = true;
            lastPointerAngle = AngleFromPivot(worldPosition, lastPointerAngle);
            return;
        }

        float angle = AngleFromPivot(worldPosition, lastPointerAngle);
        float delta = Mathf.DeltaAngle(lastPointerAngle, angle);

        // 기준 각도는 버리는 경우에도 항상 갱신한다.
        // 그래야 죽은 구역을 빠져나올 때 그동안 벌어진 각도가 한꺼번에 밀려들지 않는다.
        lastPointerAngle = angle;

        // 축 바로 옆에서는 손을 조금만 떨어도 각도가 수십 도씩 튄다. 돌린 것으로 치지 않는다.
        Vector2 fromPivot = worldPosition - PivotPosition;
        if (fromPivot.sqrMagnitude < deadRadius * deadRadius)
            return;

        // 축을 가로지르면 각도가 한 프레임에 180도 가까이 뒤집힌다. 실제로 돌린 게 아니므로 버린다.
        if (Mathf.Abs(delta) > maxTurnSpeed * Time.deltaTime)
            return;

        AddRotation(delta);
    }

    /// <summary>돌아간 각도를 누적하고 클리어를 판정한다.</summary>
    private void AddRotation(float delta)
    {
        if (Mathf.Approximately(delta, 0f))
            return;

        visualAngle += delta;
        onAngleChanged.Invoke(visualAngle);

        // Atan2 는 반시계가 +라서, 시계 방향을 +로 보려면 부호를 뒤집어야 한다.
        float clockwiseDelta = -delta;
        float progressDelta = SignedProgress(clockwiseDelta);

        if (ratchet && progressDelta < 0f)
            progressDelta = 0f;

        // 반대로 돌리면 깎이되 음수까지는 내려가지 않는다.
        turnedDegrees = Mathf.Max(0f, turnedDegrees + progressDelta);

        onTurn.Invoke(Turns);
        onProgressChanged.Invoke(Progress);

        if (turnedDegrees >= ClearDegrees)
        {
            state = State.Cleared;
            onClear.Invoke();
            ReportFinished(true);
        }
    }

    /// <summary>
    /// 시계 방향 기준 각도 변화량을 "조인 정도"로 환산한다.
    /// Any 일 때는 처음 돌린 쪽을 이번 판의 정방향으로 잡아 둔다.
    /// 그래야 어느 쪽으로 돌려도 되면서, 좌우로 흔들면 더한 만큼 도로 깎여서 헛돌지 않는다.
    /// </summary>
    private float SignedProgress(float clockwiseDelta)
    {
        switch (turnDirection)
        {
            case TurnDirection.Clockwise:
                return clockwiseDelta;

            case TurnDirection.CounterClockwise:
                return -clockwiseDelta;

            default:
                if (lockedDirection == 0)
                    lockedDirection = clockwiseDelta > 0f ? 1 : -1;

                return clockwiseDelta * lockedDirection;
        }
    }

    /// <summary>회전의 중심. 축을 지정하지 않았으면 자기 위치를 쓴다.</summary>
    private Vector2 PivotPosition => pivot != null ? (Vector2)pivot.position : (Vector2)transform.position;

    /// <summary>축에서 본 마우스 방향의 각도(도). 축 위에 정확히 겹치면 각도를 잴 수 없어 직전 값을 그대로 쓴다.</summary>
    private float AngleFromPivot(Vector2 worldPosition, float fallback)
    {
        Vector2 direction = worldPosition - PivotPosition;

        if (direction.sqrMagnitude < 0.000001f)
            return fallback;

        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }

    /// <summary>누르고 있는 동안 계속 true 를 돌려준다. 연타가 아니라 드래그라서 눌림 상태가 필요하다.</summary>
    private bool TryGetPointer(out Vector2 screenPosition, out bool held)
    {
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            screenPosition = mouse.position.ReadValue();
            held = mouse.leftButton.isPressed;
            return true;
        }

        Touchscreen touch = Touchscreen.current;
        if (touch != null)
        {
            screenPosition = touch.primaryTouch.position.ReadValue();
            held = touch.primaryTouch.press.isPressed;
            return true;
        }

        screenPosition = default;
        held = false;
        return false;
    }

    /// <summary>그 월드 좌표에 잡을 수 있는 콜라이더가 있는지.</summary>
    private bool IsOnGrabbable(Vector2 worldPosition)
    {
        return Physics2D.OverlapPoint(worldPosition, filter, hitBuffer) > 0;
    }
}
