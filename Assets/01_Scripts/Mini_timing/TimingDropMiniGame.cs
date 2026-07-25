using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 중세 "풍선 터뜨리기" 미니게임 (철의 처녀 문 닫기).
/// 하늘에서 풍선이 좌우로 살랑거리며 내려오고, 풍선이 열린 문 정중앙에 들어온 순간
/// 스페이스를 눌러 문을 닫아 풍선을 터뜨린다.
///
/// - 문 가운데 판정 영역(<see cref="hitToleranceX"/> x <see cref="hitToleranceY"/>) 안에서 누르면 성공.
///   문이 닫히고 풍선이 터진 뒤, 제한 시간이 다 찰 때까지 기다렸다가 클리어 통지된다.
/// - 판정 영역 밖에서 누르면 헛닫힘 — <see cref="failOnMistimedPress"/> 가 켜져 있으면 즉시 실패,
///   꺼져 있으면 문이 잠깐 닫혔다 다시 열려 재시도할 수 있다.
/// - 누르지 못하고 풍선이 문을 지나쳐 내려가면 즉시 실패.
///
/// 프리팹으로 만들어 <see cref="MiniGamePlayer"/> 가 재생한다.
/// </summary>
public class TimingDropMiniGame : TimedMiniGame
{
    // 인스펙터에 확실히 노출되도록 제네릭 UnityEvent 는 구체 타입으로 선언한다.
    [Serializable] public class AccuracyEvent : UnityEvent<float> { }

    [Header("떨어지는 풍선 (ballon)")]
    [Tooltip("하늘에서 내려올 풍선 오브젝트. 타이밍을 맞추면 문에 끼어 터진다")]
    [SerializeField] private Transform fallingObject;

    [Header("낙하 지점")]
    [Tooltip("출발 지점. 비워두면 문 바로 위, 카메라 화면 바깥에서 자동으로 시작한다")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("문 가운데(판정 지점). 비워두면 문 오브젝트 위치 → 카메라 화면 정중앙 순으로 대신 쓴다")]
    [SerializeField] private Transform targetPoint;

    [Tooltip("지점을 자동 계산할 때 쓸 카메라. 비어 있으면 Camera.main 을 쓴다")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("출발 지점 자동 계산 시 화면 위쪽 경계보다 이만큼 더 위에서 시작한다(월드 유닛)")]
    [SerializeField] private float spawnMargin = 1f;

    [Header("낙하 규칙")]
    [Tooltip("출발 지점에서 문 가운데까지 내려오는 데 걸리는 시간(초). 제한 시간보다 짧아야 한다")]
    [SerializeField] private float fallDuration = 2.5f;

    [Header("좌우 흔들림")]
    [Tooltip("문 가운데를 기준으로 좌우로 흔들리는 폭(월드 유닛). 0 이면 똑바로 떨어진다")]
    [SerializeField] private float swayAmplitude = 1.1f;

    [Tooltip("좌우 왕복 횟수(초당). 높을수록 빠르게 흔들려 어려워진다")]
    [SerializeField] private float swayFrequency = 0.85f;

    [Tooltip("재생할 때마다 흔들림의 시작 위상과 방향을 랜덤으로 정한다. 끄면 항상 왼쪽에서 시작")]
    [SerializeField] private bool randomizeSway = true;

    [Header("판정 (문 가운데)")]
    [Tooltip("판정 영역의 좌우 반폭(월드 유닛). 풍선이 이 영역 '안에 완전히' 들어와야 성공한다")]
    [SerializeField] private float hitToleranceX = 1.8f;

    [Tooltip("판정 영역의 위아래 반높이(월드 유닛). 풍선이 이 영역 '안에 완전히' 들어와야 성공한다")]
    [SerializeField] private float hitToleranceY = 2.1f;

    [Tooltip("풍선 스프라이트에서 실제 풍선이 차지하는 비율(가로, 세로). " +
        "이미지 가장자리의 투명 여백을 판정에서 빼기 위한 값이다. 1이면 이미지 전체를 풍선으로 친다")]
    [SerializeField] private Vector2 balloonHitboxScale = new Vector2(0.7f, 0.9f);

    [Tooltip("판정 밖에서 눌렀을 때 즉시 실패시킬지. 끄면 문이 다시 열려 재시도할 수 있다")]
    [SerializeField] private bool failOnMistimedPress = true;

    [Tooltip("헛닫힘 후 문이 다시 열리기까지의 시간(초). failOnMistimedPress 가 꺼져 있을 때만 쓰인다")]
    [SerializeField] private float doorReopenDelay = 0.35f;

    [Header("입력")]
    [Tooltip("문을 닫을 키")]
    [SerializeField] private Key triggerKey = Key.Space;

    [Tooltip("같이 인정할 보조 키. 필요 없으면 None 으로 둔다")]
    [SerializeField] private Key alternateKey = Key.None;

    [Header("문 그림")]
    [Tooltip("문 그림이 열림/닫힘으로 바뀔 SpriteRenderer")]
    [SerializeField] private SpriteRenderer pressRenderer;

    [Tooltip("열린 문. miss_game_assets_open")]
    [SerializeField] private Sprite idleSprite;

    [Tooltip("닫힌 문. Rythmgame2")]
    [SerializeField] private Sprite pressedSprite;

    [Header("문 오브젝트 전환")]
    [Tooltip("열린 문 오브젝트(miss_game_assets_open). 문이 닫히면 비활성화된다")]
    [SerializeField] private GameObject doorOpenObject;

    [Tooltip("닫힌 문 오브젝트(Rythmgame2). 문이 닫히면 활성화된다")]
    [SerializeField] private GameObject doorClosedObject;

    [Header("성공 연출")]
    [Tooltip("풍선이 터지듯 부풀어 오르는 시간(초). 끝나면 풍선이 사라진다")]
    [SerializeField] private float hitPopDuration = 0.15f;

    [Tooltip("사라지기 직전까지 부풀어 오르는 최대 배율")]
    [SerializeField] private float hitPopScale = 1.4f;

    [Tooltip("성공 시 켜줄 연출 오브젝트(터짐 효과 등). 없으면 비워둔다")]
    [SerializeField] private GameObject hitEffect;

    [Tooltip("성공 시 커지면서 흔들릴 Pow! 이미지들. 배치해 둔 위치·크기가 최종 모습이 된다")]
    [SerializeField] private SpriteRenderer[] powImages;

    [Tooltip("Pow! 가 커지는 데 걸리는 시간(초). 이 동안 같이 흔들린다")]
    [SerializeField] private float powGrowDuration = 0.35f;

    [Tooltip("Pow! 가 튀어나오기 시작할 때의 크기 배율. 여기서 제 크기까지 커진다")]
    [SerializeField] private float powStartScale = 0.2f;

    [Tooltip("Pow! 가 커지는 동안 흔들리는 세기(월드 유닛)")]
    [SerializeField] private float powShakeMagnitude = 0.25f;

    [Tooltip("두 번째 Pow! 부터 늦게 터지는 간격(초). 0이면 동시에 터진다")]
    [SerializeField] private float powStagger = 0.1f;

    [Tooltip("성공 시 화면이 흔들리는 시간(초). 0이면 흔들지 않는다")]
    [SerializeField] private float successShakeDuration = 0.35f;

    [Tooltip("성공 시 화면 흔들림 세기(월드 유닛)")]
    [SerializeField] private float successShakeMagnitude = 0.4f;

    [Tooltip("성공 순간 풍선을 문 뒤로 보내 문에 삼켜지는 것처럼 보이게 한다")]
    [SerializeField] private bool hideBalloonBehindDoor = true;

    [Header("실패 연출")]
    [Tooltip("실패 시 풍선이 날아가는 방향. 기본값은 왼쪽 위")]
    [SerializeField] private Vector2 failFlyDirection = new Vector2(-1f, 0.25f);

    [Tooltip("실패 시 풍선이 날아가는 속도(월드 유닛/초). 0이면 그 자리에 멈춘다")]
    [SerializeField] private float failFlySpeed = 14f;

    [Tooltip("날아가면서 점점 빨라지는 가속도(월드 유닛/초²). 슉 하고 빠져나가는 느낌을 준다")]
    [SerializeField] private float failFlyAcceleration = 30f;

    [Tooltip("날아가면서 도는 속도(도/초). 0이면 돌지 않고 그대로 날아간다")]
    [SerializeField] private float failFlySpin = 0f;

    [Tooltip("실패 시 닫힌 문을 보여주는 시간(초). 이 시간이 지나면 화면이 어두워지기 시작한다")]
    [SerializeField] private float failDoorHold = 0.4f;

    [Tooltip("화면이 새까맣게 어두워지는 데 걸리는 시간(초)")]
    [SerializeField] private float failFadeDuration = 0.5f;

    [Tooltip("완전히 어두워진 뒤 실패를 통지하기까지 기다리는 시간(초)")]
    [SerializeField] private float failBlackHold = 0.25f;

    [Tooltip("화면을 덮을 검은 판. 비워두면 카메라 화면을 덮는 검은 판을 자동으로 만들어 쓴다")]
    [SerializeField] private SpriteRenderer blackoutRenderer;

    [Header("이벤트")]
    [Tooltip("판정 성공 시 정확도를 넘긴다. 1 = 문 정중앙, 0 = 판정 영역 가장자리")]
    public AccuracyEvent onJudged;

    public UnityEvent onClear;
    public UnityEvent onFail;

    private Vector3 startPosition;
    private Vector3 goalPosition;
    private float fallSpeed;

    /// <summary>재생 시작 후 흐른 낙하 시간(초). <see cref="fallDuration"/> 이 되는 순간이 문 가운데.</summary>
    private float fallTime;

    /// <summary>좌우 흔들림의 시작 위상(라디안). 매 재생마다 달라져 같은 타이밍이 반복되지 않는다.</summary>
    private float swayPhase;

    /// <summary>헛닫힘 후 문이 다시 열리기까지 남은 시간(초). 0 이면 문은 열려 있다.</summary>
    private float reopenTimer;

    /// <summary>실패 연출이 진행 중인지. 켜져 있는 동안은 입력도 낙하도 멈춘다.</summary>
    private bool failing;

    private Vector3 originalScale = Vector3.one;
    private Quaternion originalRotation = Quaternion.identity;
    private Coroutine hitRoutine;
    private Coroutine failRoutine;
    private Coroutine flyAwayRoutine;

    // Pow! 가 배치돼 있던 원래 위치·크기. 연출이 끝나면 여기로 수렴하고, 리셋 때 되돌린다.
    private Vector3[] powRestPositions;
    private Vector3[] powRestScales;

    // 화면 흔들기 전 카메라 원위치. 카메라는 호스트 씬 공용이라 반드시 되돌려야 한다.
    private Coroutine shakeRoutine;
    private Vector3 cameraRestorePos;
    private bool cameraShaking;

    // 성공 시 풍선을 문 뒤로 보내기 위해 원래 정렬 순서를 기억해 둔다.
    private SpriteRenderer[] balloonRenderers;
    private int[] balloonSortingLayers;
    private int[] balloonSortingOrders;

    // blackoutRenderer 가 비어 있을 때 자동으로 만들어 쓰는 검은 판.
    private SpriteRenderer autoBlackout;
    private Texture2D autoBlackoutTexture;

    private void Awake()
    {
        if (fallingObject != null)
        {
            originalScale = fallingObject.localScale;
            originalRotation = fallingObject.localRotation;
            CacheBalloonSorting();
        }

        CachePowPoses();
    }

    /// <summary>Pow! 들이 배치된 위치·크기를 기억해 둔다. 연출은 이 값을 목표로 삼는다.</summary>
    private void CachePowPoses()
    {
        if (powImages == null)
            return;

        powRestPositions = new Vector3[powImages.Length];
        powRestScales = new Vector3[powImages.Length];

        for (int i = 0; i < powImages.Length; i++)
        {
            if (powImages[i] == null)
                continue;

            powRestPositions[i] = powImages[i].transform.localPosition;
            powRestScales[i] = powImages[i].transform.localScale;
        }
    }

    private void OnDestroy()
    {
        // 자동 생성한 검은 판의 텍스처는 직접 만든 것이라 직접 정리한다.
        if (autoBlackoutTexture != null)
            Destroy(autoBlackoutTexture);
    }

    private void Start()
    {
        // 게임 진행이 아니라 초기 표시만 한다.
        // 풍선은 배치해 둔 자리에 그대로 둔다 — 여기서 출발 지점(화면 위 바깥)으로 옮겨버리면
        // 재생되지 않는 동안 "풍선이 아예 안 보이는" 상태가 된다.
        ApplyDoorClosed(false);

        if (hitEffect != null)
            hitEffect.SetActive(false);
    }

    protected override void OnTimedPlay()
    {
        ResetInternal();

        // 스페이스를 눌러도 문이 안 닫히는 흔한 원인 — 두 방식 모두 비어 있으면 미리 알려준다.
        // 문 전환은 오브젝트 켜고 끄기(doorOpenObject/doorClosedObject) 또는
        // 스프라이트 교체(pressRenderer + idle/pressed) 중 하나만 설정돼 있으면 된다.
        bool objectToggleReady = doorOpenObject != null || doorClosedObject != null;
        bool spriteSwapReady = pressRenderer != null && idleSprite != null && pressedSprite != null;

        if (!objectToggleReady && !spriteSwapReady)
        {
            Debug.LogWarning(
                $"[{name}] 문 전환 설정이 비어 있어 스페이스를 눌러도 문이 바뀌지 않는다. " +
                "Door Open Object / Door Closed Object 를 연결하거나, " +
                "Press Renderer 와 열린 문 / 닫힌 문 스프라이트를 연결하라.", this);
        }

        if (timeLimit < fallDuration)
        {
            Debug.LogWarning(
                $"[{name}] 제한 시간({timeLimit}s)이 낙하 시간({fallDuration}s)보다 짧다. " +
                "풍선이 문에 닿기도 전에 시간이 끝난다.", this);
        }

        // 풍선이 판정 영역보다 크면 아무리 잘 눌러도 "완전히 안에" 들어올 수 없다.
        GetBalloonHitbox(out _, out Vector2 hitboxExtents);
        if (hitboxExtents.x > hitToleranceX || hitboxExtents.y > hitToleranceY)
        {
            Debug.LogWarning(
                $"[{name}] 판정 영역({hitToleranceX} x {hitToleranceY})이 풍선 반크기" +
                $"({hitboxExtents.x:F2} x {hitboxExtents.y:F2})보다 작아 절대 성공할 수 없다. " +
                "Hit Tolerance 를 키우거나 풍선 스케일 / Balloon Hitbox Scale 을 줄여라.", this);
        }
        else if (swayAmplitude > 0f && hitToleranceX - hitboxExtents.x >= swayAmplitude)
        {
            Debug.LogWarning(
                $"[{name}] 가로 여유({hitToleranceX - hitboxExtents.x:F2})가 흔들림 폭({swayAmplitude}) 이상이라 " +
                "아무 때나 눌러도 성공한다.", this);
        }
    }

    protected override void OnTimedStopAndReset()
    {
        if (hitRoutine != null)
        {
            StopCoroutine(hitRoutine);
            hitRoutine = null;
        }

        if (failRoutine != null)
        {
            StopCoroutine(failRoutine);
            failRoutine = null;
        }

        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        if (flyAwayRoutine != null)
        {
            StopCoroutine(flyAwayRoutine);
            flyAwayRoutine = null;
        }

        ResetInternal();
    }

    protected override void OnTimedUpdate()
    {
        // 실패 연출 중에는 풍선도 입력도 그 자리에 멈춘다.
        if (failing)
            return;

        fallTime += Time.deltaTime;
        ApplyFallPosition();

        // 헛닫힘으로 닫아둔 문을 다시 연다.
        if (reopenTimer > 0f)
        {
            reopenTimer -= Time.deltaTime;
            if (reopenTimer <= 0f)
            {
                reopenTimer = 0f;
                ApplyDoorClosed(false);
            }
        }

        if (IsTriggerPressed())
        {
            CloseDoorAndJudge();
            return;
        }

        // 풍선이 판정 영역 아래로 완전히 빠져나갔다 — 더는 들어올 수 없으니 놓친 것.
        if (fallingObject != null)
        {
            GetBalloonHitbox(out Vector3 center, out Vector2 extents);
            if (center.y + extents.y < goalPosition.y - hitToleranceY)
                Miss();
        }
    }

    /// <summary>낙하 시간, 위치, 스케일, 문 그림, 연출 오브젝트를 처음 상태로 되돌린다.</summary>
    private void ResetInternal()
    {
        fallTime = 0f;
        reopenTimer = 0f;
        failing = false;

        // 흔들리던 중에 중단됐다면 카메라를 원위치로 되돌린다(카메라는 호스트 씬 공용이다).
        if (cameraShaking)
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam != null)
                cam.transform.localPosition = cameraRestorePos;

            cameraShaking = false;
        }

        // 흔들림 위상: 랜덤이면 매 판마다 문에 도착하는 순간의 좌우 위치가 달라진다.
        swayPhase = randomizeSway ? UnityEngine.Random.Range(0f, Mathf.PI * 2f) : 0f;

        // 날아가면서 돌았을 수 있으니 자세도 처음으로 되돌린다.
        if (fallingObject != null)
        {
            fallingObject.localRotation = originalRotation;
            fallingObject.localScale = originalScale;
            fallingObject.gameObject.SetActive(true);
        }

        ResolvePoints();
        ApplyFallPosition();

        

        ApplyDoorClosed(false);
        RestoreBalloonSorting();
        ClearBlackout();

        if (hitEffect != null)
            hitEffect.SetActive(false);

        HidePowImages();
    }

    /// <summary>Pow! 들을 숨기고 원래 위치·크기로 되돌린다. 멱등.</summary>
    private void HidePowImages()
    {
        if (powImages == null)
            return;

        for (int i = 0; i < powImages.Length; i++)
        {
            if (powImages[i] == null)
                continue;

            if (powRestPositions != null && i < powRestPositions.Length)
            {
                powImages[i].transform.localPosition = powRestPositions[i];
                powImages[i].transform.localScale = powRestScales[i];
            }

            powImages[i].gameObject.SetActive(false);
        }
    }

    /// <summary>출발/문 지점을 확정하고 낙하 속도를 계산한다. Transform 이 비어 있으면 카메라 화면 기준으로 자동 계산.</summary>
    private void ResolvePoints()
    {
        if (fallingObject == null)
            return;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        float z = fallingObject.position.z;

        // 판정 지점은 targetPoint 우선. 비어 있으면 문 오브젝트 위치를 쓰고, 그것도 없으면 화면 정중앙.
        if (targetPoint != null)
            goalPosition = targetPoint.position;
        else if (pressRenderer != null)
            goalPosition = pressRenderer.transform.position;
        else
            goalPosition = ViewportPoint(cam, 0.5f, 0.5f, z, 0f);

        goalPosition.z = z;

        if (spawnPoint != null)
        {
            startPosition = spawnPoint.position;
        }
        else
        {
            // 자동 계산 시엔 문 바로 위, 화면 높이의 절반 + 여유만큼 위에서 떨어뜨린다.
            // 카메라의 절대 위치가 아니라 "화면 높이"만 쓰므로, MiniGamePlayer 가 원점에서
            // 멀리 떨어진 곳에 인스턴스를 놓고 재생해도 풍선이 문 위에서 제대로 내려온다.
            startPosition = new Vector3(
                goalPosition.x, goalPosition.y + ScreenHalfHeight(cam, z) + spawnMargin, z);
        }

        float distance = Mathf.Abs(goalPosition.y - startPosition.y);
        fallSpeed = fallDuration > 0f ? distance / fallDuration : 0f;
    }

    /// <summary>
    /// 카메라 화면 높이의 절반을 월드 단위로 구한다. 카메라가 없으면 5(기본 orthographic size)로 친다.
    /// 화면 "크기"만 쓰고 위치는 쓰지 않는 것이 핵심이다.
    /// </summary>
    private float ScreenHalfHeight(Camera cam, float z)
    {
        if (cam == null)
            return 5f;

        if (cam.orthographic)
            return cam.orthographicSize;

        float depth = Mathf.Abs(z - cam.transform.position.z);
        Vector3 bottom = cam.ViewportToWorldPoint(new Vector3(0.5f, 0f, depth));
        Vector3 top = cam.ViewportToWorldPoint(new Vector3(0.5f, 1f, depth));

        return Mathf.Abs(top.y - bottom.y) * 0.5f;
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

    /// <summary>
    /// 현재 낙하 시간에 맞는 위치로 풍선을 옮긴다.
    /// 세로는 일정 속도로 내려가고(문을 지나쳐도 계속 내려간다), 가로는 문 가운데를 축으로 사인 곡선을 그린다.
    /// </summary>
    private void ApplyFallPosition()
    {
        if (fallingObject == null)
            return;

        float y = startPosition.y - fallSpeed * fallTime;
        float sway = swayAmplitude * Mathf.Sin(swayPhase + fallTime * swayFrequency * Mathf.PI * 2f);

        fallingObject.position = new Vector3(goalPosition.x + sway, y, startPosition.z);
    }

    /// <summary>이번 프레임에 키를 새로 눌렀는지.</summary>
    private bool IsTriggerPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        if (triggerKey != Key.None && keyboard[triggerKey].wasPressedThisFrame)
            return true;

        return alternateKey != Key.None && keyboard[alternateKey].wasPressedThisFrame;
    }

    /// <summary>
    /// 문을 닫고 그 순간의 풍선 위치로 판정한다.
    /// 판정 영역 안이면 성공, 밖이면 헛닫힘이다.
    /// </summary>
    private void CloseDoorAndJudge()
    {
        ApplyDoorClosed(true);

        if (fallingObject == null)
        {
            Mistimed();
            return;
        }

        // 풍선의 '중심'이 아니라 '전체'가 판정 영역 안에 들어와야 성공이다.
        // 남은 여유(margin)가 0 이상이면 삐져나가지 않고 완전히 들어온 것.
        GetBalloonHitbox(out Vector3 center, out Vector2 extents);

        float dx = Mathf.Abs(center.x - goalPosition.x);
        float dy = Mathf.Abs(center.y - goalPosition.y);

        float marginX = hitToleranceX - (dx + extents.x);
        float marginY = hitToleranceY - (dy + extents.y);

        if (marginX >= 0f && marginY >= 0f)
            Hit(marginX, marginY);
        else
            Mistimed();
    }

    /// <summary>
    /// 판정에 쓸 풍선의 중심과 반크기를 구한다.
    /// 스프라이트 렌더러의 실제 크기에 <see cref="balloonHitboxScale"/> 을 곱해 투명 여백을 덜어낸다.
    /// </summary>
    private void GetBalloonHitbox(out Vector3 center, out Vector2 extents)
    {
        center = fallingObject != null ? fallingObject.position : goalPosition;
        extents = Vector2.zero;

        if (balloonRenderers == null || balloonRenderers.Length == 0)
            return;

        bool found = false;
        Bounds bounds = default;

        foreach (SpriteRenderer renderer in balloonRenderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!found)
            return;

        center = bounds.center;
        extents = new Vector2(
            bounds.extents.x * Mathf.Max(0f, balloonHitboxScale.x),
            bounds.extents.y * Mathf.Max(0f, balloonHitboxScale.y));
    }

    /// <summary>풍선이 판정 영역 안에 완전히 들어온 상태에서 문을 닫았다. 성공 연출을 재생한다.</summary>
    /// <param name="marginX">가로로 남은 여유(월드 유닛). 0이면 딱 걸친 것.</param>
    /// <param name="marginY">세로로 남은 여유(월드 유닛).</param>
    private void Hit(float marginX, float marginY)
    {
        Debug.Log($"[{name}] 클리어! 풍선이 판정 영역 안에 완전히 들어왔다 " +
            $"(남은 여유 가로 {marginX:F2} / 세로 {marginY:F2})", this);

        // 판정은 들어왔나/아닌가 뿐이므로 통지 값은 항상 1로 고정한다.
        onJudged.Invoke(1f);

        // 터뜨린 손맛 — 화면을 짧게 흔든다. 풍선/Pow 연출과 겹쳐서 재생된다.
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam != null && successShakeDuration > 0f)
            shakeRoutine = StartCoroutine(ShakeCameraRoutine(cam));

        // 문이 닫히면서 풍선을 삼키도록 정렬 순서를 문 뒤로 내린다.
        if (hideBalloonBehindDoor)
            SendBalloonBehindDoor();

        // 성공 확정 이후엔 OnTimedUpdate 가 불리지 않으므로 풍선은 이 자리에 멈춘다.
        SucceedWhenTimeUp();
        hitRoutine = StartCoroutine(HitRoutine());
    }

    /// <summary>풍선이 없는 곳에서 문을 닫았다(헛닫힘).</summary>
    private void Mistimed()
    {
        if (failOnMistimedPress)
        {
            BeginFail("헛닫힘 — 판정 영역 밖에서 눌렀다");
            return;
        }

        Debug.Log($"[{name}] 헛닫힘 — 문이 다시 열린다(재시도 가능)", this);

        // 실패시키지 않는 설정이면 문만 잠깐 닫혔다가 다시 열린다.
        reopenTimer = Mathf.Max(doorReopenDelay, Mathf.Epsilon);
    }

    /// <summary>문 가운데를 지나칠 때까지 누르지 못했다.</summary>
    private void Miss()
    {
        BeginFail("놓침 — 풍선이 문을 지나쳐 내려갔다");
    }

    /// <summary>제한 시간을 다 썼다. 즉시 통지하지 않고 실패 연출을 거친다.</summary>
    protected override void OnTimeUp()
    {
        // 이미 실패 연출이 돌고 있으면(연출 도중 시간이 다 찬 경우) 그쪽이 통지를 책임진다.
        if (failing)
            return;

        BeginFail("시간 초과");
    }

    /// <summary>
    /// 실패 연출을 시작한다. 문이 닫힌 채로 잠시 멈췄다가 화면이 까매지고, 그 뒤에 실패로 통지된다.
    /// 연출 중에는 <see cref="failing"/> 로 입력과 낙하를 막는다.
    /// </summary>
    /// <param name="reason">로그에 남길 실패 원인.</param>
    private void BeginFail(string reason)
    {
        if (failing)
            return;

        failing = true;

        Debug.Log($"[{name}] 실패! {reason}", this);

        // 실패한 순간에도 문은 닫힌 그림으로 보여준다.
        ApplyDoorClosed(true);
        onFail.Invoke();

        // 놓친 풍선은 왼쪽으로 날아가 버린다.
        if (fallingObject != null && failFlySpeed > 0f)
            flyAwayRoutine = StartCoroutine(FlyAwayRoutine());

        failRoutine = StartCoroutine(FailRoutine());
    }

    /// <summary>
    /// 실패한 풍선이 화면 밖으로 날아간다. 실패 연출(문 유지 → 암전) 동안 계속 움직이고,
    /// 화면 밖으로 충분히 벗어나면 스스로 멈춘다.
    /// </summary>
    private IEnumerator FlyAwayRoutine()
    {
        Vector2 direction = failFlyDirection.sqrMagnitude > 0.0001f
            ? failFlyDirection.normalized
            : Vector2.left;

        // 실패 연출이 다 끝날 때까지는 확실히 날아가도록 넉넉히 잡는다.
        float duration = failDoorHold + failFadeDuration + failBlackHold + 0.5f;
        float time = 0f;
        float speed = failFlySpeed;

        while (time < duration && fallingObject != null)
        {
            float dt = Time.deltaTime;
            time += dt;

            // 갈수록 빨라져서 슉 하고 화면 밖으로 빠져나간다.
            speed += failFlyAcceleration * dt;
            fallingObject.position += (Vector3)(direction * (speed * dt));

            if (!Mathf.Approximately(failFlySpin, 0f))
                fallingObject.Rotate(0f, 0f, failFlySpin * dt);

            yield return null;
        }

        flyAwayRoutine = null;
    }

    /// <summary>실패 연출: 닫힌 문을 잠깐 보여주고 화면을 검게 덮은 뒤 실패를 통지한다.</summary>
    private IEnumerator FailRoutine()
    {
        if (failDoorHold > 0f)
            yield return new WaitForSeconds(failDoorHold);

        SpriteRenderer blackout = ResolveBlackout();
        if (blackout != null)
        {
            blackout.gameObject.SetActive(true);

            Color color = blackout.color;
            float time = 0f;

            while (time < failFadeDuration)
            {
                time += Time.deltaTime;
                color.a = failFadeDuration > 0f ? Mathf.Clamp01(time / failFadeDuration) : 1f;
                blackout.color = color;

                yield return null;
            }

            color.a = 1f;
            blackout.color = color;
        }

        if (failBlackHold > 0f)
            yield return new WaitForSeconds(failBlackHold);

        failRoutine = null;

        // 시간 초과로 이미 결과가 잠긴 경우 FailImmediately 는 무시되므로 직접 통지한다.
        ReportFinished(false);
    }

    /// <summary>성공 연출: 문에 낀 풍선이 부풀어 오르다가 터지듯 사라지고, 터짐 효과를 켠다.</summary>
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
        {
            // [버그 추적용 임시 로깅] 이 순간 철의여인(닫힌 문)도 같이 사라진다면
            // 문이 풍선의 자식이거나 정렬 동률로 배경 뒤에 그려진 것이다.
            Debug.Log($"[TimingDrop][진단] 풍선 비활성화. 닫힌 문 activeInHierarchy=" +
                $"{(doorClosedObject != null ? doorClosedObject.activeInHierarchy.ToString() : "참조 없음")}", this);

            fallingObject.gameObject.SetActive(false);
        }

        if (hitEffect != null)
            hitEffect.SetActive(true);

        // Pow! 들이 작게 튀어나와 커지면서 부르르 흔들린다.
        yield return PopPowRoutine();

        hitRoutine = null;
        onClear.Invoke();
    }

    /// <summary>Pow! 이미지들을 순서대로 터뜨린다. 각자 작게 시작해 제 크기까지 커지며 흔들린다.</summary>
    private IEnumerator PopPowRoutine()
    {
        if (powImages == null || powImages.Length == 0)
            yield break;

        for (int i = 0; i < powImages.Length; i++)
        {
            if (powImages[i] == null)
                continue;

            StartCoroutine(PopOnePowRoutine(i));

            if (powStagger > 0f && i < powImages.Length - 1)
                yield return new WaitForSeconds(powStagger);
        }

        // 마지막으로 터진 것까지 다 커질 때까지 기다린다.
        if (powGrowDuration > 0f)
            yield return new WaitForSeconds(powGrowDuration);
    }

    /// <summary>Pow! 하나를 작게 띄워 제 크기까지 키우면서 흔든다.</summary>
    private IEnumerator PopOnePowRoutine(int index)
    {
        SpriteRenderer pow = powImages[index];
        Transform t = pow.transform;

        Vector3 restPos = powRestPositions != null && index < powRestPositions.Length
            ? powRestPositions[index]
            : t.localPosition;
        Vector3 restScale = powRestScales != null && index < powRestScales.Length
            ? powRestScales[index]
            : t.localScale;

        t.localPosition = restPos;
        t.localScale = restScale * powStartScale;
        pow.gameObject.SetActive(true);

        float time = 0f;
        while (time < powGrowDuration)
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(powGrowDuration > 0f ? time / powGrowDuration : 1f);

            // 커지는 건 끝에서 살짝 넘쳤다 수렴하고, 흔들림은 갈수록 잦아든다.
            t.localScale = Vector3.LerpUnclamped(restScale * powStartScale, restScale, EaseOutBack(k));

            float damper = 1f - k;
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            t.localPosition = restPos + new Vector3(
                Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (powShakeMagnitude * damper);

            yield return null;
        }

        t.localPosition = restPos;
        t.localScale = restScale;
    }

    /// <summary>카메라를 흔들었다가 원위치로 되돌린다. 시간이 지날수록 잦아든다.</summary>
    private IEnumerator ShakeCameraRoutine(Camera cam)
    {
        Transform t = cam.transform;
        cameraRestorePos = t.localPosition;
        cameraShaking = true;

        // [버그 추적용 임시 로깅] CameraEffectManager(실패 패널티 셰이크 등)가 카메라를 움직이는 중에
        // 이 값을 캐시하면 "오프셋 걸린 위치"를 원위치로 복원해 카메라가 영구히 어긋난다.
        // 이후 미니게임(삽 등)의 스프라이트가 화면 밖으로 밀려 안 보일 수 있는 의심 지점.
        Debug.Log($"[TimingDrop][진단] 카메라 셰이크 시작: 복원 기준 localPos={cameraRestorePos} " +
            $"(CameraEffectManager 활성 여부 확인 필요)", this);

        float time = 0f;
        while (time < successShakeDuration)
        {
            time += Time.deltaTime;
            float damper = 1f - Mathf.Clamp01(time / successShakeDuration);
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            t.localPosition = cameraRestorePos + new Vector3(
                Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (successShakeMagnitude * damper);

            yield return null;
        }

        t.localPosition = cameraRestorePos;
        cameraShaking = false;
        shakeRoutine = null;

        // [버그 추적용 임시 로깅] 셰이크 종료 시 복원된 위치. 시작 로그와 값이 다르거나
        // 이후 다른 게임 시작 로그의 카메라 위치와 어긋나면 카메라 복원 경합이 원인이다.
        Debug.Log($"[TimingDrop][진단] 카메라 셰이크 종료: localPos={t.localPosition} 로 복원", this);
    }

    /// <summary>끝에서 살짝 넘쳤다가 제자리로 돌아오는 이징(통통 튀는 느낌).</summary>
    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    /// <summary>
    /// 문 그림을 적용한다. 닫으면 <see cref="pressedSprite"/>, 열면 <see cref="idleSprite"/>.
    /// 한 번 닫은 문은 성공/실패가 확정되면 그대로 닫힌 채 남는다.
    /// </summary>
    private void ApplyDoorClosed(bool closed)
    {
        // 오브젝트를 통째로 켜고 끄는 방식. 스페이스를 누르면 열린 문이 꺼지고 닫힌 문이 켜진다.
        if (doorOpenObject != null)
            doorOpenObject.SetActive(!closed);

        if (doorClosedObject != null)
            doorClosedObject.SetActive(closed);

        if (pressRenderer == null)
            return;

        Sprite target = closed ? pressedSprite : idleSprite;

        // 같은 그림이면 굳이 다시 대입하지 않는다.
        if (target != null && pressRenderer.sprite != target)
            pressRenderer.sprite = target;
    }

    /// <summary>풍선(과 그 자식)의 원래 정렬 순서를 기억해 둔다. 문 뒤로 보냈다가 되돌리기 위한 것.</summary>
    private void CacheBalloonSorting()
    {
        balloonRenderers = fallingObject.GetComponentsInChildren<SpriteRenderer>(true);
        balloonSortingLayers = new int[balloonRenderers.Length];
        balloonSortingOrders = new int[balloonRenderers.Length];

        for (int i = 0; i < balloonRenderers.Length; i++)
        {
            balloonSortingLayers[i] = balloonRenderers[i].sortingLayerID;
            balloonSortingOrders[i] = balloonRenderers[i].sortingOrder;
        }
    }

    /// <summary>풍선을 문보다 뒤에 그리게 해서 닫히는 문에 삼켜지는 것처럼 보이게 한다.</summary>
    private void SendBalloonBehindDoor()
    {
        if (balloonRenderers == null || pressRenderer == null)
            return;

        // [버그 추적용 임시 로깅] "풍선을 터뜨릴 때 철의여인(닫힌 문)이 함께 사라지는" 문제 진단.
        // 의심: 풍선을 pressRenderer.order - 1 로 내리는데, 배경이 그보다 높거나 같은 order 면
        //       풍선이 배경 뒤로 사라지고, 닫힌 문(order 0)과 배경(order 0)이 동률이면
        //       문 자체도 그리기 순서 미정의로 배경 뒤로 갈 수 있다.
        Debug.Log($"[TimingDrop][진단] SendBalloonBehindDoor: pressRenderer='{pressRenderer.name}' " +
            $"layer={pressRenderer.sortingLayerName}, order={pressRenderer.sortingOrder} → " +
            $"풍선 목표 order={pressRenderer.sortingOrder - 1}", this);

        foreach (SpriteRenderer renderer in balloonRenderers)
        {
            if (renderer == null)
                continue;

            // 문과 같은 정렬 레이어로 맞춘 뒤 한 칸 뒤로 보낸다. 레이어가 다르면 순서만으론 안 가려진다.
            renderer.sortingLayerID = pressRenderer.sortingLayerID;
            renderer.sortingOrder = pressRenderer.sortingOrder - 1;
        }

        LogDoorSortingTies();
    }

    /// <summary>
    /// [버그 추적용 임시 로깅] 닫힌 문(철의여인)과 정렬 순서가 동률인 렌더러를 찾아 경고한다.
    /// 동률이면 그리기 순서가 미정의라 문이 배경 뒤로 그려져 "사라진 것처럼" 보일 수 있다.
    /// </summary>
    private void LogDoorSortingTies()
    {
        if (doorClosedObject == null)
            return;

        SpriteRenderer[] doorRenderers = doorClosedObject.GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer other in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (other == null || other.transform.IsChildOf(doorClosedObject.transform))
                continue;

            foreach (SpriteRenderer door in doorRenderers)
            {
                Debug.Log($"[TimingDrop][진단] 렌더러 '{other.name}' layer={other.sortingLayerName}, " +
                    $"order={other.sortingOrder}, z={other.transform.position.z:F3}, " +
                    $"active={other.gameObject.activeInHierarchy}", other);

                if (other.sortingLayerID == door.sortingLayerID && other.sortingOrder == door.sortingOrder)
                {
                    Debug.LogWarning($"[TimingDrop][진단] 정렬 순서 동률! 닫힌 문 '{door.name}'(order {door.sortingOrder}, " +
                        $"z {door.transform.position.z:F3}) vs '{other.name}'(order {other.sortingOrder}, " +
                        $"z {other.transform.position.z:F3}) — 문이 배경 뒤로 그려져 사라져 보일 수 있다.", door);
                }

                break; // 렌더러 나열 로그는 문 렌더러마다 반복하지 않는다.
            }
        }
    }

    /// <summary>문 뒤로 보냈던 풍선의 정렬 순서를 원래대로 되돌린다.</summary>
    private void RestoreBalloonSorting()
    {
        if (balloonRenderers == null)
            return;

        for (int i = 0; i < balloonRenderers.Length; i++)
        {
            if (balloonRenderers[i] == null)
                continue;

            balloonRenderers[i].sortingLayerID = balloonSortingLayers[i];
            balloonRenderers[i].sortingOrder = balloonSortingOrders[i];
        }
    }

    /// <summary>
    /// 화면을 덮을 검은 판을 얻는다. 인스펙터에 넣어둔 게 없으면 카메라 화면 크기에 맞춰 하나 만들어 쓴다.
    /// 자동 생성한 판은 이 미니게임의 자식이라 프리팹과 함께 정리된다.
    /// </summary>
    private SpriteRenderer ResolveBlackout()
    {
        if (blackoutRenderer != null)
            return blackoutRenderer;

        if (autoBlackout == null)
        {
            autoBlackoutTexture = new Texture2D(1, 1);
            autoBlackoutTexture.SetPixel(0, 0, Color.white);
            autoBlackoutTexture.Apply();

            var holder = new GameObject("[Blackout]");
            holder.transform.SetParent(transform, false);

            autoBlackout = holder.AddComponent<SpriteRenderer>();
            autoBlackout.sprite = Sprite.Create(
                autoBlackoutTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

            // 무엇보다 앞에 그려야 화면 전체가 까매진다.
            autoBlackout.sortingOrder = short.MaxValue;
        }

        FitToScreen(autoBlackout.transform);

        autoBlackout.color = new Color(0f, 0f, 0f, 0f);
        return autoBlackout;
    }

    /// <summary>자동 생성한 검은 판을 카메라 화면 전체를 덮도록 놓고 크기를 맞춘다.</summary>
    private void FitToScreen(Transform target)
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
            return;

        // 카메라 바로 앞(near clip 바깥)에 두고 화면 네 귀퉁이 크기로 늘린다.
        float depth = cam.nearClipPlane + 1f;
        Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(1f, 1f, depth));

        target.position = (bottomLeft + topRight) * 0.5f;
        target.rotation = cam.transform.rotation;
        target.localScale = new Vector3(
            Mathf.Abs(topRight.x - bottomLeft.x), Mathf.Abs(topRight.y - bottomLeft.y), 1f);
    }

    /// <summary>검은 판을 완전히 투명하게 되돌린다. 다음 재생 때 화면이 까만 채로 시작하지 않게 한다.</summary>
    private void ClearBlackout()
    {
        SpriteRenderer blackout = blackoutRenderer != null ? blackoutRenderer : autoBlackout;
        if (blackout == null)
            return;

        Color color = blackout.color;
        color.a = 0f;
        blackout.color = color;
    }

    /// <summary>인스펙터에서 선택했을 때 문 가운데 판정 영역과 흔들림 폭을 그려 튜닝을 돕는다.</summary>
    private void OnDrawGizmosSelected()
    {
        if (fallingObject == null)
            return;

        ResolvePoints();

        // 판정 영역(초록) — 풍선 상자가 이 안에 완전히 들어와야 성공.
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(goalPosition, new Vector3(hitToleranceX * 2f, hitToleranceY * 2f, 0f));

        // 풍선 판정 상자(하늘색) — 초록 상자 안에 통째로 들어가야 한다.
        if (balloonRenderers == null || balloonRenderers.Length == 0)
            CacheBalloonSorting();

        GetBalloonHitbox(out Vector3 balloonCenter, out Vector2 balloonExtents);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(balloonCenter, new Vector3(balloonExtents.x * 2f, balloonExtents.y * 2f, 0f));

        // 흔들림 폭(노랑) — 풍선이 좌우로 오가는 범위.
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            new Vector3(goalPosition.x - swayAmplitude, goalPosition.y, goalPosition.z),
            new Vector3(goalPosition.x + swayAmplitude, goalPosition.y, goalPosition.z));
    }
}
