using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 타이밍 낙하 미니게임.
/// 하늘(화면 위)에서 풍선(ballon)이 화면 가운데의 목표 지점으로 천천히 내려오고,
/// 풍선이 목표 지점에 겹치는 순간 스페이스를 눌러야 한다.
/// 스페이스를 누르면 Rythmgame1 그림이 Rythmgame2(누른 자세)로 바뀐다.
///
/// - 판정 구간(<see cref="hitWindow"/>) 안에서 누르면 성공. 풍선이 터지듯 부풀었다가 사라지고
///   제한 시간이 다 찰 때까지 기다렸다가 클리어 통지된다.
/// - 너무 일찍 누르면(연타 방지) 즉시 실패.
/// - 누르지 못하고 목표 지점을 지나쳐 버리면 즉시 실패.
///
/// 프리팹으로 만들어 <see cref="MiniGamePlayer"/> 가 재생한다.
/// </summary>
public class TimingDropMiniGame : TimedMiniGame
{
    // 인스펙터에 확실히 노출되도록 제네릭 UnityEvent 는 구체 타입으로 선언한다.
    [Serializable] public class AccuracyEvent : UnityEvent<float> { }

    [Header("떨어지는 그림 (ballon)")]
    [Tooltip("하늘에서 내려올 풍선 오브젝트. 타이밍을 맞추면 이 오브젝트가 사라진다")]
    [SerializeField] private Transform fallingObject;

    [Header("낙하 지점")]
    [Tooltip("출발 지점. 비워두면 카메라 화면 위쪽 바깥에서 자동으로 시작한다")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("목표 지점(판정선). 비워두면 카메라 화면 정중앙이 목표가 된다")]
    [SerializeField] private Transform targetPoint;

    [Tooltip("지점을 자동 계산할 때 쓸 카메라. 비어 있으면 Camera.main 을 쓴다")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("출발 지점 자동 계산 시 화면 위쪽 경계보다 이만큼 더 위에서 시작한다(월드 유닛)")]
    [SerializeField] private float spawnMargin = 1f;

    [Header("규칙")]
    [Tooltip("출발 지점에서 목표 지점까지 내려오는 데 걸리는 시간(초). 제한 시간보다 판정 구간만큼 짧아야 한다")]
    [SerializeField] private float fallDuration = 2.5f;

    [Tooltip("판정 허용 오차(초). 정확도 계산에만 쓴다. 목표 도달 시점에 정확히 누르면 정확도 1, 이 구간 밖이면 0. 일찍 눌러도 실패는 아니다")]
    [SerializeField] private float hitWindow = 0.25f;

    [Header("입력")]
    [Tooltip("타이밍을 맞춰 누를 키")]
    [SerializeField] private Key triggerKey = Key.Space;

    [Tooltip("같이 인정할 보조 키. 필요 없으면 None 으로 둔다")]
    [SerializeField] private Key alternateKey = Key.None;

    [Header("누르는 그림 (Rythmgame1)")]
    [Tooltip("키를 누를 때 그림이 바뀔 SpriteRenderer")]
    [SerializeField] private SpriteRenderer pressRenderer;

    [Tooltip("평소 자세. Rythmgame1")]
    [SerializeField] private Sprite idleSprite;

    [Tooltip("누른 자세. Rythmgame2")]
    [SerializeField] private Sprite pressedSprite;

    [Tooltip("성공해서 풍선이 터진 뒤에도 누른 자세를 유지할지. 끄면 평소 자세로 돌아온다")]
    [SerializeField] private bool keepPressedOnClear = true;

    [Header("성공 연출")]
    [Tooltip("풍선이 터지듯 부풀어 오르는 시간(초). 끝나면 풍선이 사라진다")]
    [SerializeField] private float hitPopDuration = 0.15f;

    [Tooltip("사라지기 직전까지 부풀어 오르는 최대 배율")]
    [SerializeField] private float hitPopScale = 1.4f;

    [Tooltip("성공 시 켜줄 연출 오브젝트(터짐 효과 등). 없으면 비워둔다")]
    [SerializeField] private GameObject hitEffect;

    [Header("이벤트")]
    [Tooltip("판정 성공 시 정확도를 넘긴다. 1 = 정확히 목표 지점, 0 = 판정 구간 가장자리")]
    public AccuracyEvent onJudged;

    public UnityEvent onClear;
    public UnityEvent onFail;

    private Vector3 startPosition;
    private Vector3 goalPosition;
    private Vector3 fallDirection = Vector3.down;
    private float fallSpeed;

    /// <summary>재생 시작 후 흐른 낙하 시간(초). <see cref="fallDuration"/> 이 되는 순간이 목표 지점.</summary>
    private float fallTime;

    private Vector3 originalScale = Vector3.one;
    private Coroutine hitRoutine;

    private void Awake()
    {
        if (fallingObject != null)
            originalScale = fallingObject.localScale;
    }

    private void Start()
    {
        // 게임 진행이 아니라 초기 배치/표시만 한다.
        ResetInternal();
    }

    protected override void OnTimedPlay()
    {
        ResetInternal();

        // 스페이스를 눌러도 그림이 안 바뀌는 흔한 원인 — 참조가 비어 있으면 미리 알려준다.
        if (pressRenderer == null || idleSprite == null || pressedSprite == null)
        {
            Debug.LogWarning(
                $"[{name}] Press Renderer / Idle Sprite / Pressed Sprite 중 비어 있는 게 있어 " +
                "스페이스를 눌러도 그림이 바뀌지 않는다. 인스펙터에서 Rythmgame1 SpriteRenderer 와 " +
                "Rythmgame1_0 / Rythmgame1_1 스프라이트를 연결하라.", this);
        }

        if (timeLimit < fallDuration + hitWindow)
        {
            Debug.LogWarning(
                $"[{name}] 제한 시간({timeLimit}s)이 낙하 시간+판정 구간({fallDuration + hitWindow}s)보다 짧다. " +
                "그림이 목표에 닿기도 전에 시간이 끝난다.", this);
        }
    }

    protected override void OnTimedStopAndReset()
    {
        if (hitRoutine != null)
        {
            StopCoroutine(hitRoutine);
            hitRoutine = null;
        }

        ResetInternal();
    }

    protected override void OnTimedUpdate()
    {
        fallTime += Time.deltaTime;
        ApplyFallPosition();

        // 목표 도달 시점을 기준으로 한 오차. 음수면 아직 오는 중, 양수면 지나간 뒤.
        float timeError = fallTime - fallDuration;

        // 키를 누르고 있는 동안은 계속 누른 자세를 보여준다. 판정과 무관하다.
        ApplyPressPose(IsTriggerHeld());

        if (IsTriggerPressed())
        {
            Judge(timeError);
            return;
        }

        // 누르지 못한 채 판정 구간을 벗어났다 — 놓쳤다.
        if (timeError > hitWindow)
            Miss();
    }

    /// <summary>낙하 시간, 위치, 스케일, 그림, 연출 오브젝트를 처음 상태로 되돌린다.</summary>
    private void ResetInternal()
    {
        fallTime = 0f;

        ResolvePoints();
        ApplyFallPosition();

        if (fallingObject != null)
        {
            // 터져서 사라진 풍선을 다음 재생을 위해 되살린다.
            fallingObject.localScale = originalScale;
            fallingObject.gameObject.SetActive(true);
        }

        ApplyPressPose(false);

        if (hitEffect != null)
            hitEffect.SetActive(false);
    }

    /// <summary>출발/목표 지점을 확정하고 낙하 속도를 계산한다. Transform 이 비어 있으면 카메라 화면 기준으로 자동 계산.</summary>
    private void ResolvePoints()
    {
        if (fallingObject == null)
            return;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        float z = fallingObject.position.z;

        goalPosition = targetPoint != null
            ? targetPoint.position
            : ViewportPoint(cam, 0.5f, 0.5f, z, 0f);

        startPosition = spawnPoint != null
            ? spawnPoint.position
            : ViewportPoint(cam, 0.5f, 1f, z, spawnMargin);

        Vector3 delta = goalPosition - startPosition;
        float distance = delta.magnitude;

        // 목표와 출발이 같은 지점이면(설정 실수) 아래로 떨어지는 것으로 처리해 0 나눗셈을 피한다.
        fallDirection = distance > Mathf.Epsilon ? delta / distance : Vector3.down;
        fallSpeed = fallDuration > 0f ? distance / fallDuration : 0f;
    }

    /// <summary>카메라 화면의 (vx, vy) 지점을 월드 좌표로 바꾼다. 카메라가 없으면 목표 지점을 그대로 쓴다.</summary>
    private Vector3 ViewportPoint(Camera cam, float vx, float vy, float z, float yOffset)
    {
        if (cam == null)
            return new Vector3(0f, yOffset, z);

        float depth = Mathf.Abs(z - cam.transform.position.z);
        Vector3 world = cam.ViewportToWorldPoint(new Vector3(vx, vy, depth));

        return new Vector3(world.x, world.y + yOffset, z);
    }

    /// <summary>현재 낙하 시간에 맞는 위치로 그림을 옮긴다. 목표를 지나쳐도 같은 속도로 계속 내려간다.</summary>
    private void ApplyFallPosition()
    {
        if (fallingObject == null)
            return;

        fallingObject.position = startPosition + fallDirection * (fallSpeed * fallTime);
    }

    /// <summary>이번 프레임에 키를 새로 눌렀는지. 판정용.</summary>
    private bool IsTriggerPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        if (triggerKey != Key.None && keyboard[triggerKey].wasPressedThisFrame)
            return true;

        return alternateKey != Key.None && keyboard[alternateKey].wasPressedThisFrame;
    }

    /// <summary>키를 누르고 있는지. 그림 교체용.</summary>
    private bool IsTriggerHeld()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        if (triggerKey != Key.None && keyboard[triggerKey].isPressed)
            return true;

        return alternateKey != Key.None && keyboard[alternateKey].isPressed;
    }

    /// <summary>
    /// 키를 누른 순간의 오차로 판정한다.
    /// 판정 구간 안이면 성공, 그보다 일찍 눌렀으면 실패가 아니라 그냥 무시한다(그림만 눌린 자세로 바뀐다).
    /// 놓치는 것(<see cref="Miss"/>)만 실패다.
    /// </summary>
    private void Judge(float timeError)
    {
        float offset = Mathf.Abs(timeError);

        // 목표 지점 판정 구간 안에서 눌렀을 때만 성공.
        if (offset <= hitWindow)
            Hit(offset);

        // 그 전에 미리 눌렀으면 아무 일도 없다. 풍선은 계속 내려오고 다시 누를 수 있다.
    }

    /// <summary>풍선을 터뜨렸다. 성공 연출을 재생한다.</summary>
    private void Hit(float offset)
    {
        float accuracy = hitWindow > 0f ? 1f - Mathf.Clamp01(offset / hitWindow) : 1f;
        onJudged.Invoke(accuracy);

        // 성공 확정 이후엔 OnTimedUpdate 가 불리지 않으므로 그림은 이 자리에 멈춘다.
        SucceedWhenTimeUp();
        hitRoutine = StartCoroutine(HitRoutine());
    }

    /// <summary>목표 지점을 지나칠 때까지 누르지 못했다.</summary>
    private void Miss()
    {
        onFail.Invoke();
        FailImmediately();
    }

    /// <summary>성공 연출: 풍선이 부풀어 오르다가 터지듯 사라지고, 터짐 효과를 켠다.</summary>
    private IEnumerator HitRoutine()
    {
        if (fallingObject != null && hitPopDuration > 0f)
        {
            float time = 0f;
            while (time < hitPopDuration)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / hitPopDuration);

                fallingObject.localScale = originalScale * Mathf.Lerp(1f, hitPopScale, k);

                yield return null;
            }
        }

        // 풍선이 사라진다. 다음 재생 때 ResetInternal 이 되살린다.
        if (fallingObject != null)
            fallingObject.gameObject.SetActive(false);

        // 성공 후엔 OnTimedUpdate 가 멈춰 누른 자세가 그대로 남는다. 원하면 평소 자세로 되돌린다.
        if (!keepPressedOnClear)
            ApplyPressPose(false);

        if (hitEffect != null)
            hitEffect.SetActive(true);

        hitRoutine = null;
        onClear.Invoke();
    }

    /// <summary>
    /// 누름 여부에 맞는 그림을 적용한다.
    /// 누르고 있으면 <see cref="pressedSprite"/>(Rythmgame2), 떼면 <see cref="idleSprite"/>(Rythmgame1).
    /// </summary>
    private void ApplyPressPose(bool pressed)
    {
        if (pressRenderer == null)
            return;

        Sprite target = pressed ? pressedSprite : idleSprite;

        // 같은 그림이면 굳이 다시 대입하지 않는다.
        if (target != null && pressRenderer.sprite != target)
            pressRenderer.sprite = target;
    }
}
