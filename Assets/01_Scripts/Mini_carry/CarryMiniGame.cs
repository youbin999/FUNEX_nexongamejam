using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 태양 키우기 미니게임.
/// 스페이스바를 연타할 때마다 태양이 점점 커지고 기온이 올라간다.
///
/// - 누를 때마다 진행도(0~1)가 올라가고, 태양은 그 진행도를 따라 커진다.
/// - <see cref="hotCount"/> 번째 타에서 인물이 더워하는 모습으로 바뀐다.
/// - 제한 시간 안에 <see cref="clearCount"/> 번을 채우면 태양이 사라지고
///   인물이 최종 모습으로 바뀌면서 <see cref="clearObjects"/> 가 나타난다.
///   와리오웨어 규칙대로 즉시 끝나지 않고 제한 시간이 다 찰 때까지 결과를 보여준다.
/// - 못 채우면 시간 초과로 실패한다.
///
/// 프리팹으로 만들어 <see cref="MiniGamePlayer"/> 가 재생한다.
/// </summary>
public class CarryMiniGame : TimedMiniGame
{
    // 인스펙터에 확실히 노출되도록 제네릭 UnityEvent 는 구체 타입으로 선언한다.
    [Serializable] public class MashEvent : UnityEvent<int> { }
    [Serializable] public class RatioEvent : UnityEvent<float> { }

    private enum State
    {
        Idle,
        Playing,
        Cleared,
        Failed,
    }

    [Header("규칙")]
    [Tooltip("클리어에 필요한 연타 횟수")]
    [SerializeField] private int clearCount = 14;

    [Tooltip("인물이 더워하는 모습으로 바뀌는 연타 횟수. 클리어 횟수보다 작아야 한다")]
    [SerializeField] private int hotCount = 7;

    [Header("태양")]
    [Tooltip("클리어하면 사라지는 태양. 크기 연출은 FireGrowth 를 붙여 onProgressChanged 에 연결한다")]
    [SerializeField] private GameObject sunObject;

    [Header("인물")]
    [Tooltip("단계에 따라 그림이 바뀌는 인물")]
    [SerializeField] private SpriteRenderer personRenderer;

    [Tooltip("평상시 모습")]
    [SerializeField] private Sprite normalSprite;

    [Tooltip("더워하는 모습. hotCount 번째 타에서 바뀐다")]
    [SerializeField] private Sprite hotSprite;

    [Tooltip("클리어했을 때의 모습")]
    [SerializeField] private Sprite clearSprite;

    [Header("성공 시 등장")]
    [Tooltip("평소에는 꺼져 있다가 클리어하면 켜지는 오브젝트들")]
    [SerializeField] private GameObject[] clearObjects;

    [Header("실패 연출")]
    [Tooltip("태양이 다 꺼질 때까지 기다리는 시간(초). 태양의 FireGrowth 쪽 Extinguish Time 과 맞춰 둔다")]
    [SerializeField] private float failSunOutDuration = 0.35f;

    [Tooltip("인물이 돌아선 뒤 걷기 시작할 때까지 기다리는 시간(초)")]
    [SerializeField] private float failTurnDelay = 0.2f;

    [Tooltip("걸어 나가는 속도(초당 월드 단위)")]
    [SerializeField] private float failWalkSpeed = 4f;

    [Tooltip("화면 왼쪽 끝을 이만큼 더 지나야 완전히 나간 것으로 본다")]
    [SerializeField] private float failExitMargin = 1f;

    [Tooltip("걸을 때 위아래로 통통 튀는 높이. 0이면 미끄러지듯 이동한다")]
    [SerializeField] private float failBobHeight = 0.15f;

    [Tooltip("통통 튀는 빠르기. 걸음 수라고 보면 된다")]
    [SerializeField] private float failBobFrequency = 6f;

    [Tooltip("화면 왼쪽 끝을 계산할 카메라. 비어 있으면 Camera.main 을 쓴다")]
    [SerializeField] private Camera targetCamera;

    [Header("이벤트")]
    [Tooltip("누를 때마다 현재 횟수를 넘긴다")]
    public MashEvent onMash;

    [Tooltip("0에서 1로 차오르는 진행도. 태양의 FireGrowth.SetRatio 에 연결한다")]
    public RatioEvent onProgressChanged;

    [Tooltip("인물이 더워지기 시작하는 순간. 효과음 등을 연결한다")]
    public UnityEvent onHot;

    [Tooltip("재생을 시작할 때 발화한다. 구름의 Drop(), 연출 컴포넌트의 ResetPose() 등을 연결한다")]
    public UnityEvent onReset;

    public UnityEvent onClear;
    public UnityEvent onFail;

    private int mashCount;
    private State state = State.Idle;

    // 실패 연출 도중인지. 연출이 끝날 때 ReportFinished 를 부르는 책임도 이쪽이 진다.
    private bool failing;
    private Coroutine failRoutine;

    // 인물의 처음 자세. 실패 연출로 뒤집고 걸어 나간 뒤 여기로 되돌린다.
    private Vector3 personRestPosition;
    private bool personRestFlipX;

    // 폴링(wasPressedThisFrame)은 한 프레임에 두 번 눌러도 한 번으로 먹는다.
    // 연타 게임이라 입력 이벤트를 직접 받아서 하나도 놓치지 않게 한다.
    private InputAction mashAction;

    /// <summary>지금까지 누른 횟수.</summary>
    public int MashCount => mashCount;

    /// <summary>0에서 1로 차오르는 진행도.</summary>
    public float Progress => clearCount > 0 ? Mathf.Clamp01((float)mashCount / clearCount) : 0f;

    /// <summary>연타 입력 액션을 만들고 카메라·인물의 처음 자세를 기억해 둔다.</summary>
    private void Awake()
    {
        mashAction = new InputAction("Mash", InputActionType.Button, "<Keyboard>/space");
        mashAction.performed += OnMashPerformed;

        if (targetCamera == null)
            targetCamera = Camera.main;

        // 실패 연출이 인물을 뒤집고 옮기므로 처음 자세를 기억해 둔다.
        if (personRenderer != null)
        {
            personRestPosition = personRenderer.transform.localPosition;
            personRestFlipX = personRenderer.flipX;
        }
    }

    /// <summary>
    /// 게임 설명 화면이 떠 있는 동안에도 첫 모습이 제대로 보이도록 화면만 초기 상태로 잡아 둔다.
    /// 게임을 시작하지는 않는다 — 입력은 <see cref="OnTimedPlay"/> 에서 열린다.
    /// Start 가 아니라 OnEnable 인 이유: <see cref="MiniGamePlayer"/> 가 인스턴스를 껐다 켜서 재사용하므로
    /// Start 로 잡으면 두 번째 판부터 지난 판의 결과 화면이 그대로 남는다.
    /// </summary>
    private void OnEnable()
    {
        ResetVisuals();
    }

    /// <summary>연타 입력 액션을 닫는다.</summary>
    private void OnDisable()
    {
        mashAction.Disable();
    }

    /// <summary>입력 액션 구독을 풀고 폐기한다. Awake 가 안 돌았을 수도 있어 먼저 확인한다.</summary>
    private void OnDestroy()
    {
        // 프리로드 풀에서 한 번도 활성화되지 않은 채 파괴되면 Awake 가 안 돌았을 수 있다.
        if (mashAction == null)
            return;

        mashAction.performed -= OnMashPerformed;
        mashAction.Dispose();
    }

    // 한 프레임에 입력이 여러 번 들어오면 그만큼 여러 번 호출된다.
    private void OnMashPerformed(InputAction.CallbackContext context)
    {
        Mash();
    }

    /// <summary>입력 없이 한 번 누르게 하고 싶을 때 직접 호출해도 된다.</summary>
    public void Mash()
    {
        if (state != State.Playing)
            return;

        mashCount++;
        onMash.Invoke(mashCount);
        onProgressChanged.Invoke(Progress);

        // 딱 그 횟수에서만 한 번 바꾼다. >= 로 비교하면 이후 매 타마다 다시 발화한다.
        if (mashCount == hotCount)
        {
            ApplyPersonSprite(hotSprite);
            onHot.Invoke();
        }

        if (mashCount >= clearCount)
            Clear();
    }

    /// <summary>매 프레임 처리할 일이 없다. 입력은 <see cref="mashAction"/> 콜백에서 받는다.</summary>
    protected override void OnTimedUpdate()
    {
    }

    /// <summary>게임을 시작한다. 상태와 화면을 초기화하고 Playing 으로 전환한다.</summary>
    protected override void OnTimedPlay()
    {
        WarnIfMisconfigured();

        ResetInternal();

        // 지난 판의 연출은 여기서 지운다. 종료 직후가 아니라 다음 판을 시작할 때 지워야 결과를 볼 수 있다.
        ResetVisuals();
        onReset.Invoke();

        state = State.Playing;
        mashAction.Enable();
    }

    /// <summary>게임을 강제 중단하고 초기 상태(Idle)로 되돌린다.</summary>
    protected override void OnTimedStopAndReset()
    {
        mashAction.Disable();

        // 화면은 건드리지 않는다. 결과 화면이 페이드아웃 전까지 남아 있어야 한다.
        ResetInternal();
        state = State.Idle;
    }

    /// <summary>
    /// 제한 시간을 다 써서 실패로 확정될 때 호출된다.
    /// 여기서 바로 끝내지 않고 실패 연출을 재생한 뒤 <see cref="FailRoutine"/> 이 통지한다.
    /// </summary>
    protected override void OnTimeUp()
    {
        if (failing)
            return;

        state = State.Failed;
        failing = true;
        mashAction.Disable();

        // 태양이 꺼지는 연출은 onFail 에 연결한 FireGrowth.Extinguish 가 맡는다.
        onFail.Invoke();

        // base.OnTimeUp() 을 부르지 않는다. 연출이 끝나야 결과를 통지한다.
        failRoutine = StartCoroutine(FailRoutine());
    }

    /// <summary>
    /// 실패 연출: 태양이 다 꺼지면 인물이 좌우로 돌아선 뒤 왼쪽으로 걸어 화면 밖으로 나간다.
    /// 다 나가면 실패를 통지한다.
    /// </summary>
    private IEnumerator FailRoutine()
    {
        if (failSunOutDuration > 0f)
            yield return new WaitForSeconds(failSunOutDuration);

        if (personRenderer != null)
        {
            // 돌아선다.
            personRenderer.flipX = !personRestFlipX;

            if (failTurnDelay > 0f)
                yield return new WaitForSeconds(failTurnDelay);

            yield return WalkOutRoutine();
        }

        failRoutine = null;

        // 시간 초과로 결과가 이미 잠겨 있어 FailImmediately 는 무시된다. 직접 통지한다.
        ReportFinished(false);
    }

    /// <summary>인물이 화면 왼쪽 밖으로 완전히 사라질 때까지 걸어간다.</summary>
    private IEnumerator WalkOutRoutine()
    {
        Transform person = personRenderer.transform;

        // 속도가 0 이하면 영영 못 나가므로 최소값을 둔다.
        float speed = Mathf.Max(failWalkSpeed, 0.01f);

        // 스프라이트 오른쪽 끝까지 화면 밖으로 빠져야 완전히 사라진 것이다.
        float exitX = ScreenLeftX(person.position.z) - personRenderer.bounds.extents.x - failExitMargin;

        float baseY = person.position.y;
        float walked = 0f;

        while (person.position.x > exitX)
        {
            float step = speed * Time.deltaTime;
            walked += step;

            Vector3 position = person.position;
            position.x -= step;

            // 걸음마다 위아래로 통통 튀게 해서 걸어가는 것처럼 보이게 한다.
            position.y = baseY + Mathf.Abs(Mathf.Sin(walked * failBobFrequency)) * failBobHeight;
            person.position = position;

            yield return null;
        }
    }

    /// <summary>
    /// 화면 왼쪽 끝의 월드 x 좌표를 구한다.
    /// 카메라를 못 찾으면 인물 자리에서 넉넉히 왼쪽인 것으로 친다.
    /// </summary>
    private float ScreenLeftX(float z)
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
            return (personRenderer != null ? personRenderer.transform.position.x : 0f) - 10f;

        float depth = Mathf.Abs(z - cam.transform.position.z);
        return cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, depth)).x;
    }

    /// <summary>클리어 처리. 태양을 지우고 인물과 소품을 결과 모습으로 바꾼다.</summary>
    private void Clear()
    {
        // 여기는 mashAction 의 콜백 안이라 Disable() 을 부르지 않는다.
        // 상태가 Cleared 로 바뀌면 Mash() 가 바로 빠져나가므로 그것으로 충분하다.
        state = State.Cleared;

        if (sunObject != null)
            sunObject.SetActive(false);

        ApplyPersonSprite(clearSprite);
        SetClearObjectsActive(true);

        onClear.Invoke();

        // 즉시 끝내지 않는다. 입력만 잠근 채 제한 시간이 다 찰 때까지 결과를 보여준다.
        SucceedWhenTimeUp();
    }

    /// <summary>횟수와 실패 연출 상태를 처음으로 되돌린다.</summary>
    private void ResetInternal()
    {
        mashCount = 0;
        failing = false;

        if (failRoutine != null)
        {
            StopCoroutine(failRoutine);
            failRoutine = null;
        }
    }

    /// <summary>태양과 인물, 클리어 소품을 시작 상태로 되돌린다.</summary>
    private void ResetVisuals()
    {
        if (sunObject != null)
            sunObject.SetActive(true);

        ApplyPersonSprite(normalSprite);
        SetClearObjectsActive(false);

        // 실패 연출로 뒤집히고 걸어 나간 인물을 제자리로 되돌린다.
        if (personRenderer != null)
        {
            personRenderer.flipX = personRestFlipX;
            personRenderer.transform.localPosition = personRestPosition;
        }

        onProgressChanged.Invoke(0f);
    }

    /// <summary>클리어 연출용 오브젝트들을 한꺼번에 켜거나 끈다.</summary>
    private void SetClearObjectsActive(bool value)
    {
        if (clearObjects == null)
            return;

        foreach (GameObject clearObject in clearObjects)
        {
            if (clearObject != null)
                clearObject.SetActive(value);
        }
    }

    /// <summary>인물 그림을 바꾼다. 같은 그림이면 굳이 다시 대입하지 않는다.</summary>
    private void ApplyPersonSprite(Sprite sprite)
    {
        if (personRenderer == null || sprite == null)
            return;

        if (personRenderer.sprite != sprite)
            personRenderer.sprite = sprite;
    }

    /// <summary>인스펙터 배선이 빠졌거나 값이 어긋났을 때 재생 시작 시점에 알려 준다.</summary>
    private void WarnIfMisconfigured()
    {
        if (personRenderer == null)
            Debug.LogWarning($"[{name}] Person Renderer 가 비어 있어 인물 그림이 바뀌지 않는다.", this);
        else if (normalSprite == null || hotSprite == null || clearSprite == null)
            Debug.LogWarning($"[{name}] Normal / Hot / Clear Sprite 중 비어 있는 게 있어 그 단계는 그림이 그대로 남는다.", this);

        if (sunObject == null)
            Debug.LogWarning($"[{name}] Sun Object 가 비어 있어 클리어해도 태양이 사라지지 않는다.", this);

        if (clearCount <= 0)
            Debug.LogWarning($"[{name}] Clear Count({clearCount})가 0 이하라 클리어할 수 없다.", this);

        if (hotCount <= 0 || hotCount >= clearCount)
            Debug.LogWarning($"[{name}] Hot Count({hotCount})가 1 ~ Clear Count-1({clearCount - 1}) 범위를 벗어나 더워지는 연출이 보이지 않는다.", this);
    }
}
