using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 손 비비기 미니게임.
/// A로 시작해서 A, D를 번갈아 눌러야 손이 한 번씩 엇갈리며 움직인다.
/// 제한 시간 안에 정해진 횟수를 채우면 클리어, 못 채우면 실패.
/// 빈 GameObject에 붙여서 쓴다.
/// </summary>
public class RubMiniGame : MonoBehaviour
{
    // 인스펙터에 확실히 노출되도록 제네릭 UnityEvent 는 구체 타입으로 선언한다.
    [Serializable] public class RubEvent : UnityEvent<int> { }
    [Serializable] public class TimerEvent : UnityEvent<float> { }

    private enum State
    {
        Playing,
        Cleared,
        Failed,
    }

    private enum RubKey
    {
        None,
        A,
        D,
    }

    [Header("손")]
    [SerializeField] private HandRub leftHand;
    [SerializeField] private HandRub rightHand;

    [Header("규칙")]
    [Tooltip("클리어에 필요한 입력 횟수")]
    [SerializeField] private int clearCount = 10;

    [Tooltip("제한 시간(초). 게임이 시작되면 입력과 상관없이 흐른다")]
    [SerializeField] private float timeLimit = 3f;

    [Header("이벤트")]
    [Tooltip("한 번 비빌 때마다 현재 횟수를 넘긴다")]
    public RubEvent onRub;

    [Tooltip("0에서 1로 차오르는 게이지 값. Image.fillAmount 에 그대로 연결하면 된다")]
    public TimerEvent onTimerChanged;

    public UnityEvent onClear;
    public UnityEvent onFail;

    private int rubCount;
    private float elapsed;
    private State state = State.Playing;

    // 마지막으로 인정된 키. 같은 키를 연타하면 진행되지 않는다.
    private RubKey lastAcceptedKey = RubKey.None;

    /// <summary>지금까지 비빈 횟수.</summary>
    public int RubCount => rubCount;

    /// <summary>0에서 1로 차오르는 타이머 값.</summary>
    public float TimerRatio => timeLimit > 0f ? Mathf.Clamp01(elapsed / timeLimit) : 0f;

    private void Start()
    {
        onTimerChanged.Invoke(0f);
    }

    private void Update()
    {
        if (state != State.Playing)
            return;

        TickTimer();

        // 이번 프레임에 시간이 다 됐으면 입력은 안 받는다.
        if (state != State.Playing)
            return;

        RubKey pressed = ReadPressedKey();
        if (pressed == RubKey.None)
            return;

        // 시작은 반드시 A 부터.
        if (lastAcceptedKey == RubKey.None && pressed != RubKey.A)
            return;

        // 직전과 같은 키면 무시한다. A, D 를 번갈아 눌러야 진행된다.
        if (pressed == lastAcceptedKey)
            return;

        lastAcceptedKey = pressed;
        Rub(pressed == RubKey.A ? 1 : -1);
    }

    /// <summary>입력 없이 한 번 비비게 하고 싶을 때 직접 호출해도 된다.</summary>
    public void Rub(int direction)
    {
        if (state != State.Playing)
            return;

        // 양손이 서로 반대 방향으로 엇갈려야 비비는 것처럼 보인다.
        if (leftHand != null)
            leftHand.MoveTo(direction);

        if (rightHand != null)
            rightHand.MoveTo(-direction);

        rubCount++;
        onRub.Invoke(rubCount);

        if (rubCount >= clearCount)
        {
            state = State.Cleared;
            onClear.Invoke();
        }
    }

    /// <summary>횟수와 타이머, 자세를 처음으로 되돌린다.</summary>
    public void ResetGame()
    {
        rubCount = 0;
        elapsed = 0f;
        state = State.Playing;
        lastAcceptedKey = RubKey.None;

        onTimerChanged.Invoke(0f);

        if (leftHand != null)
            leftHand.ResetPose();

        if (rightHand != null)
            rightHand.ResetPose();
    }

    private void TickTimer()
    {
        elapsed += Time.deltaTime;
        onTimerChanged.Invoke(TimerRatio);

        if (elapsed < timeLimit)
            return;

        elapsed = timeLimit;
        state = State.Failed;
        onFail.Invoke();
    }

    private RubKey ReadPressedKey()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return RubKey.None;

        if (keyboard.aKey.wasPressedThisFrame)
            return RubKey.A;

        if (keyboard.dKey.wasPressedThisFrame)
            return RubKey.D;

        return RubKey.None;
    }
}
