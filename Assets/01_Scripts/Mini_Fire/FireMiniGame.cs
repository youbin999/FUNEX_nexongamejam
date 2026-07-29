using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 불 붙이기 미니게임.
/// 스페이스바를 연타할 때마다 왼쪽 오브젝트가 오른쪽 오브젝트를 친다.
/// 제한 시간 안에 정해진 횟수를 채우면 클리어, 못 채우면 실패.
/// 프리팹으로 만들어 <see cref="MiniGamePlayer"/> 가 재생한다.
/// </summary>
public class FireMiniGame : MiniGame
{
    // 인스펙터에 확실히 노출되도록 제네릭 UnityEvent 는 구체 타입으로 선언한다.
    [Serializable] public class HitEvent : UnityEvent<int> { }
    [Serializable] public class RatioEvent : UnityEvent<float> { }

    private enum State
    {
        Idle,
        Playing,
        Cleared,
        Failed,
    }

    [Header("오브젝트")]
    [Tooltip("때리는 쪽 (왼쪽)")]
    [SerializeField] private Striker striker;

    [Tooltip("맞는 쪽 (오른쪽). 비워둬도 동작한다")]
    [SerializeField] private StrikeTarget target;

    [Header("규칙")]
    [Tooltip("클리어에 필요한 연타 횟수")]
    [SerializeField] private int clearCount = 20;

    [Tooltip("제한 시간(초). 게임이 시작되면 입력과 상관없이 흐른다")]
    [SerializeField] private float timeLimit = 3f;

    [Header("이벤트")]
    [Tooltip("칠 때마다 현재 횟수를 넘긴다")]
    public HitEvent onHit;

    [Tooltip("0에서 1로 차오르는 불 게이지. Image.fillAmount 에 연결한다")]
    public RatioEvent onProgressChanged;

    [Tooltip("0에서 1로 차오르는 타이머 게이지. Image.fillAmount 에 연결한다")]
    public RatioEvent onTimerChanged;

    public UnityEvent onClear;
    public UnityEvent onFail;

    private int hitCount;
    private float elapsed;
    private State state = State.Idle;

    // 폴링(wasPressedThisFrame)은 한 프레임에 두 번 눌러도 한 번으로 먹는다.
    // 연타 게임이라 입력 이벤트를 직접 받아서 하나도 놓치지 않게 한다.
    private InputAction hitAction;

    /// <summary>지금까지 친 횟수.</summary>
    public int HitCount => hitCount;

    /// <summary>0에서 1로 차오르는 진행도.</summary>
    public float Progress => clearCount > 0 ? Mathf.Clamp01((float)hitCount / clearCount) : 0f;

    /// <summary>제한 시간이 잡혀 있으면 씬 공용 타이머 UI에 표시한다.</summary>
    public override bool HasTimer => timeLimit > 0f;

    /// <summary>0에서 1로 차오르는 타이머 값.</summary>
    public override float TimerRatio => timeLimit > 0f ? Mathf.Clamp01(elapsed / timeLimit) : 0f;

    /// <summary>스페이스 입력 액션을 만들어 두고 게이지를 0으로 비운다. 입력은 재생할 때 열린다.</summary>
    private void Awake()
    {
        hitAction = new InputAction("Hit", InputActionType.Button, "<Keyboard>/space");
        hitAction.performed += OnHitPerformed;

        // 재생 전에는 게이지만 비워 둔다. 입력은 OnPlay 에서 열린다.
        onProgressChanged.Invoke(0f);
        onTimerChanged.Invoke(0f);
    }

    /// <summary>입력 액션을 닫는다.</summary>
    private void OnDisable()
    {
        hitAction.Disable();
    }

    /// <summary>입력 액션 구독을 풀고 폐기한다.</summary>
    private void OnDestroy()
    {
        hitAction.performed -= OnHitPerformed;
        hitAction.Dispose();
    }

    /// <summary>재생 중일 때만 제한 시간을 흘린다.</summary>
    private void Update()
    {
        if (state != State.Playing)
            return;

        TickTimer();
    }

    // 한 프레임에 입력이 여러 번 들어오면 그만큼 여러 번 호출된다.
    private void OnHitPerformed(InputAction.CallbackContext context)
    {
        Hit();
    }

    /// <summary>입력 없이 한 번 치게 하고 싶을 때 직접 호출해도 된다.</summary>
    public void Hit()
    {
        if (state != State.Playing)
            return;

        // 맞는 쪽 반응은 Striker 의 onImpact 에서 부르는 게 타이밍이 맞다.
        // 인스펙터에서 연결하지 않았다면 여기서 대신 불러 준다.
        if (striker != null)
            striker.Strike();
        else if (target != null)
            target.Hit();

        hitCount++;
        onHit.Invoke(hitCount);
        onProgressChanged.Invoke(Progress);

        if (hitCount >= clearCount)
        {
            state = State.Cleared;
            onClear.Invoke();
            ReportFinished(true);
        }
    }

    /// <summary>게임을 시작한다. 상태를 초기화하고 Playing 으로 전환한다.</summary>
    protected override void OnPlay()
    {
        ResetInternal();

        // 게이지는 재생을 시작하는 이 시점에만 0으로 되돌린다.
        // 끝난 뒤에도 마지막 상태(다 찬 타이머, 커진 불)가 화면에 남아야 결과가 보인다.
        onProgressChanged.Invoke(0f);
        onTimerChanged.Invoke(0f);

        state = State.Playing;
        hitAction.Enable();
    }

    /// <summary>게임을 강제 중단하고 초기 상태(Idle)로 되돌린다.</summary>
    protected override void OnStopAndReset()
    {
        hitAction.Disable();
        ResetInternal();
        state = State.Idle;
    }

    /// <summary>
    /// 횟수와 타이머, 자세를 처음으로 되돌린다.
    /// 게이지 이벤트는 여기서 쏘지 않는다. 종료 직후에도 결과 화면이 유지돼야 하기 때문이고,
    /// 다음 판을 위한 초기화는 <see cref="OnPlay"/> 가 책임진다.
    /// </summary>
    private void ResetInternal()
    {
        hitCount = 0;
        elapsed = 0f;

        if (striker != null)
            striker.ResetPose();

        if (target != null)
            target.ResetPose();
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
}
