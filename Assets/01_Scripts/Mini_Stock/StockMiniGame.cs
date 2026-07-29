using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 주식 미니게임 — "매수하라! 매도하라!".
/// 비어 있던 차트 오른쪽에 봉이 하나씩 자라나고, 봉이 다 자라기 전에
/// 양봉이면 W(매수), 음봉이면 S(매도)를 눌러야 한다.
/// 잘못 누르거나 제때 못 누르면 그 자리에서 실패, 전부 맞히면 클리어.
///
/// 봉마다 자라는 시간(<see cref="CandleStep.growDuration"/>)을 따로 줄 수 있어서,
/// 첫 봉만 길게 잡아 초견 플레이어에게 상황 파악할 여유를 주는 식으로 조절한다.
/// 프리팹으로 만들어 <see cref="MiniGamePlayer"/> 가 재생한다.
/// </summary>
public class StockMiniGame : TimedMiniGame
{
    /// <summary>봉의 종류. 어떤 키를 눌러야 하는지를 결정한다.</summary>
    public enum CandleKind
    {
        /// <summary>상승(양봉). 매수 키를 눌러야 한다.</summary>
        Bullish,

        /// <summary>하락(음봉). 매도 키를 눌러야 한다.</summary>
        Bearish,
    }

    /// <summary>봉 하나가 언제 어떻게 등장하는지에 대한 설정.</summary>
    [Serializable]
    public class CandleStep
    {
        [Tooltip("이 차례에 자라날 봉")]
        public StockCandle candle;

        [Tooltip("상승이면 매수(W), 하락이면 매도(S) 를 눌러야 한다")]
        public CandleKind kind = CandleKind.Bullish;

        [Tooltip("봉이 완전히 자라는 데 걸리는 시간(초). 이 시간이 그대로 입력을 받는 창이 된다")]
        public float growDuration = 0.7f;

        [Tooltip("이 봉이 다 자란 뒤 다음 봉이 나오기까지 비는 시간(초)")]
        public float gapAfter = 0.3f;

        [Tooltip("이 봉을 제때 맞혔을 때 발화. 봉마다 다른 연출을 붙이려고 따로 둔다 (동전/지폐/무더기)")]
        public UnityEvent onSuccess;
    }

    [Header("시퀀스")]
    [Tooltip("등장 순서대로의 봉 설정. 봉마다 자라는 시간을 따로 줄 수 있다")]
    [SerializeField]
    private CandleStep[] steps =
    {
        // 첫 봉은 상황 파악할 여유를 주려고 길게 잡아 둔다.
        new CandleStep { kind = CandleKind.Bullish, growDuration = 1.0f, gapAfter = 0.3f },
        new CandleStep { kind = CandleKind.Bullish, growDuration = 0.7f, gapAfter = 0.3f },
        new CandleStep { kind = CandleKind.Bearish, growDuration = 0.7f, gapAfter = 0f },
    };

    [Tooltip("마지막 봉이 다 자란 뒤 결과가 통지될 때까지의 여유 시간(초). 클리어 연출을 보여줄 시간이다")]
    [SerializeField] private float tailDelay = 0.5f;

    [Tooltip("켜면 위 시퀀스 길이에 맞춰 제한 시간을 자동으로 계산한다. 보통 켜 둔다")]
    [SerializeField] private bool autoFitTimeLimit = true;

    [Header("조작")]
    [Tooltip("매수 키")]
    [SerializeField] private Key buyKey = Key.W;

    [Tooltip("매수 보조 키. 필요 없으면 None")]
    [SerializeField] private Key buyKeyAlt = Key.UpArrow;

    [Tooltip("매도 키")]
    [SerializeField] private Key sellKey = Key.S;

    [Tooltip("매도 보조 키. 필요 없으면 None")]
    [SerializeField] private Key sellKeyAlt = Key.DownArrow;

    [Tooltip("켜면 봉이 자라는 중이 아닐 때(봉과 봉 사이) 누른 입력도 실패로 친다")]
    [SerializeField] private bool failOnStrayInput = true;

    [Header("실패 연출")]
    [Tooltip("실패가 확정된 뒤 결과를 통지하기까지 기다리는 시간(초). 실패 연출을 보여줄 여유다.\n" +
        "0이면 즉시 통지한다. 통지 직후에는 MiniGamePlayer 가 화면을 덮고 게임을 거둬가므로,\n" +
        "여기서 벌어주지 않으면 실패 연출이 화면에 뜨기도 전에 사라진다")]
    [SerializeField] private float failEffectDelay = 0.6f;

    [Header("이벤트")]
    [Tooltip("매수를 제때 맞혔을 때. 차트의 매수 버튼을 반짝이게 하는 데 쓴다")]
    public UnityEvent onBuySuccess;

    [Tooltip("매도를 제때 맞혔을 때")]
    public UnityEvent onSellSuccess;

    public UnityEvent onClear;
    public UnityEvent onFail;

    [Tooltip("판이 시작되거나 초기화될 때 발화. 튀어나와 있던 연출을 거두는 데 쓴다 (MoneyBurst.StopBurst 등)")]
    public UnityEvent onResetEffects;

    // 각 봉이 자라기 시작하는 시각(초). 시퀀스 설정에서 누적해 만든다.
    private float[] startTimes;

    // 각 봉을 제때 맞혔는지.
    private bool[] answered;

    // 지금 판정 중인 봉의 번호. 봉이 다 자라 창이 닫히면 다음으로 넘어간다.
    private int cursor;

    // 실패가 확정됐지만 연출을 보여주느라 아직 통지하지 않은 상태.
    private bool failPending;
    private Coroutine failRoutine;

    /// <summary>게임을 시작한다. 호출 시점에 타이머는 이미 0으로 초기화돼 있다.</summary>
    protected override void OnTimedPlay()
    {
        BuildSchedule();
        ResetSequence();
    }

    /// <summary>게임을 중단하고 초기 상태로 되돌린다. 멱등하다.</summary>
    protected override void OnTimedStopAndReset()
    {
        ResetSequence();
    }

    /// <summary>완성된 봉이 미리 보이지 않도록 숨겨만 둔다. 게임 진행은 시작하지 않는다.</summary>
    private void Awake()
    {
        // 프리팹이 켜져 있는 동안 완성된 봉이 미리 보이지 않도록 숨겨만 둔다. 게임 진행은 시작하지 않는다.
        HideAllCandles();
    }

    /// <summary>매 프레임 입력과 진행을 처리한다. 결과가 확정된 뒤에는 불리지 않는다.</summary>
    protected override void OnTimedUpdate()
    {
        // 실패 연출 중에는 봉도 입력도 그 자리에 멈춰 있어야 한다.
        if (failPending)
            return;

        float now = Elapsed;

        UpdateCandles(now);
        AdvanceWindows(now);

        // 위에서 성패가 갈렸으면 이번 프레임 입력은 받지 않는다.
        if (ResultLocked)
            return;

        HandleInput(now);
    }

    /// <summary>제한 시간 초과. 시퀀스가 끝나기 전에 시간이 다 찬 경우라 실패다.</summary>
    protected override void OnTimeUp()
    {
        // 실패 연출을 기다리는 중에 시간이 다 찬 경우라면 연출은 이미 재생 중이다. 두 번 띄우지 않는다.
        if (!failPending)
            onFail.Invoke();

        base.OnTimeUp();
    }

    /// <summary>봉별 자라는 시간과 간격을 누적해 등장 시각표를 만들고, 제한 시간을 맞춘다.</summary>
    private void BuildSchedule()
    {
        int count = steps != null ? steps.Length : 0;

        if (startTimes == null || startTimes.Length != count)
        {
            startTimes = new float[count];
            answered = new bool[count];
        }

        float cursorTime = 0f;
        for (int i = 0; i < count; i++)
        {
            startTimes[i] = cursorTime;
            cursorTime += Mathf.Max(0f, steps[i].growDuration) + Mathf.Max(0f, steps[i].gapAfter);
        }

        // 마지막 봉 뒤의 gapAfter 는 클리어 여유로 쓰지 않고, tailDelay 로 따로 준다.
        float total = SequenceEnd + Mathf.Max(0f, tailDelay);

        if (autoFitTimeLimit)
        {
            timeLimit = total;
            return;
        }

        if (timeLimit < total)
            Debug.LogWarning($"{name}: 제한 시간({timeLimit}s)이 봉 시퀀스({total}s)보다 짧아서 클리어할 수 없다", this);
    }

    /// <summary>마지막 봉이 다 자라는 시각(초).</summary>
    private float SequenceEnd => steps.Length > 0 ? EndTime(steps.Length - 1) : 0f;

    /// <summary>index 번째 봉이 다 자라는 시각(초).</summary>
    private float EndTime(int index)
    {
        return startTimes[index] + Mathf.Max(0f, steps[index].growDuration);
    }

    /// <summary>진행 위치와 정답 기록, 예약된 실패 처리를 처음으로 되돌린다.</summary>
    private void ResetSequence()
    {
        cursor = 0;

        if (answered != null)
            Array.Clear(answered, 0, answered.Length);

        failPending = false;

        if (failRoutine != null)
        {
            StopCoroutine(failRoutine);
            failRoutine = null;
        }

        HideAllCandles();

        // 튀어나와 있던 동전/지폐와 실패 연출을 거둔다. 다음 판이 지난 판의 잔해 위에서 시작하면 안 된다.
        onResetEffects.Invoke();
    }

    /// <summary>모든 봉을 감춘다.</summary>
    private void HideAllCandles()
    {
        if (steps == null)
            return;

        foreach (CandleStep step in steps)
        {
            if (step != null && step.candle != null)
                step.candle.Hide();
        }
    }

    /// <summary>경과 시간에 맞춰 봉들을 그린다. 아직 차례가 아닌 봉은 진행도 0이라 보이지 않는다.</summary>
    private void UpdateCandles(float now)
    {
        for (int i = 0; i < steps.Length; i++)
        {
            CandleStep step = steps[i];
            if (step == null || step.candle == null)
                continue;

            float grow = Mathf.Max(0f, step.growDuration);
            float progress = grow > 0f
                ? (now - startTimes[i]) / grow
                : (now >= startTimes[i] ? 1f : 0f);

            step.candle.Show(Mathf.Clamp01(progress));
        }
    }

    /// <summary>
    /// 다 자란 봉의 입력 창을 닫는다. 못 맞힌 채로 창이 닫히면 그 자리에서 실패다.
    /// 프레임이 튀어 창을 통째로 건너뛰어도 놓치지 않도록 while 로 훑는다.
    /// </summary>
    private void AdvanceWindows(float now)
    {
        while (cursor < steps.Length && now >= EndTime(cursor))
        {
            if (!answered[cursor])
            {
                Fail();
                return;
            }

            cursor++;
        }

        // 마지막 봉까지 다 자란 뒤에야 성공을 확정한다. 그래야 봉이 크는 모습이 잘리지 않는다.
        if (cursor >= steps.Length)
        {
            onClear.Invoke();
            SucceedWhenTimeUp();
        }
    }

    /// <summary>매수·매도 입력을 읽어 지금 자라는 봉의 방향과 맞는지 판정한다.</summary>
    private void HandleInput(float now)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        bool buy = WasPressed(keyboard, buyKey) || WasPressed(keyboard, buyKeyAlt);
        bool sell = WasPressed(keyboard, sellKey) || WasPressed(keyboard, sellKeyAlt);

        if (!buy && !sell)
            return;

        // 봉이 자라는 중이 아닐 때 누른 입력.
        if (cursor >= steps.Length || now < startTimes[cursor])
        {
            if (failOnStrayInput)
                Fail();

            return;
        }

        // 이미 맞힌 봉에 대고 또 누르는 것은 연타로 보고 흘려보낸다.
        if (answered[cursor])
            return;

        bool wantBuy = steps[cursor].kind == CandleKind.Bullish;
        bool correct = buy != sell && buy == wantBuy;

        if (!correct)
        {
            Fail();
            return;
        }

        answered[cursor] = true;

        if (wantBuy)
            onBuySuccess.Invoke();
        else
            onSellSuccess.Invoke();

        // 봉마다 다른 연출(동전/지폐/무더기)을 붙이기 위한 개별 통지.
        // 인스펙터에서 만들어지지 않은 항목은 null 일 수 있다.
        steps[cursor].onSuccess?.Invoke();
    }

    /// <summary>
    /// 실패 연출을 재생하고 결과를 통지한다.
    /// <see cref="failEffectDelay"/> 만큼 통지를 늦춰 연출이 화면에 보일 시간을 벌어준다.
    /// </summary>
    private void Fail()
    {
        if (ResultLocked || failPending)
            return;

        onFail.Invoke();

        if (failEffectDelay <= 0f)
        {
            FailImmediately();
            return;
        }

        failPending = true;
        failRoutine = StartCoroutine(FailAfterDelayRoutine());
    }

    /// <summary>실패 연출을 잠깐 보여준 뒤 실패로 통지한다.</summary>
    private IEnumerator FailAfterDelayRoutine()
    {
        yield return new WaitForSeconds(failEffectDelay);

        failRoutine = null;
        failPending = false;
        FailImmediately();
    }

    /// <summary>해당 키가 이번 프레임에 새로 눌렸는지. None 은 항상 false.</summary>
    private static bool WasPressed(Keyboard keyboard, Key key)
    {
        return key != Key.None && keyboard[key].wasPressedThisFrame;
    }
}
