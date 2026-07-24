using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 손 비비기 미니게임.
/// 콜라이더를 클릭할 때마다 양손이 서로 반대로 위/아래로 움직인다.
/// 제한 시간 안에 정해진 횟수를 채우면 클리어, 못 채우면 실패.
/// 프리팹으로 만들어 <see cref="MiniGamePlayer"/> 가 재생한다.
/// </summary>
public class RubMiniGame : TimedMiniGame
{
    // 인스펙터에 확실히 노출되도록 제네릭 UnityEvent 는 구체 타입으로 선언한다.
    [Serializable] public class RubEvent : UnityEvent<int> { }

    private enum State
    {
        Idle,
        Playing,
        Cleared,
        Failed,
    }

    [Header("손")]
    [SerializeField] private HandRub leftHand;
    [SerializeField] private HandRub rightHand;

    [Header("규칙")]
    [Tooltip("클리어에 필요한 클릭 횟수")]
    [SerializeField] private int clearCount = 10;

    [Header("클릭 판정")]
    [Tooltip("비워두면 Camera.main 을 쓴다")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("클릭을 받을 콜라이더가 있는 레이어")]
    [SerializeField] private LayerMask clickableLayers = ~0;

    [Header("이벤트")]
    [Tooltip("클릭할 때마다 현재 횟수를 넘긴다")]
    public RubEvent onRub;

    public UnityEvent onClear;
    public UnityEvent onFail;

    private readonly List<Collider2D> hitBuffer = new List<Collider2D>();
    private ContactFilter2D filter;

    private int rubCount;
    private int direction = 1;
    private State state = State.Idle;

    /// <summary>지금까지 비빈 횟수.</summary>
    public int RubCount => rubCount;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        filter = new ContactFilter2D
        {
            useTriggers = true,
        };
        filter.SetLayerMask(clickableLayers);
    }

    protected override void OnTimedUpdate()
    {
        if (!TryGetPressPosition(out Vector2 screenPosition))
            return;

        if (!IsOnClickable(screenPosition))
            return;

        Rub();
    }

    /// <summary>클릭 판정 없이 한 번 비비게 하고 싶을 때 직접 호출해도 된다.</summary>
    public void Rub()
    {
        if (state != State.Playing)
            return;

        // 양손이 서로 반대 방향으로 엇갈려야 비비는 것처럼 보인다.
        if (leftHand != null)
            leftHand.MoveTo(direction);

        if (rightHand != null)
            rightHand.MoveTo(-direction);

        direction = -direction;

        rubCount++;
        onRub.Invoke(rubCount);

        if (rubCount >= clearCount)
        {
            state = State.Cleared;
            onClear.Invoke();
            SucceedWhenTimeUp();
        }
    }

    /// <summary>게임을 시작한다. 상태를 초기화하고 Playing 으로 전환한다.</summary>
    protected override void OnTimedPlay()
    {
        ResetInternal();
        state = State.Playing;
    }

    /// <summary>게임을 강제 중단하고 초기 상태(Idle)로 되돌린다.</summary>
    protected override void OnTimedStopAndReset()
    {
        ResetInternal();
        state = State.Idle;
    }

    /// <summary>횟수, 손 자세와 방향을 처음으로 되돌린다.</summary>
    private void ResetInternal()
    {
        rubCount = 0;
        direction = 1;

        if (leftHand != null)
            leftHand.ResetPose();

        if (rightHand != null)
            rightHand.ResetPose();
    }

    /// <summary>제한 시간을 다 써서 실패로 확정될 때 호출된다.</summary>
    protected override void OnTimeUp()
    {
        state = State.Failed;
        onFail.Invoke();
        base.OnTimeUp();
    }

    private bool TryGetPressPosition(out Vector2 screenPosition)
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPosition = mouse.position.ReadValue();
            return true;
        }

        Touchscreen touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = touch.primaryTouch.position.ReadValue();
            return true;
        }

        screenPosition = default;
        return false;
    }

    private bool IsOnClickable(Vector2 screenPosition)
    {
        if (targetCamera == null)
            return false;

        Vector2 worldPoint = targetCamera.ScreenToWorldPoint(screenPosition);
        return Physics2D.OverlapPoint(worldPoint, filter, hitBuffer) > 0;
    }
}
