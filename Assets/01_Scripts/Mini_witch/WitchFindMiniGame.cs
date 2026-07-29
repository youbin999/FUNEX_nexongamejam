using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 마녀를 찾아라! 미니게임.
/// 4x4 그리드에 숨은 마녀 1~2 마리를 WASD 로 커서를 옮기고 스페이스바로 표시해 찾는다.
/// 제한 시간 안에 마녀를 전부 표시하면 클리어, 일반 얼굴을 잘못 표시하거나 시간이 다 되면 실패.
/// 프리팹으로 만들어 <see cref="MiniGamePlayer"/> 가 재생한다.
/// </summary>
public class WitchFindMiniGame : TimedMiniGame
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

    [Header("얼굴 스프라이트")]
    [Tooltip("비워두면 스프라이트를 바꾸지 않고 기본 이미지를 그대로 쓴다(플레이스홀더)")]
    [SerializeField] private Sprite[] normalFaceSprites;
    [SerializeField] private Sprite[] witchFaceSprites;

    [Header("규칙")]
    [Tooltip("이번 판에 등장할 마녀 숫자 범위(포함, 포함)")]
    [SerializeField] private int minWitchCount = 1;
    [SerializeField] private int maxWitchCount = 2;

    [Header("이벤트")]
    [Tooltip("(찾은 마녀 수, 이번 판 전체 마녀 수)")]
    public ProgressEvent onProgressChanged;

    public UnityEvent onClear;
    public UnityEvent onFail;
    [Tooltip("일반 여성을 잘못 선택했을 때 발생한다.")]
    public UnityEvent onWrongSelection;

    private readonly List<int> witchIndices = new List<int>();
    private bool[] marked = Array.Empty<bool>();
    private int cursorX;
    private int cursorY;
    private int foundCount;
    private State state = State.Idle;

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
        witchIndices.Clear();
        marked = new bool[cells.Length];

        onProgressChanged.Invoke(0, 0);

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] != null)
                cells[i].ResetVisual();
        }

        UpdateCursorVisual();
    }

    /// <summary>마녀 수를 정하고 칸을 섞어 배치한 뒤 얼굴 스프라이트를 채운다.</summary>
    private void SetupGrid()
    {
        int witchCount = UnityEngine.Random.Range(minWitchCount, maxWitchCount + 1);
        witchCount = Mathf.Clamp(witchCount, 0, cells.Length);

        List<int> shuffled = new List<int>(cells.Length);
        for (int i = 0; i < cells.Length; i++)
            shuffled.Add(i);

        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        for (int i = 0; i < witchCount; i++)
            witchIndices.Add(shuffled[i]);

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] == null)
                continue;

            bool isWitch = witchIndices.Contains(i);
            cells[i].SetFace(PickSprite(isWitch ? witchFaceSprites : normalFaceSprites));
        }

        onProgressChanged.Invoke(0, witchIndices.Count);
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
    /// 커서가 놓인 칸을 골라낸다. 이미 고른 칸은 무시한다.
    /// - 마녀면 진행도가 오르고, 전부 찾으면 클리어
    /// - 멀쩡한 얼굴이면 그 얼굴이 귀신 패널티로 넘어가며 즉시 실패
    /// </summary>
    private void MarkCurrent()
    {
        int index = cursorY * GridWidth + cursorX;
        if (index < 0 || index >= cells.Length || index >= marked.Length)
            return;

        if (marked[index])
            return;

        marked[index] = true;

        bool isWitch = witchIndices.Contains(index);
        if (cells[index] != null)
            cells[index].SetMarked(isWitch);

        if (isWitch)
        {
            foundCount++;
            onProgressChanged.Invoke(foundCount, witchIndices.Count);

            if (foundCount >= witchIndices.Count)
            {
                state = State.Cleared;
                onClear.Invoke();
                SucceedWhenTimeUp();
            }
        }
        else
        {
            state = State.Failed;
            // 잘못 고른 얼굴이 푸른 귀신이 되어 다음 미니게임까지 따라다닌다.
            WitchGhostPenaltyRequest.Set(cells[index] != null ? cells[index].CurrentFace : null, true);
            onWrongSelection.Invoke();
            FailImmediately();
        }
    }

    /// <summary>제한 시간을 다 써서 실패로 확정될 때 호출된다.</summary>
    protected override void OnTimeUp()
    {
        state = State.Failed;
        // 시간 초과는 놓친 마녀가 틴트 없이 반투명하게 따라다닌다.
        WitchGhostPenaltyRequest.Set(FindRemainingWitchFace(), false);
        onFail.Invoke();
        base.OnTimeUp();
    }

    /// <summary>아직 못 찾은 마녀의 얼굴 스프라이트. 없으면 첫 마녀 얼굴을 돌려준다.</summary>
    private Sprite FindRemainingWitchFace()
    {
        Sprite firstWitchFace = null;

        for (int i = 0; i < witchIndices.Count; i++)
        {
            int index = witchIndices[i];
            if (index < 0 || index >= cells.Length || cells[index] == null)
                continue;

            Sprite face = cells[index].CurrentFace;
            if (firstWitchFace == null)
                firstWitchFace = face;

            if (index < marked.Length && !marked[index])
                return face;
        }

        return firstWitchFace;
    }
}
