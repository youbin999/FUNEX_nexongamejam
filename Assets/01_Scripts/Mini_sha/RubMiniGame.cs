using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 손 비비기 미니게임.
/// A / D 를 번갈아 누를 때마다 양손이 서로 반대로 위/아래로 움직인다.
/// 같은 키를 연달아 눌러도 진행되지 않는다 — 반드시 번갈아 눌러야 한다.
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

    /// <summary>번갈아 눌러야 하는 두 키 중 어느 쪽을 마지막에 인정받았는지.</summary>
    private enum Side
    {
        None,
        First,
        Second,
    }

    [Header("손")]
    [SerializeField] private HandRub leftHand;
    [SerializeField] private HandRub rightHand;

    [Header("규칙")]
    [Tooltip("클리어에 필요한 연타 횟수")]
    [SerializeField] private int clearCount = 10;

    [Header("조작")]
    [Tooltip("번갈아 누를 두 키 중 첫 번째")]
    [SerializeField] private Key firstKey = Key.A;

    [Tooltip("번갈아 누를 두 키 중 두 번째")]
    [SerializeField] private Key secondKey = Key.D;

    [Header("이벤트")]
    [Tooltip("한 번 비빌 때마다 현재 횟수를 넘긴다")]
    public RubEvent onRub;

    [Tooltip("같은 키를 연달아 눌렀을 때. 헛손질 피드백(소리 등)을 연결한다")]
    public UnityEvent onWrongKey;

    [Tooltip("재생을 시작할 때 발화한다. 연출 컴포넌트들의 초기화(ResetPose 등)를 연결한다")]
    public UnityEvent onReset;

    public UnityEvent onClear;
    public UnityEvent onFail;

    private int rubCount;
    private int direction = 1;
    private Side lastSide = Side.None;
    private State state = State.Idle;

    /// <summary>지금까지 비빈 횟수.</summary>
    public int RubCount => rubCount;

    protected override void OnTimedUpdate()
    {
        HandleKeys();
    }

    /// <summary>A / D 를 번갈아 눌렀는지 판정한다.</summary>
    private void HandleKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        bool firstPressed = keyboard[firstKey].wasPressedThisFrame;
        bool secondPressed = keyboard[secondKey].wasPressedThisFrame;

        if (!firstPressed && !secondPressed)
            return;

        // 첫 입력은 어느 쪽으로 시작해도 인정한다.
        if (lastSide == Side.None)
        {
            lastSide = firstPressed ? Side.First : Side.Second;
            Rub();
            return;
        }

        Side want = lastSide == Side.First ? Side.Second : Side.First;
        bool wantPressed = want == Side.First ? firstPressed : secondPressed;

        // 눌러야 할 쪽이 안 눌렸다면 같은 키를 연달아 누른 것이다. 진행하지 않는다.
        // 한 프레임에 둘 다 눌렸을 때는 이 검사를 통과하므로 정확히 한 번만 먹는다.
        if (!wantPressed)
        {
            onWrongKey.Invoke();
            return;
        }

        lastSide = want;
        Rub();
    }

    /// <summary>키 입력 없이 한 번 비비게 하고 싶을 때 직접 호출해도 된다.</summary>
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

        // 지난 판의 연출을 여기서 지운다. 종료 직후가 아니라 다음 판을 시작할 때 지워야 결과를 볼 수 있다.
        onReset.Invoke();

        state = State.Playing;
    }

    /// <summary>게임을 강제 중단하고 초기 상태(Idle)로 되돌린다.</summary>
    protected override void OnTimedStopAndReset()
    {
        ResetInternal();
        state = State.Idle;
    }

    /// <summary>횟수, 손 자세와 방향, 번갈아 누르기 기준을 처음으로 되돌린다.</summary>
    private void ResetInternal()
    {
        rubCount = 0;
        direction = 1;
        lastSide = Side.None;

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
}
