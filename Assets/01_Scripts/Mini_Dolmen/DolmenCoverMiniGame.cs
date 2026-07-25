using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 두 받침돌 위에 덮개돌을 조심스럽게 올리는 고인돌 미니게임.
/// 덮개돌은 WASD/방향키로 힘을 가해 조작하며, 모든 돌은 2D 물리를 사용한다.
/// </summary>
public sealed class DolmenCoverMiniGame : TimedMiniGame
{
    private enum State
    {
        Idle,
        Playing,
        SuccessPending,
        Failed,
    }

    [Header("돌 구성")]
    [Tooltip("미리 놓인 왼쪽/오른쪽 받침돌. 반드시 두 개를 지정한다.")]
    [SerializeField] private DolmenStone[] supportStones = new DolmenStone[2];

    [Tooltip("플레이어가 움직일 길쭉한 덮개돌.")]
    [SerializeField] private DolmenStone coverStone;

    [Tooltip("비어 있으면 자식의 모든 DolmenStone을 자동으로 찾아 리셋/실패 판정에 사용한다.")]
    [SerializeField] private DolmenStone[] allStones;

    [Header("키보드 조작")]
    [Tooltip("WASD 또는 방향키를 누르는 동안 덮개돌에 가하는 힘.")]
    [SerializeField] private float controlForce = 18f;

    [Tooltip("조작으로 낼 수 있는 덮개돌의 최대 속도. 너무 빠르게 부딪혀 무너지는 일을 줄인다.")]
    [SerializeField] private float maxControlSpeed = 4f;

    [Header("안정 판정")]
    [Tooltip("덮개돌이 이 각도보다 많이 기울면 즉시 실패한다.")]
    [SerializeField] private float maxCoverTilt = 16f;

    [Tooltip("받침돌이 이 각도보다 많이 기울면 즉시 실패한다.")]
    [SerializeField] private float maxSupportTilt = 13f;

    [Tooltip("두 받침돌에 닿고, 속도와 각도가 안정 범위인 상태를 유지해야 하는 시간.")]
    [SerializeField] private float stableDuration = 0.2f;

    [Tooltip("타임아웃 직전 이 시간 안에 최종 안정 상태여야 성공을 예약한다. TimedMiniGame의 타임아웃 규약상 성공 예약은 타임아웃 전에 이뤄져야 한다.")]
    [SerializeField] private float finalCheckLeadTime = 0.08f;

    [Header("실패 경계")]
    [Tooltip("지정하면 모든 돌의 Collider가 이 영역을 완전히 벗어나는 순간 실패한다. 비어 있으면 카메라 화면을 사용한다.")]
    [SerializeField] private Collider2D failureBounds;

    [Tooltip("카메라 화면을 실패 경계로 쓸 때 안쪽으로 줄일 여백(월드 단위).")]
    [SerializeField] private float screenBoundaryMargin = 0.25f;

    [Tooltip("카메라가 없을 때 사용하는 아래쪽 실패 높이.")]
    [SerializeField] private float fallbackFailureHeight = -5f;

    [Tooltip("각도 초과 또는 경계 이탈 상태가 이 시간 이상 연속으로 유지되면 실패한다.")]
    [SerializeField] private float physicsFailureDelay = 0.5f;

    [Header("이벤트")]
    public UnityEvent onClear;
    public UnityEvent onFail;

    private Camera targetCamera;
    private float stableElapsed;
    private float physicsFailureElapsed;
    private bool controlLocked;
    private bool failureReported;
    private State state = State.Idle;

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

        LockControlAfterFirstSupportContact();

        if (!controlLocked)
            ApplyCoverControl();

        if (HasFailedPhysicsState())
        {
            stableElapsed = 0f;
            physicsFailureElapsed += Time.deltaTime;

            if (physicsFailureElapsed >= Mathf.Max(0f, physicsFailureDelay))
                FailForPhysicsState();

            return;
        }

        physicsFailureElapsed = 0f;

        if (!IsFinalPlacementStable())
        {
            stableElapsed = 0f;
            return;
        }

        stableElapsed += Time.deltaTime;

        // TimedMiniGame은 OnTimeUp에 들어가기 전에 결과를 잠근다. 따라서 마지막 Update에서
        // 최종 배치를 확인해 성공을 예약하면, 다음 프레임의 정확한 제한시간에 성공이 통지된다.
        float requiredStableTime = Mathf.Max(0f, stableDuration);
        float latestReservationTime = Mathf.Max(0f, timeLimit - Mathf.Max(0.001f, finalCheckLeadTime));
        if (stableElapsed >= requiredStableTime && Elapsed >= latestReservationTime)
        {
            state = State.SuccessPending;
            onClear.Invoke();
            SucceedWhenTimeUp();
        }
    }

    protected override void OnTimeUp()
    {
        state = State.Failed;
        controlLocked = true;
        ReportFailureEvent();
        base.OnTimeUp();
    }

    private void ResetInternal(bool enablePhysics)
    {
        stableElapsed = 0f;
        physicsFailureElapsed = 0f;
        controlLocked = false;
        failureReported = false;

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

    /// <summary>
    /// 윗돌이 어느 한 아랫돌에 처음 닿는 순간 배치를 확정하고 이후 조작을 잠근다.
    /// 물리 시뮬레이션은 계속되어 돌이 안정되거나 무너지는 과정은 그대로 진행된다.
    /// </summary>
    private void LockControlAfterFirstSupportContact()
    {
        if (controlLocked || coverStone == null || supportStones == null)
            return;

        for (int i = 0; i < supportStones.Length; i++)
        {
            DolmenStone supportStone = supportStones[i];
            if (supportStone != null && coverStone.IsRestingOn(supportStone))
            {
                controlLocked = true;
                return;
            }
        }
    }

    private void ApplyCoverControl()
    {
        if (coverStone == null)
            return;

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
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            direction.y += 1f;

        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        coverStone.ApplyControlForce(direction, controlForce, maxControlSpeed);
    }

    private bool IsFinalPlacementStable()
    {
        if (coverStone == null || supportStones == null || supportStones.Length != 2)
            return false;

        DolmenStone leftSupport = supportStones[0];
        DolmenStone rightSupport = supportStones[1];
        if (leftSupport == null || rightSupport == null)
            return false;

        return coverStone.IsRestingOn(leftSupport)
            && coverStone.IsRestingOn(rightSupport)
            && coverStone.IsStable()
            && leftSupport.IsStable()
            && rightSupport.IsStable()
            && coverStone.IsAngleWithin(maxCoverTilt)
            && leftSupport.IsAngleWithin(maxSupportTilt)
            && rightSupport.IsAngleWithin(maxSupportTilt)
            && IsCoverBetweenSupports(leftSupport, rightSupport);
    }

    private bool IsCoverBetweenSupports(DolmenStone firstSupport, DolmenStone secondSupport)
    {
        Collider2D coverCollider = coverStone.HitCollider;
        Collider2D firstCollider = firstSupport.HitCollider;
        Collider2D secondCollider = secondSupport.HitCollider;
        if (coverCollider == null || firstCollider == null || secondCollider == null)
            return false;

        float minSupportX = Mathf.Min(firstCollider.bounds.center.x, secondCollider.bounds.center.x);
        float maxSupportX = Mathf.Max(firstCollider.bounds.center.x, secondCollider.bounds.center.x);
        float coverX = coverCollider.bounds.center.x;
        return coverX >= minSupportX && coverX <= maxSupportX;
    }

    private bool HasFailedPhysicsState()
    {
        if (allStones == null || allStones.Length == 0)
            return true;

        for (int i = 0; i < allStones.Length; i++)
        {
            DolmenStone stone = allStones[i];
            if (stone == null)
                return true;

            float maxTilt = stone == coverStone ? maxCoverTilt : maxSupportTilt;
            if (!stone.IsAngleWithin(maxTilt) || !IsStoneInsideFailureBounds(stone))
                return true;
        }

        return false;
    }

    private bool IsStoneInsideFailureBounds(DolmenStone stone)
    {
        Collider2D stoneCollider = stone.HitCollider;
        if (stoneCollider == null)
            return false;

        Bounds stoneBounds = stoneCollider.bounds;
        if (failureBounds != null)
        {
            Bounds allowedBounds = failureBounds.bounds;
            return allowedBounds.Contains(stoneBounds.min) && allowedBounds.Contains(stoneBounds.max);
        }

        if (targetCamera != null && targetCamera.orthographic)
        {
            Vector3 bottomLeft = targetCamera.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
            Vector3 topRight = targetCamera.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));
            float minX = bottomLeft.x + screenBoundaryMargin;
            float maxX = topRight.x - screenBoundaryMargin;
            float minY = bottomLeft.y + screenBoundaryMargin;
            float maxY = topRight.y - screenBoundaryMargin;
            return stoneBounds.min.x >= minX && stoneBounds.max.x <= maxX
                && stoneBounds.min.y >= minY && stoneBounds.max.y <= maxY;
        }

        return stoneBounds.max.y >= fallbackFailureHeight;
    }

    private void FailForPhysicsState()
    {
        state = State.Failed;
        controlLocked = true;
        ReportFailureEvent();
        FailImmediately();
    }

    private void ReportFailureEvent()
    {
        if (failureReported)
            return;

        failureReported = true;
        onFail.Invoke();
    }
}
