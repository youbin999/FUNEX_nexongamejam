using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 골라내라! 페니실린 미니게임.
/// 4x4 그리드에 놓인 페트리 접시 중 푸른곰팡이가 핀 접시를 WASD 로 커서를 옮기고 스페이스바로 골라낸다.
/// 제한 시간 안에 곰팡이 접시를 전부 고르면 클리어, 멀쩡한 접시를 고르거나 시간이 다 되면 실패.
///
/// <see cref="WitchFindMiniGame"/> 과 규칙·조작이 **완전히 같다.** 의도된 중복이다 —
/// 중세에서 "골라내라"를 시킨 뒤 근대에서 똑같은 화면으로 다시 시키는 것이 이 게임의 주제 장치다.
/// 플레이어가 손으로 먼저 알아채게 하는 게 목적이므로, 그리드 배치와 조작은 절대 다르게 만들지 않는다.
///
/// 그리드 칸은 <see cref="WitchGridCell"/> 을 그대로 공유한다. 표시 문구는 칸 쪽 correctLabel 로 바꾼다.
/// </summary>
public class PenicillinFindMiniGame : TimedMiniGame
{
    // 인스펙터에 확실히 노출되도록 제네릭 UnityEvent 는 구체 타입으로 선언한다.
    [Serializable] public class ProgressEvent : UnityEvent<int, int> { }

    private enum State
    {
        Idle,
        Playing,
        Cleared,
        Failed,
    }

    private const int GridWidth = 4;
    private const int GridHeight = 4;

    [Header("그리드")]
    [Tooltip("4x4 = 16칸. 왼쪽 위부터 가로로 채운 순서(row-major)로 등록한다")]
    [SerializeField] private WitchGridCell[] cells = new WitchGridCell[GridWidth * GridHeight];

    [Header("접시 스프라이트")]
    [Tooltip("비워두면 스프라이트를 바꾸지 않고 기본 이미지를 그대로 쓴다(플레이스홀더)")]
    [SerializeField] private Sprite[] normalDishSprites;

    [Tooltip("푸른곰팡이가 핀 접시")]
    [SerializeField] private Sprite[] moldDishSprites;

    [Header("규칙")]
    [Tooltip("이번 판에 등장할 곰팡이 접시 숫자 범위(포함, 포함)")]
    [SerializeField] private int minMoldCount = 1;
    [SerializeField] private int maxMoldCount = 2;

    [Header("클리어 연출")]
    [Tooltip("접시 그리드 전체. 클리어하면 통째로 숨긴다")]
    [SerializeField] private GameObject gridRoot;

    [Tooltip("밑에서 위로 올라오는 페니. 다 올라오면 윙크 스프라이트로 바뀐다")]
    [SerializeField] private SpriteRenderer feniRenderer;

    [Tooltip("페니가 다 올라온 뒤 바꿔 끼울 윙크 스프라이트")]
    [SerializeField] private Sprite feniWinkSprite;

    [Tooltip("윙크에 맞춰 팝아웃되는 BOOO 들. 등록 순서대로 튀어나온다")]
    [SerializeField] private SpriteRenderer[] boooRenderers;

    [Header("클리어 연출 타이밍")]
    [Tooltip("페니가 화면 아래에서 제자리까지 올라오는 시간")]
    [SerializeField] private float feniRiseDuration = 0.45f;

    [Tooltip("페니가 출발하는 지점의 아래 오프셋(월드 유닛). 화면 밖에서 시작하도록 넉넉히 잡는다")]
    [SerializeField] private float feniRiseDistance = 6f;

    [Tooltip("페니가 다 올라온 뒤 윙크로 바뀌기까지의 간격")]
    [SerializeField] private float winkDelay = 0.12f;

    [Tooltip("BOOO 하나가 팝아웃되는 시간")]
    [SerializeField] private float boooPopDuration = 0.22f;

    [Tooltip("BOOO 들이 순서대로 튀어나오는 간격. 0이면 전부 동시에 튀어나온다")]
    [SerializeField] private float boooPopInterval = 0.08f;

    [Header("실패 연출")]
    [Tooltip("실패하면 팝아웃되는 페니. 멀쩡한 접시를 골랐을 때와 시간 초과 모두에서 나온다")]
    [SerializeField] private SpriteRenderer feniFailRenderer;

    [Tooltip("실패 페니가 팝아웃되는 시간. 실패는 즉시 종료되므로 짧게 잡는다")]
    [SerializeField] private float failPopDuration = 0.22f;

    [Header("이벤트")]
    [Tooltip("(골라낸 곰팡이 접시 수, 이번 판 전체 곰팡이 접시 수)")]
    public ProgressEvent onProgressChanged;

    [Tooltip("페니가 윙크로 바뀌는 순간 발화한다. 윙크 효과음처럼 연출과 붙어야 하는 소리를 연결한다.\n" +
        "onClear 는 페니가 올라오기도 전에 발화하므로 이 타이밍에는 쓸 수 없다")]
    public UnityEvent onWink;

    public UnityEvent onClear;
    public UnityEvent onFail;

    private readonly List<int> moldIndices = new List<int>();
    private bool[] marked = Array.Empty<bool>();
    private int cursorX;
    private int cursorY;
    private int foundCount;
    private State state = State.Idle;

    // 성공 연출과 실패 연출은 동시에 나지 않으므로 코루틴 슬롯 하나를 공유한다.
    private Coroutine outcomeRoutine;

    // 클리어 연출이 아직 도는 중인지. 제한 시간이 다 차도 이게 내려갈 때까지는 성공을 통지하지 않는다.
    // outcomeRoutine 의 null 여부로 대신하지 않는 이유는, 연출이 한 프레임에 끝나 버리는 배선
    // (페니·BOOO 가 비어 있는 경우)에서 StartCoroutine 이 돌려준 값이 뒤늦게 덮어써지기 때문이다.
    private bool clearPresentationPlaying;

    // 인스펙터에 배치된 자리를 기억해 뒀다가 매 판 그 자리로 되돌린다.
    private Vector3 feniBasePosition;
    private Sprite feniBaseSprite;
    private Vector3[] boooBaseScales;
    private Vector3 feniFailBaseScale = Vector3.one;


    // ── 수명주기 ──

    /// <summary>페니·BOOO 가 배치된 자리와 크기를 기억해 둔다. 매 판 여기로 되돌린다.</summary>
    private void Awake()
    {
        if (feniRenderer != null)
        {
            feniBasePosition = feniRenderer.transform.localPosition;
            feniBaseSprite = feniRenderer.sprite;
        }

        if (feniFailRenderer != null)
            feniFailBaseScale = feniFailRenderer.transform.localScale;

        if (boooRenderers != null)
        {
            boooBaseScales = new Vector3[boooRenderers.Length];
            for (int i = 0; i < boooRenderers.Length; i++)
            {
                boooBaseScales[i] = boooRenderers[i] != null
                    ? boooRenderers[i].transform.localScale
                    : Vector3.one;
            }
        }
    }

    /// <summary>
    /// 페니가 올라와 윙크하고 BOOO 가 다 튀어나올 때까지는 성공을 통지하지 않는다.
    /// 제한 시간(3초)이 짧아서 늦게 클리어하면 연출이 잘린 채 다음 게임으로 넘어가 버린다.
    /// </summary>
    protected override bool IsSuccessPresentationDone => !clearPresentationPlaying;

    /// <summary>매 프레임 입력과 진행을 처리한다. 결과가 확정된 뒤에는 불리지 않는다.</summary>
    protected override void OnTimedUpdate()
    {
        HandleInput();
    }

    /// <summary>게임을 시작한다. 그리드를 새로 섞고 Playing 으로 전환한다.</summary>
    protected override void OnTimedPlay()
    {
        ResetInternal();
        SetupGrid();
        state = State.Playing;
    }

    /// <summary>게임을 강제 중단하고 초기 상태(Idle)로 되돌린다.</summary>
    protected override void OnTimedStopAndReset()
    {
        ResetInternal();
        state = State.Idle;
    }

    /// <summary>커서, 진행도, 칸 연출을 처음으로 되돌린다.</summary>
    private void ResetInternal()
    {
        cursorX = 0;
        cursorY = 0;
        foundCount = 0;
        moldIndices.Clear();
        marked = new bool[cells.Length];

        onProgressChanged.Invoke(0, 0);

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] != null)
                cells[i].ResetVisual();
        }

        ResetOutcomeVisual();
        UpdateCursorVisual();
    }

    /// <summary>
    /// 성공·실패 연출을 배치 당시 상태로 되돌린다.
    /// 재생 중이 아닐 때 불려도 안전해야 한다 — 프리팹이 풀에서 다시 꺼내질 때마다 불린다.
    /// </summary>
    private void ResetOutcomeVisual()
    {
        if (outcomeRoutine != null)
        {
            StopCoroutine(outcomeRoutine);
            outcomeRoutine = null;
        }

        // 연출을 끊었으면 붙잡아 둘 이유도 사라진다. 여기서 안 내리면 다음 판이 끝나지 않는다.
        clearPresentationPlaying = false;

        if (feniFailRenderer != null)
        {
            feniFailRenderer.transform.localScale = feniFailBaseScale;
            feniFailRenderer.gameObject.SetActive(false);
        }

        if (gridRoot != null)
            gridRoot.SetActive(true);

        if (feniRenderer != null)
        {
            feniRenderer.transform.localPosition = feniBasePosition;

            if (feniBaseSprite != null)
                feniRenderer.sprite = feniBaseSprite;

            feniRenderer.gameObject.SetActive(false);
        }

        if (boooRenderers != null)
        {
            for (int i = 0; i < boooRenderers.Length; i++)
            {
                if (boooRenderers[i] == null)
                    continue;

                boooRenderers[i].transform.localScale = BaseScale(i);
                boooRenderers[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>index 번째 BOOO 의 배치 당시 크기. 범위를 벗어나면 1배.</summary>
    private Vector3 BaseScale(int index)
        => boooBaseScales != null && index < boooBaseScales.Length ? boooBaseScales[index] : Vector3.one;

    /// <summary>곰팡이 접시 수를 정하고 칸을 섞어 배치한 뒤 접시 스프라이트를 채운다.</summary>
    private void SetupGrid()
    {
        int moldCount = UnityEngine.Random.Range(minMoldCount, maxMoldCount + 1);
        moldCount = Mathf.Clamp(moldCount, 0, cells.Length);

        List<int> shuffled = new List<int>(cells.Length);
        for (int i = 0; i < cells.Length; i++)
            shuffled.Add(i);

        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        for (int i = 0; i < moldCount; i++)
            moldIndices.Add(shuffled[i]);

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] == null)
                continue;

            bool isMold = moldIndices.Contains(i);
            cells[i].SetFace(PickSprite(isMold ? moldDishSprites : normalDishSprites));
        }

        onProgressChanged.Invoke(0, moldIndices.Count);
    }

    /// <summary>배열에서 스프라이트 하나를 무작위로 고른다. 비어 있으면 null.</summary>
    private Sprite PickSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        return sprites[UnityEngine.Random.Range(0, sprites.Length)];
    }


    // ── 조작 ──

    /// <summary>WASD 로 커서를 옮기고 스페이스로 현재 칸을 골라낸다.</summary>
    private void HandleInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        int dx = 0;
        int dy = 0;

        if (keyboard.aKey.wasPressedThisFrame)
            dx = -1;
        else if (keyboard.dKey.wasPressedThisFrame)
            dx = 1;
        else if (keyboard.wKey.wasPressedThisFrame)
            dy = -1;
        else if (keyboard.sKey.wasPressedThisFrame)
            dy = 1;

        if (dx != 0 || dy != 0)
            MoveCursor(dx, dy);

        if (keyboard.spaceKey.wasPressedThisFrame)
            MarkCurrent();
    }

    /// <summary>커서를 그리드 범위 안에서 옮기고 표시를 갱신한다.</summary>
    private void MoveCursor(int dx, int dy)
    {
        cursorX = Mathf.Clamp(cursorX + dx, 0, GridWidth - 1);
        cursorY = Mathf.Clamp(cursorY + dy, 0, GridHeight - 1);
        UpdateCursorVisual();
    }

    /// <summary>커서가 놓인 칸만 선택 표시를 켠다.</summary>
    private void UpdateCursorVisual()
    {
        int selectedIndex = cursorY * GridWidth + cursorX;

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] != null)
                cells[i].SetSelected(i == selectedIndex);
        }
    }

    /// <summary>
    /// 커서가 놓인 접시를 골라낸다. 이미 고른 칸은 무시한다.
    /// - 곰팡이면 진행도가 오르고, 전부 찾으면 클리어 연출을 재생
    /// - 멀쩡한 접시면 실패 팝아웃을 띄우고 즉시 실패
    /// </summary>
    private void MarkCurrent()
    {
        int index = cursorY * GridWidth + cursorX;
        if (index < 0 || index >= cells.Length || index >= marked.Length)
            return;

        if (marked[index])
            return;

        marked[index] = true;

        bool isMold = moldIndices.Contains(index);
        if (cells[index] != null)
            cells[index].SetMarked(isMold);

        if (isMold)
        {
            foundCount++;
            onProgressChanged.Invoke(foundCount, moldIndices.Count);

            if (foundCount >= moldIndices.Count)
            {
                state = State.Cleared;
                onClear.Invoke();
                SucceedWhenTimeUp();

                // StartCoroutine 이 연출을 그 자리에서 끝내 버릴 수도 있으므로 플래그를 먼저 올린다.
                clearPresentationPlaying = true;
                outcomeRoutine = StartCoroutine(ClearRoutine());
            }
        }
        else
        {
            state = State.Failed;
            onFail.Invoke();

            // 팝아웃을 먼저 띄운다 — FailImmediately() 는 곧바로 종료를 통지한다.
            outcomeRoutine = StartCoroutine(FailPopRoutine());
            FailImmediately();
        }
    }

    /// <summary>제한 시간을 다 써서 실패로 확정될 때 호출된다.</summary>
    protected override void OnTimeUp()
    {
        state = State.Failed;
        onFail.Invoke();

        outcomeRoutine = StartCoroutine(FailPopRoutine());
        base.OnTimeUp();
    }

    /// <summary>
    /// 클리어 연출: 그리드가 사라지고 → 페니가 아래에서 올라오고 → 윙크로 바뀌며 → BOOO 가 팝아웃된다.
    /// 성공 통지는 <b>제한 시간과 이 연출이 둘 다 끝난 뒤</b>에 나간다
    /// (<see cref="IsSuccessPresentationDone"/>). 연출이 먼저 끝나면 남은 시간만큼 그대로 유지되고,
    /// 제한 시간이 먼저 차면 연출이 끝날 때까지 종료가 미뤄진다.
    /// </summary>
    private IEnumerator ClearRoutine()
    {
        if (gridRoot != null)
            gridRoot.SetActive(false);

        yield return FeniRiseRoutine();

        if (winkDelay > 0f)
            yield return new WaitForSeconds(winkDelay);

        // 윙크로 바뀌는 순간에 맞춰 소리를 낸다. 스프라이트가 비어 있어도 박자는 유지한다.
        onWink.Invoke();

        if (feniRenderer != null && feniWinkSprite != null)
            feniRenderer.sprite = feniWinkSprite;

        yield return BoooPopRoutine();

        clearPresentationPlaying = false;
        outcomeRoutine = null;
    }

    /// <summary>
    /// 실패 연출: 그리드가 사라지고 페니가 통통 튀며 팝아웃된다.
    /// 클리어와 마찬가지로 접시 그리드는 통째로 걷어낸다 — 페니만 화면에 남아야 결과가 또렷하다.
    ///
    /// 실패는 <see cref="MiniGame.ReportFinished"/> 가 즉시 통지하므로 화면에 남는 시간이 길지 않다.
    /// 오래 보여주려면 프리팹에 failurePenaltyPrefab 을 물려 정리를 미루는 쪽이 맞다.
    /// </summary>
    private IEnumerator FailPopRoutine()
    {
        // 페니 배선이 비어 있어도 그리드는 걷는다. 아래 조기 반환보다 먼저 처리해야 한다.
        if (gridRoot != null)
            gridRoot.SetActive(false);

        if (feniFailRenderer == null)
            yield break;

        Transform t = feniFailRenderer.transform;
        t.localScale = Vector3.zero;
        feniFailRenderer.gameObject.SetActive(true);

        float duration = Mathf.Max(failPopDuration, 0.0001f);
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            t.localScale = feniFailBaseScale * Ease.OutBack(Mathf.Clamp01(time / duration));
            yield return null;
        }

        t.localScale = feniFailBaseScale;
        outcomeRoutine = null;
    }

    /// <summary>배치된 자리보다 아래에서 시작해 제자리까지 솟아오른다.</summary>
    private IEnumerator FeniRiseRoutine()
    {
        if (feniRenderer == null)
            yield break;

        Transform t = feniRenderer.transform;
        Vector3 endPosition = feniBasePosition;
        Vector3 startPosition = endPosition + Vector3.down * feniRiseDistance;

        t.localPosition = startPosition;
        feniRenderer.gameObject.SetActive(true);

        float duration = Mathf.Max(feniRiseDuration, 0.0001f);
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / duration);

            // ease-out: 빠르게 솟았다가 제자리에서 감속한다.
            t.localPosition = Vector3.Lerp(startPosition, endPosition, 1f - (1f - k) * (1f - k));
            yield return null;
        }

        t.localPosition = endPosition;
    }

    /// <summary>
    /// BOOO 들을 순서대로 팝아웃시킨다.
    /// 코루틴을 하나만 쓰는 이유는 중단(<see cref="ResetOutcomeVisual"/>)이 한 번에 먹어야 하기 때문이다 —
    /// 개별로 StartCoroutine 하면 중단 후에도 살아남아 스케일을 되돌려 놓은 걸 다시 덮어쓴다.
    /// </summary>
    private IEnumerator BoooPopRoutine()
    {
        if (boooRenderers == null || boooRenderers.Length == 0)
            yield break;

        float duration = Mathf.Max(boooPopDuration, 0.0001f);
        float interval = Mathf.Max(boooPopInterval, 0f);

        // 스케일 0 이면 보이지 않으므로 미리 전부 켜 두고 시간차만 준다.
        for (int i = 0; i < boooRenderers.Length; i++)
        {
            if (boooRenderers[i] == null)
                continue;

            boooRenderers[i].transform.localScale = Vector3.zero;
            boooRenderers[i].gameObject.SetActive(true);
        }

        float total = duration + interval * (boooRenderers.Length - 1);
        float time = 0f;

        while (time < total)
        {
            time += Time.deltaTime;

            for (int i = 0; i < boooRenderers.Length; i++)
            {
                if (boooRenderers[i] == null)
                    continue;

                float k = Mathf.Clamp01((time - interval * i) / duration);
                if (k <= 0f)
                    continue;

                boooRenderers[i].transform.localScale = BaseScale(i) * Ease.OutBack(k);
            }

            yield return null;
        }

        for (int i = 0; i < boooRenderers.Length; i++)
        {
            if (boooRenderers[i] == null)
                continue;

            boooRenderers[i].transform.localScale = BaseScale(i);
        }
    }
}
