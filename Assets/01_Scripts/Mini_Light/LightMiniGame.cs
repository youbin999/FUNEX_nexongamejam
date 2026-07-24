using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 찾아서 켜! 미니게임.
/// 화면이 캄캄한 상태에서 마우스 자리만 희미하게 밝아진다.
/// 더듬어서 벽에 숨겨진 스위치를 찾아 누르면 성공, 제한 시간 안에 못 찾으면 실패.
/// 성공해도 바로 끝나지 않는다. 입력만 잠긴 채 제한 시간이 다 찰 때까지
/// 방이 밝아지고 에디슨이 튀어나오는 연출을 보여준 뒤 통지된다 (<see cref="TimedMiniGame"/> 규칙).
/// 프리팹으로 만들어 <see cref="MiniGamePlayer"/> 가 재생한다.
/// </summary>
public class LightMiniGame : TimedMiniGame
{
    [Header("빛")]
    [Tooltip("마우스를 따라다니는 빛")]
    [SerializeField] private PointerLight pointerLight;

    [Tooltip("켜면 누르고 있는 동안에만 빛이 따라온다. 끄면 마우스만 움직여도 따라온다")]
    [SerializeField] private bool lightFollowsOnlyWhileHeld = false;

    [Header("스위치")]
    [Tooltip("찾아야 하는 두꺼비집/스위치의 콜라이더. 여기를 누르면 성공이다")]
    [SerializeField] private Collider2D switchCollider;

    [Header("클릭 판정")]
    [Tooltip("비워두면 Camera.main 을 쓴다")]
    [SerializeField] private Camera targetCamera;

    [Header("이벤트")]
    [Tooltip("엉뚱한 곳을 눌렀을 때. 툭 하는 소리나 화면 흔들림을 연결한다")]
    public UnityEvent onMissed;

    [Tooltip("재생을 시작할 때 발화한다. 연출 컴포넌트들의 ResetPose 를 연결해 지난 판의 결과를 지운다")]
    public UnityEvent onReset;

    [Tooltip("스위치를 찾았을 때. 방 밝히기·전구 교체·에디슨 등장을 전부 여기 연결한다")]
    public UnityEvent onClear;

    public UnityEvent onFail;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    /// <summary>게임을 시작한다. 타이머는 베이스가 이미 0으로 맞춰 둔 상태다.</summary>
    protected override void OnTimedPlay()
    {
        // 지난 판의 연출(밝아진 방, 켜진 전구, 나와 있는 에디슨)을 여기서 지운다.
        // 게임이 끝난 직후가 아니라 다음 판을 시작할 때 지워야 결과를 볼 수 있다.
        onReset.Invoke();

        // 시작하자마자 빛이 화면을 가로질러 날아오지 않도록 현재 마우스 자리에 붙여 둔다.
        if (pointerLight != null && TryGetPointerWorld(out Vector2 worldPosition, out _, out _))
            pointerLight.SnapTo(worldPosition);
    }

    /// <summary>게임을 강제 중단하고 초기 상태로 되돌린다.</summary>
    protected override void OnTimedStopAndReset()
    {
        if (pointerLight != null)
            pointerLight.ResetPose();
    }

    /// <summary>스위치를 찾기 전까지만 불린다. 성공이 확정되면 베이스가 호출을 멈춘다.</summary>
    protected override void OnTimedUpdate()
    {
        HandlePointer();
    }

    /// <summary>제한 시간을 다 써서 실패로 확정될 때. 성공한 판에서는 불리지 않는다.</summary>
    protected override void OnTimeUp()
    {
        onFail.Invoke();
        base.OnTimeUp();
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
            onClear.Invoke();

            // 여기서 끝내지 않는다. 입력만 잠기고, 제한 시간이 다 차면 베이스가 성공을 통지한다.
            SucceedWhenTimeUp();
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
