using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 잡아라! 쥐 미니게임.
/// WASD(방향키)로 손을 움직여 위에서 내려오는 쥐를 잡는다. 손 판정 범위에 닿으면 바로 잡히고,
/// 세 마리를 다 잡으면 클리어. 한 마리라도 아래 곡식 자루 선까지 내려가면 즉시 실패한다.
///
/// 좌표 계산은 전부 <b>이 프리팹 루트의 로컬 공간</b>에서 한다. 손과 쥐가 계층 어디에 매달려 있든
/// (예: 쥐들이 Rats 그룹 밑에 있어도) 인스펙터 값의 의미가 달라지지 않게 하려는 것이다.
/// </summary>
public sealed class RatCatchMiniGame : TimedMiniGame
{
    private enum State
    {
        Idle,
        Playing,
        Cleared,
        Failed,
    }

    /// <summary>쥐 한 마리의 상태.</summary>
    private enum RatState
    {
        /// <summary>아직 등장 전.</summary>
        Waiting,

        /// <summary>내려오는 중.</summary>
        Falling,

        /// <summary>잡혔다.</summary>
        Caught,
    }

    [Header("구성")]
    [Tooltip("플레이어가 움직이는 손. 비워두면 자식에서 'Hand' 를 찾는다")]
    [SerializeField] private Transform hand;

    [Tooltip("내려올 쥐들. 비워두면 자식 'Rats' 의 자식들을 순서대로 쓴다")]
    [SerializeField] private Transform[] rats = new Transform[3];

    [Header("조작")]
    [Tooltip("손 이동 속도(초당 유닛)")]
    [SerializeField] private float moveSpeed = 9f;

    [Tooltip("손이 움직일 수 있는 범위(루트 기준 반값). x=좌우, y=상하")]
    [SerializeField] private Vector2 handAreaHalfSize = new Vector2(7f, 3.4f);

    [Tooltip("손 중심에서 이 거리 안에 쥐가 들어오면 잡는다")]
    [Min(0f)]
    [SerializeField] private float catchRadius = 1.1f;

    [Header("쥐")]
    [Tooltip("쥐가 한 마리씩 나오는 간격(초). 0이면 세 마리가 동시에 나온다")]
    [Min(0f)]
    [SerializeField] private float spawnInterval = 0.7f;

    [Tooltip("낙하 속도 범위(초당 유닛). 마리마다 이 사이에서 무작위로 정해진다")]
    [SerializeField] private Vector2 fallSpeedRange = new Vector2(3.2f, 4.6f);

    [Tooltip("쥐가 나타나는 좌우 범위(루트 기준 로컬 X)")]
    [SerializeField] private Vector2 spawnXRange = new Vector2(-6.5f, 6.5f);

    [Tooltip("쥐가 나타나는 높이(루트 기준 로컬 Y). 화면 위쪽 바깥으로 잡는다")]
    [SerializeField] private float spawnY = 5.5f;

    [Tooltip("곡식 자루 윗면 높이(루트 기준 로컬 Y). 쥐가 여기까지 내려오면 실패다")]
    [SerializeField] private float failLineY = -3.6f;

    [Tooltip("잡힌 쥐가 오그라들며 사라지는 시간(초)")]
    [Min(0f)]
    [SerializeField] private float catchAnimDuration = 0.18f;

    [Header("이벤트")]
    [Tooltip("쥐를 한 마리 잡을 때마다 발화. 잡는 소리를 연결한다")]
    public UnityEvent onRatCaught;

    [Tooltip("세 마리를 다 잡았을 때 발화. 성공 소리를 연결한다")]
    public UnityEvent onClear;

    [Tooltip("쥐를 놓치거나 시간이 다 됐을 때 발화. 실패 소리를 연결한다")]
    public UnityEvent onFail;

    private RatState[] ratStates;
    private float[] fallSpeeds;
    private Vector3[] ratStartScales;
    private Vector3[] ratStartPositions;

    private Vector3 handStart;
    private int spawnedCount;
    private int caughtCount;
    private float spawnTimer;
    private State state = State.Idle;

    private void Awake()
    {
        // 계약 1.3 — 여기서는 참조와 초기 위치를 캐시만 한다. 게임은 Play() 로만 시작한다.
        if (hand == null)
            hand = transform.Find("Hand");

        if (rats == null || rats.Length == 0 || rats[0] == null)
            CollectRatsFromChildren();

        CacheStarts();
    }

    protected override void OnTimedPlay()
    {
        WarnIfNotWired();
        ResetObjects();
        state = State.Playing;
    }

    protected override void OnTimedStopAndReset()
    {
        StopAllCoroutines();
        ResetObjects();
        state = State.Idle;
    }

    protected override void OnTimedUpdate()
    {
        if (state != State.Playing)
            return;

        MoveHand();
        UpdateSpawn();
        UpdateRats();
    }

    /// <summary>
    /// 시간 초과 실패. 성공이 확정된 뒤에는 프레임워크가 이 훅을 부르지 않으므로
    /// 여기서 실패 소리를 내도 성공과 겹치지 않는다.
    /// </summary>
    protected override void OnTimeUp()
    {
        if (state != State.Failed)
        {
            state = State.Failed;
            onFail.Invoke();
        }

        base.OnTimeUp();
    }

    /// <summary>
    /// 배선이 빠졌을 때 조용히 안 움직이는 대신 콘솔에 알려준다.
    /// 재생을 시작할 때 한 번만 확인한다.
    /// </summary>
    private void WarnIfNotWired()
    {
        if (hand == null)
        {
            Debug.LogWarning($"[{name}] 손이 비어 있어 움직일 것이 없다. " +
                "인스펙터의 Hand 칸을 채우거나 자식 오브젝트 이름을 'Hand' 로 맞춘다.", this);
            return;
        }

        if (hand.GetComponent<Renderer>() == null)
        {
            Debug.LogWarning($"[{name}] 손('{hand.name}')에 SpriteRenderer 가 없어 화면에 보이지 않는다. " +
                "손 스프라이트를 붙이거나, 스프라이트가 있는 오브젝트를 Hand 칸에 넣는다.", this);
        }
    }

    /// <summary>WASD / 방향키로 손을 움직이고 이동 범위 안으로 가둔다.</summary>
    private void MoveHand()
    {
        if (hand == null)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        Vector2 input = Vector2.zero;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;

        // 대각선이 더 빠르지 않도록 정규화한다.
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        Vector3 position = GetLocalPosition(hand);
        position.x = Mathf.Clamp(position.x + input.x * moveSpeed * Time.deltaTime, -handAreaHalfSize.x, handAreaHalfSize.x);
        position.y = Mathf.Clamp(position.y + input.y * moveSpeed * Time.deltaTime, -handAreaHalfSize.y, handAreaHalfSize.y);
        SetLocalPosition(hand, position);
    }

    /// <summary>간격에 맞춰 쥐를 한 마리씩 내려보낸다.</summary>
    private void UpdateSpawn()
    {
        if (rats == null || spawnedCount >= rats.Length)
            return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f)
            return;

        Spawn(spawnedCount);
        spawnedCount++;
        spawnTimer = spawnInterval;
    }

    private void Spawn(int index)
    {
        Transform rat = rats[index];
        if (rat == null)
        {
            // 빈 칸은 이미 지나간 것으로 친다 — 없는 쥐를 기다리다 못 끝내면 안 된다.
            ratStates[index] = RatState.Caught;
            caughtCount++;
            CheckCleared();
            return;
        }

        float x = Random.Range(spawnXRange.x, spawnXRange.y);
        SetLocalPosition(rat, new Vector3(x, spawnY, ratStartPositions[index].z));

        rat.localScale = ratStartScales[index];
        rat.localRotation = Quaternion.identity;
        rat.gameObject.SetActive(true);

        fallSpeeds[index] = Random.Range(fallSpeedRange.x, fallSpeedRange.y);
        ratStates[index] = RatState.Falling;
    }

    /// <summary>내려오는 쥐들을 움직이고 잡힘/놓침을 판정한다.</summary>
    private void UpdateRats()
    {
        if (rats == null)
            return;

        // 손이 없어도(배선 누락) 쥐는 내려가야 한다 — 그래야 실패 판정이라도 제대로 난다.
        bool canCatch = hand != null;
        Vector3 handPosition = canCatch ? GetLocalPosition(hand) : Vector3.zero;

        for (int i = 0; i < rats.Length; i++)
        {
            if (ratStates[i] != RatState.Falling || rats[i] == null)
                continue;

            Vector3 position = GetLocalPosition(rats[i]);
            position.y -= fallSpeeds[i] * Time.deltaTime;
            SetLocalPosition(rats[i], position);

            if (canCatch && Vector2.Distance(handPosition, position) <= catchRadius)
            {
                Catch(i);
                continue;
            }

            if (position.y <= failLineY)
            {
                Fail();
                return;
            }
        }
    }

    private void Catch(int index)
    {
        ratStates[index] = RatState.Caught;
        caughtCount++;

        onRatCaught.Invoke();
        StartCoroutine(CatchRoutine(index));

        CheckCleared();
    }

    /// <summary>잡힌 쥐를 오그라뜨려 치운다. 연출일 뿐이라 판정은 이미 끝나 있다.</summary>
    private IEnumerator CatchRoutine(int index)
    {
        Transform rat = rats[index];
        if (rat == null)
            yield break;

        Vector3 startScale = ratStartScales[index];
        float duration = Mathf.Max(0f, catchAnimDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rat.localScale = Vector3.Lerp(startScale, startScale * 0.1f, t);
            rat.Rotate(0f, 0f, 540f * Time.deltaTime);
            yield return null;
        }

        rat.gameObject.SetActive(false);
        rat.localScale = startScale;
        rat.localRotation = Quaternion.identity;
    }

    private void CheckCleared()
    {
        if (state != State.Playing || caughtCount < rats.Length)
            return;

        state = State.Cleared;
        onClear.Invoke();

        // 와리오웨어 규칙 — 성공은 즉시 끝나지 않고 제한시간이 다 찰 때 통지된다.
        SucceedWhenTimeUp();
    }

    private void Fail()
    {
        if (state != State.Playing)
            return;

        state = State.Failed;
        onFail.Invoke();
        FailImmediately();
    }

    /// <summary>자식 'Rats' 밑의 오브젝트들을 쥐 목록으로 삼는다.</summary>
    private void CollectRatsFromChildren()
    {
        Transform group = transform.Find("Rats");
        if (group == null)
            return;

        rats = new Transform[group.childCount];
        for (int i = 0; i < rats.Length; i++)
            rats[i] = group.GetChild(i);
    }

    private void CacheStarts()
    {
        if (hand != null)
            handStart = GetLocalPosition(hand);

        int count = rats == null ? 0 : rats.Length;
        ratStates = new RatState[count];
        fallSpeeds = new float[count];
        ratStartScales = new Vector3[count];
        ratStartPositions = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            ratStartScales[i] = rats[i] != null ? rats[i].localScale : Vector3.one;
            ratStartPositions[i] = rats[i] != null ? GetLocalPosition(rats[i]) : Vector3.zero;
        }
    }

    /// <summary>모든 상태를 시작 전으로 되돌린다. 재생 중이 아닐 때 불려도 안전하다(계약 1.4).</summary>
    private void ResetObjects()
    {
        if (rats != null && (ratStates == null || ratStates.Length != rats.Length))
            CacheStarts();

        if (hand != null)
            SetLocalPosition(hand, handStart);

        spawnedCount = 0;
        caughtCount = 0;
        spawnTimer = 0f;

        if (rats == null)
            return;

        for (int i = 0; i < rats.Length; i++)
        {
            ratStates[i] = RatState.Waiting;

            if (rats[i] == null)
                continue;

            SetLocalPosition(rats[i], ratStartPositions[i]);
            rats[i].localScale = ratStartScales[i];
            rats[i].localRotation = Quaternion.identity;
            rats[i].gameObject.SetActive(false);
        }
    }

    /// <summary>대상의 위치를 이 프리팹 루트 기준 좌표로 읽는다.</summary>
    private Vector3 GetLocalPosition(Transform target) => transform.InverseTransformPoint(target.position);

    /// <summary>루트 기준 좌표로 대상을 옮긴다.</summary>
    private void SetLocalPosition(Transform target, Vector3 localPosition)
        => target.position = transform.TransformPoint(localPosition);

    private void OnDrawGizmosSelected()
    {
        // 손 이동 범위
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(handAreaHalfSize.x * 2f, handAreaHalfSize.y * 2f, 0f));

        // 등장선 / 실패선
        float halfWidth = Mathf.Max(Mathf.Abs(spawnXRange.x), Mathf.Abs(spawnXRange.y), handAreaHalfSize.x);
        Gizmos.color = new Color(0.6f, 1f, 0.6f, 0.9f);
        Gizmos.DrawLine(new Vector3(-halfWidth, spawnY, 0f), new Vector3(halfWidth, spawnY, 0f));

        Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.9f);
        Gizmos.DrawLine(new Vector3(-halfWidth, failLineY, 0f), new Vector3(halfWidth, failLineY, 0f));

        // 잡기 판정 범위
        if (hand != null)
        {
            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(transform.InverseTransformPoint(hand.position), catchRadius);
        }

        Gizmos.matrix = Matrix4x4.identity;
    }
}
