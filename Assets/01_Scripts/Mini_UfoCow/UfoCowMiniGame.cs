using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// UFO를 좌우로 움직여 목장의 소를 모두 납치하는 제한시간 미니게임.
/// A/D(또는 좌우 방향키)로 UFO를 옮기고, 소 위에서 스페이스를 눌러 흡입한다.
/// 소를 전부 흡입하면 UFO가 화면 위로 날아간 뒤 제한시간이 다 찰 때 성공한다.
///
/// 프리팹으로 만들어 <see cref="MiniGamePlayer"/> 가 재생한다.
/// </summary>
public sealed class UfoCowMiniGame : TimedMiniGame
{
    private enum State
    {
        Idle,
        Playing,

        /// <summary>소를 다 잡고 UFO가 화면 위로 빠져나가는 중.</summary>
        Escaping,

        /// <summary>탈출 연출까지 끝나고 제한 시간이 차기를 기다리는 중.</summary>
        SuccessPending,

        Failed,
    }

    [Header("구성")]
    [Tooltip("좌우로 움직일 UFO. 비워두면 자식에서 'UFO' 를 찾는다")]
    [SerializeField] private Transform ufo;

    [Tooltip("흡입 중에만 켜지는 광선. 비워두면 자식에서 'UFO/Beam' 을 찾는다")]
    [SerializeField] private Transform beam;

    [Tooltip("납치할 소 목록. 비워두면 자식 'Cows' 아래를 통째로 쓴다")]
    [SerializeField] private Transform[] cows;

    [Header("조작")]
    [Tooltip("UFO 좌우 이동 속도(월드 유닛/초)")]
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("UFO가 좌우로 갈 수 있는 한계(월드 유닛). 목장 폭에 맞춘다")]
    [SerializeField] private float horizontalLimit = 7.2f;

    [Tooltip("이 거리 안에 있는 소만 흡입할 수 있다(월드 유닛)")]
    [SerializeField] private float suctionHalfWidth = 1.15f;

    [Tooltip("소 한 마리를 빨아올리는 데 걸리는 시간(초)")]
    [SerializeField] private float suctionDuration = 0.45f;

    [Header("탈출 연출")]
    [Tooltip("소를 다 잡은 뒤 UFO가 위로 빨라지는 가속도(월드 유닛/초²)")]
    [SerializeField] private float escapeAcceleration = 24f;

    [Tooltip("탈출 연출을 재생하는 시간(초)")]
    [SerializeField] private float escapeDuration = 0.75f;

    [Header("이벤트")]
    [Tooltip("소 한 마리를 잡을 때마다 발화. 흡입음 등을 연결한다")]
    public UnityEvent onCowCaptured;

    [Tooltip("소를 전부 잡아 탈출을 시작하는 순간 발화")]
    public UnityEvent onAllCowsCaptured;

    [Tooltip("제한 시간 안에 다 잡지 못했을 때 발화")]
    public UnityEvent onFail;

    private Vector3 ufoStart;
    private Vector3[] cowStarts;
    private bool[] captured;

    // 흡입 연출이 도는 동안은 다음 흡입을 받지 않는다.
    private bool suctionBusy;
    private State state = State.Idle;


    // ── 수명주기 ──

    /// <summary>비어 있는 참조를 자식에서 찾아 채우고 시작 자세를 기억해 둔다.</summary>
    private void Awake()
    {
        if (ufo == null)
            ufo = transform.Find("UFO");

        if (beam == null)
            beam = transform.Find("UFO/Beam");

        if (cows == null || cows.Length == 0)
            CollectCowsFromChildren();

        CacheStarts();
    }

    /// <summary>게임을 시작한다. 소와 UFO를 제자리에 되돌린다.</summary>
    protected override void OnTimedPlay()
    {
        ResetObjects();
        state = State.Playing;
    }

    /// <summary>진행 중인 연출을 끊고 초기 상태로 되돌린다.</summary>
    protected override void OnTimedStopAndReset()
    {
        StopAllCoroutines();
        ResetObjects();
        state = State.Idle;
    }

    /// <summary>UFO를 좌우로 움직이고, 스페이스로 흡입을 시도한다.</summary>
    protected override void OnTimedUpdate()
    {
        if (state != State.Playing || ufo == null)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        float direction = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            direction -= 1f;

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            direction += 1f;

        Vector3 position = ufo.localPosition;
        position.x = Mathf.Clamp(
            position.x + direction * moveSpeed * Time.deltaTime, -horizontalLimit, horizontalLimit);
        ufo.localPosition = position;

        if (!suctionBusy && keyboard.spaceKey.wasPressedThisFrame)
            TryCaptureCow();
    }

    /// <summary>제한 시간 안에 소를 다 잡지 못했다.</summary>
    protected override void OnTimeUp()
    {
        state = State.Failed;
        onFail.Invoke();
        base.OnTimeUp();
    }


    // ── 흡입 ──

    /// <summary>흡입 범위 안에서 가장 가까운 소를 골라 빨아올린다. 없으면 아무 일도 없다.</summary>
    private void TryCaptureCow()
    {
        int target = -1;
        float closest = float.MaxValue;

        for (int i = 0; i < cows.Length; i++)
        {
            if (captured[i] || cows[i] == null)
                continue;

            float distance = Mathf.Abs(cows[i].position.x - ufo.position.x);
            if (distance <= suctionHalfWidth && distance < closest)
            {
                target = i;
                closest = distance;
            }
        }

        if (target >= 0)
            StartCoroutine(SuckCow(target));
    }

    /// <summary>
    /// 소 한 마리가 UFO로 빨려 올라간다. 올라가면서 작아지고 빙글빙글 돈다.
    /// 마지막 한 마리였다면 이어서 탈출 연출로 넘어간다.
    /// </summary>
    private IEnumerator SuckCow(int index)
    {
        suctionBusy = true;

        if (beam != null)
            beam.gameObject.SetActive(true);

        Transform cow = cows[index];
        Vector3 start = cow.position;
        Vector3 end = ufo.position + Vector3.down * 0.15f;

        float elapsedTime = 0f;
        while (elapsedTime < suctionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / suctionDuration);

            cow.position = Vector3.Lerp(start, end, t);
            cow.localScale = Vector3.one * Mathf.Lerp(1f, 0.12f, t);
            cow.Rotate(0f, 0f, 720f * Time.deltaTime);

            yield return null;
        }

        cow.gameObject.SetActive(false);
        captured[index] = true;
        onCowCaptured.Invoke();

        if (beam != null)
            beam.gameObject.SetActive(false);

        suctionBusy = false;

        // 아직 남은 소가 있으면 계속 진행한다.
        for (int i = 0; i < captured.Length; i++)
        {
            if (!captured[i])
                yield break;
        }

        StartCoroutine(Escape());
    }

    /// <summary>소를 다 잡았다. UFO가 점점 빨라지며 화면 위로 사라진 뒤 성공을 예약한다.</summary>
    private IEnumerator Escape()
    {
        state = State.Escaping;
        onAllCowsCaptured.Invoke();

        float speed = 0f;
        float elapsedTime = 0f;

        while (elapsedTime < escapeDuration)
        {
            elapsedTime += Time.deltaTime;
            speed += escapeAcceleration * Time.deltaTime;
            ufo.localPosition += Vector3.up * speed * Time.deltaTime;
            yield return null;
        }

        state = State.SuccessPending;
        SucceedWhenTimeUp();
    }


    // ── 초기화 ──

    /// <summary>인스펙터에 소를 지정하지 않았을 때 자식 'Cows' 아래를 통째로 가져온다.</summary>
    private void CollectCowsFromChildren()
    {
        Transform herd = transform.Find("Cows");
        if (herd == null)
            return;

        cows = new Transform[herd.childCount];
        for (int i = 0; i < cows.Length; i++)
            cows[i] = herd.GetChild(i);
    }

    /// <summary>UFO와 소들의 시작 위치를 기억해 둔다. 매 판 여기로 되돌린다.</summary>
    private void CacheStarts()
    {
        if (ufo != null)
            ufoStart = ufo.localPosition;

        int count = cows == null ? 0 : cows.Length;
        cowStarts = new Vector3[count];
        captured = new bool[count];

        for (int i = 0; i < count; i++)
        {
            if (cows[i] != null)
                cowStarts[i] = cows[i].localPosition;
        }
    }

    /// <summary>UFO·광선·소를 처음 상태로 되돌린다.</summary>
    private void ResetObjects()
    {
        suctionBusy = false;

        if (ufo != null)
            ufo.localPosition = ufoStart;

        if (beam != null)
            beam.gameObject.SetActive(false);

        // 소 목록이 런타임에 바뀌었을 수 있으니 길이가 어긋나면 다시 잡는다.
        if (cows == null || cowStarts == null || cowStarts.Length != cows.Length)
            CacheStarts();

        for (int i = 0; i < cows.Length; i++)
        {
            captured[i] = false;

            if (cows[i] == null)
                continue;

            cows[i].gameObject.SetActive(true);
            cows[i].localPosition = cowStarts[i];
            cows[i].localRotation = Quaternion.identity;
            cows[i].localScale = Vector3.one;
        }
    }
}
