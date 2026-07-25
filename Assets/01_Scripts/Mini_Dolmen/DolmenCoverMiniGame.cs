using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 제한시간 동안 윗돌이 보더에 닿지 않도록 움직이는 고인돌 미니게임.
/// 윗돌은 좌우 또는 아래로만 조작할 수 있다.
/// </summary>
public sealed class DolmenCoverMiniGame : TimedMiniGame
{
    private enum State
    {
        Idle,
        Playing,
        Failed,
    }

    [Header("돌 구성")]
    [Tooltip("플레이어가 움직이는 윗돌.")]
    [SerializeField] private DolmenStone coverStone;

    [Tooltip("비어 있으면 자식의 모든 DolmenStone을 자동으로 찾아 리셋에 사용한다.")]
    [SerializeField] private DolmenStone[] allStones;

    [Header("윗돌 조작")]
    [Tooltip("A/D/S 또는 방향키를 누르는 동안 윗돌에 가하는 힘.")]
    [SerializeField] private float controlForce = 18f;

    [Tooltip("조작으로 낼 수 있는 윗돌의 최대 속도.")]
    [SerializeField] private float maxControlSpeed = 4f;

    [Header("실패 경계")]
    [Tooltip("윗돌이 이 영역의 보더에 닿으면 즉시 실패한다. 비어 있으면 카메라 화면을 사용한다.")]
    [SerializeField] private Collider2D failureBounds;

    [Tooltip("카메라 화면을 보더로 사용할 때 안쪽으로 줄일 여백(월드 단위).")]
    [SerializeField] private float screenBoundaryMargin = 0.25f;

    [Header("이벤트")]
    public UnityEvent onClear;
    public UnityEvent onFail;

    private Camera targetCamera;
    private bool failureReported;
    private State state = State.Idle;

    // Update 에서 읽어둔 조작 방향. 실제 힘은 물리 스텝(FixedUpdate)에서 가한다.
    private Vector2 controlDirection;

    private void Awake()
    {
        targetCamera = Camera.main;

        if (allStones == null || allStones.Length == 0)
            allStones = GetComponentsInChildren<DolmenStone>(true);
    }

    protected override void OnTimedPlay()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        ResetInternal(true);
        state = State.Playing;
    }

    protected override void OnTimedStopAndReset()
    {
        ResetInternal(false);
        state = State.Idle;
    }

    protected override void OnTimedUpdate()
    {
        if (state != State.Playing)
            return;

        SampleCoverControl();

        if (IsCoverTouchingBorder())
            FailForBorderContact();
    }

    /// <summary>
    /// 힘은 프레임이 아니라 물리 스텝 단위로 가한다.
    /// Update 에서 AddForce 를 호출하면 프레임률이 떨어질 때 가속도까지 같이 느려진다.
    /// </summary>
    private void FixedUpdate()
    {
        if (state != State.Playing || !IsPlaying)
            return;

        if (coverStone != null)
            coverStone.ApplyControlForce(controlDirection, controlForce, maxControlSpeed);
    }

    protected override void OnTimeUp()
    {
        onClear.Invoke();
        ReportFinished(true);
    }

    private void ResetInternal(bool enablePhysics)
    {
        failureReported = false;
        controlDirection = Vector2.zero;

        if (allStones == null)
            return;

        for (int i = 0; i < allStones.Length; i++)
        {
            DolmenStone stone = allStones[i];
            if (stone == null)
                continue;

            stone.SetGameplayPhysicsActive(enablePhysics);
            stone.ResetToStart();
        }
    }

    /// <summary>키 입력을 읽어 조작 방향만 갱신한다. 놓친 입력이 없도록 매 프레임 샘플링한다.</summary>
    private void SampleCoverControl()
    {
        controlDirection = Vector2.zero;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        Vector2 direction = Vector2.zero;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            direction.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            direction.x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            direction.y -= 1f;

        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        controlDirection = direction;
    }

    private bool IsCoverTouchingBorder()
    {
        if (coverStone == null || coverStone.HitCollider == null)
            return true;

        Bounds coverBounds = coverStone.HitCollider.bounds;
        if (failureBounds != null)
        {
            Bounds borderBounds = failureBounds.bounds;
            return coverBounds.min.x <= borderBounds.min.x
                || coverBounds.max.x >= borderBounds.max.x
                || coverBounds.min.y <= borderBounds.min.y
                || coverBounds.max.y >= borderBounds.max.y;
        }

        if (targetCamera == null || !targetCamera.orthographic)
            return true;

        Vector3 bottomLeft = targetCamera.ViewportToWorldPoint(Vector3.zero);
        Vector3 topRight = targetCamera.ViewportToWorldPoint(Vector3.one);
        float minX = bottomLeft.x + screenBoundaryMargin;
        float maxX = topRight.x - screenBoundaryMargin;
        float minY = bottomLeft.y + screenBoundaryMargin;
        float maxY = topRight.y - screenBoundaryMargin;

        return coverBounds.min.x <= minX
            || coverBounds.max.x >= maxX
            || coverBounds.min.y <= minY
            || coverBounds.max.y >= maxY;
    }

    private void FailForBorderContact()
    {
        state = State.Failed;

        if (!failureReported)
        {
            failureReported = true;
            onFail.Invoke();
        }

        FailImmediately();
    }
}
