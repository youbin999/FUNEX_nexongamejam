using System;
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

    [Header("이벤트")]
    [Tooltip("(골라낸 곰팡이 접시 수, 이번 판 전체 곰팡이 접시 수)")]
    public ProgressEvent onProgressChanged;

    public UnityEvent onClear;
    public UnityEvent onFail;

    private readonly List<int> moldIndices = new List<int>();
    private bool[] marked = Array.Empty<bool>();
    private int cursorX;
    private int cursorY;
    private int foundCount;
    private State state = State.Idle;

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

        UpdateCursorVisual();
    }

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

    private Sprite PickSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        return sprites[UnityEngine.Random.Range(0, sprites.Length)];
    }

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

    private void MoveCursor(int dx, int dy)
    {
        cursorX = Mathf.Clamp(cursorX + dx, 0, GridWidth - 1);
        cursorY = Mathf.Clamp(cursorY + dy, 0, GridHeight - 1);
        UpdateCursorVisual();
    }

    private void UpdateCursorVisual()
    {
        int selectedIndex = cursorY * GridWidth + cursorX;

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] != null)
                cells[i].SetSelected(i == selectedIndex);
        }
    }

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
            }
        }
        else
        {
            state = State.Failed;
            onFail.Invoke();
            FailImmediately();
        }
    }

    /// <summary>제한 시간을 다 써서 실패로 확정될 때 호출된다.</summary>
    protected override void OnTimeUp()
    {
        state = State.Failed;
        onFail.Invoke();
        base.OnTimeUp();
    }
}
