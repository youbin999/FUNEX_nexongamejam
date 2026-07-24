using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 찾아서 켜! 미니게임.
/// 화면이 캄캄한 상태에서 마우스 자리만 희미하게 밝아진다.
/// 더듬어서 벽에 숨겨진 스위치를 찾아 누르면 클리어, 제한 시간 안에 못 찾으면 실패.
/// 프리팹으로 만들어 <see cref="MiniGamePlayer"/> 가 재생한다.
/// </summary>
public class LightMiniGame : MiniGame
{
    // 인스펙터에 확실히 노출되도록 제네릭 UnityEvent 는 구체 타입으로 선언한다.
    [Serializable] public class RatioEvent : UnityEvent<float> { }

    private enum State
    {
        Idle,
        Playing,
        Cleared,
        Failed,
    }

    [Header("빛")]
    [Tooltip("마우스를 따라다니는 빛")]
    [SerializeField] private PointerLight pointerLight;

    [Tooltip("켜면 누르고 있는 동안에만 빛이 따라온다. 끄면 마우스만 움직여도 따라온다")]
    [SerializeField] private bool lightFollowsOnlyWhileHeld = false;

    [Header("스위치")]
    [Tooltip("찾아야 하는 두꺼비집/스위치의 콜라이더. 여기를 누르면 클리어다")]
    [SerializeField] private Collider2D switchCollider;

    [Header("규칙")]
    [Tooltip("제한 시간(초). 게임이 시작되면 조작과 상관없이 흐른다")]
    [SerializeField] private float timeLimit = 5f;

    [Header("클릭 판정")]
    [Tooltip("비워두면 Camera.main 을 쓴다")]
    [SerializeField] private Camera targetCamera;

    [Header("이벤트")]
    [Tooltip("0에서 1로 차오르는 타이머 게이지. Image.fillAmount 에 연결한다")]
    public RatioEvent onTimerChanged;

    [Tooltip("엉뚱한 곳을 눌렀을 때. 툭 하는 소리나 화면 흔들림을 연결한다")]
    public UnityEvent onMissed;

    [Tooltip("재생을 시작할 때 발화한다. 연출 컴포넌트들의 ResetPose 를 연결해 지난 판의 결과를 지운다")]
    public UnityEvent onReset;

    [Tooltip("스위치를 찾았을 때. 방 밝히기·전구 교체·에디슨 등장을 전부 여기 연결한다")]
    public UnityEvent onClear;

    public UnityEvent onFail;

    private float elapsed;
    private State state = State.Idle;

    /// <summary>0에서 1로 차오르는 타이머 값.</summary>
    public float TimerRatio => timeLimit > 0f ? Mathf.Clamp01(elapsed / timeLimit) : 0f;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        onTimerChanged.Invoke(0f);
    }

    private void Update()
    {
        if (state != State.Playing)
            return;

        TickTimer();

        // 이번 프레임에 시간이 다 됐으면 조작은 안 받는다.
        if (state != State.Playing)
            return;

        HandlePointer();
    }

    /// <summary>게임을 시작한다. 상태를 초기화하고 Playing 으로 전환한다.</summary>
    protected override void OnPlay()
    {
        ResetInternal();

        // 지난 판의 연출(밝아진 방, 켜진 전구, 나와 있는 에디슨)을 여기서 지운다.
        // 게임이 끝난 직후가 아니라 다음 판을 시작할 때 지워야 결과를 볼 수 있다.
        onReset.Invoke();
        onTimerChanged.Invoke(0f);

        // 시작하자마자 빛이 화면을 가로질러 날아오지 않도록 현재 마우스 자리에 붙여 둔다.
        if (pointerLight != null && TryGetPointerWorld(out Vector2 worldPosition, out _, out _))
            pointerLight.SnapTo(worldPosition);

        state = State.Playing;
    }

    /// <summary>게임을 강제 중단하고 초기 상태(Idle)로 되돌린다.</summary>
    protected override void OnStopAndReset()
    {
        ResetInternal();
        state = State.Idle;
    }

    /// <summary>
    /// 타이머와 빛을 처음으로 되돌린다.
    /// 연출은 여기서 건드리지 않는다. 종료 직후에도 결과가 화면에 남아야 하기 때문이고,
    /// 다음 판을 위한 초기화는 <see cref="OnPlay"/> 의 onReset 이 책임진다.
    /// </summary>
    private void ResetInternal()
    {
        elapsed = 0f;

        if (pointerLight != null)
            pointerLight.ResetPose();
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
        ReportFinished(false);
    }

    private void HandlePointer()
    {
        if (targetCamera == null)
            return;

        if (!TryGetPointerWorld(out Vector2 worldPosition, out bool pressedThisFrame, out bool held))
            return;

        // 빛 따라가기
        if (pointerLight != null && (held || !lightFollowsOnlyWhileHeld))
            pointerLight.MoveTo(worldPosition);

        if (!pressedThisFrame)
            return;

        if (switchCollider != null && switchCollider.OverlapPoint(worldPosition))
        {
            state = State.Cleared;
            onClear.Invoke();
            ReportFinished(true);
            return;
        }

        onMissed.Invoke();
    }

    /// <summary>포인터의 월드 좌표와 눌림 상태를 함께 돌려준다.</summary>
    private bool TryGetPointerWorld(out Vector2 worldPosition, out bool pressedThisFrame, out bool held)
    {
        Vector2 screenPosition;

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            screenPosition = mouse.position.ReadValue();
            pressedThisFrame = mouse.leftButton.wasPressedThisFrame;
            held = mouse.leftButton.isPressed;
        }
        else
        {
            Touchscreen touch = Touchscreen.current;
            if (touch == null)
            {
                worldPosition = default;
                pressedThisFrame = false;
                held = false;
                return false;
            }

            screenPosition = touch.primaryTouch.position.ReadValue();
            pressedThisFrame = touch.primaryTouch.press.wasPressedThisFrame;
            held = touch.primaryTouch.press.isPressed;
        }

        if (targetCamera == null)
        {
            worldPosition = default;
            return false;
        }

        worldPosition = targetCamera.ScreenToWorldPoint(screenPosition);
        return true;
    }
}
