using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 제한 시간이 있는 미니게임의 공통 베이스.
/// 와리오웨어류 타이밍 규칙을 강제한다:
/// - 성공은 확정되어도 즉시 끝나지 않는다. 입력만 잠근 채 제한 시간이 다 찰 때까지 기다렸다가 통지된다.
/// - 실패(제한 시간 초과 또는 잘못된 조작)는 남은 시간과 무관하게 바로 통지된다.
/// 파생 클래스는 <see cref="OnPlay"/>/<see cref="OnStopAndReset"/>/<see cref="Update"/> 를 직접 건드리지 않고
/// <see cref="OnTimedPlay"/>/<see cref="OnTimedStopAndReset"/>/<see cref="OnTimedUpdate"/> 세 훅만 구현한다.
/// </summary>
public abstract class TimedMiniGame : MiniGame
{
    [Serializable] public class TimerEvent : UnityEvent<float> { }

    [Header("제한시간")]
    [Tooltip("제한 시간(초)")]
    [SerializeField] protected float timeLimit = 5f;

    [Header("이벤트")]
    [Tooltip("0에서 1로 차오르는 게이지 값. Image.fillAmount 에 그대로 연결하면 된다")]
    public TimerEvent onTimerChanged;

    private float elapsed;
    private bool resultLocked;
    private bool successPending;

    /// <summary>경과 시간(초). 0 ~ <see cref="timeLimit"/> 범위로 클램프된다.</summary>
    protected float Elapsed => elapsed;

    /// <summary>성공/실패가 이미 확정되어 더 이상 입력을 받지 않는 상태인지.</summary>
    protected bool ResultLocked => resultLocked;

    protected sealed override void OnPlay()
    {
        ResetTimer();
        OnTimedPlay();
    }

    protected sealed override void OnStopAndReset()
    {
        ResetTimer();
        OnTimedStopAndReset();
    }

    private void ResetTimer()
    {
        elapsed = 0f;
        resultLocked = false;
        successPending = false;
        onTimerChanged.Invoke(0f);
    }

    private void Update()
    {
        if (!IsPlaying)
            return;

        // 결과 확정 여부와 무관하게 시간은 항상 흐른다 — 성공 연출 중이라고 멈추지 않는다.
        elapsed = Mathf.Min(elapsed + Time.deltaTime, timeLimit);
        onTimerChanged.Invoke(timeLimit > 0f ? elapsed / timeLimit : 1f);

        if (successPending)
        {
            if (elapsed >= timeLimit)
                ReportFinished(true);
            return;
        }

        if (resultLocked)
            return;

        if (elapsed >= timeLimit)
        {
            resultLocked = true;
            OnTimeUp();
            return;
        }

        OnTimedUpdate();
    }

    /// <summary>파생 클래스의 재생 시작 처리. 호출 시점에 타이머는 이미 0으로 초기화돼 있다.</summary>
    protected abstract void OnTimedPlay();

    /// <summary>파생 클래스의 초기화 처리. 호출 시점에 타이머는 이미 0으로 리셋돼 있다.</summary>
    protected abstract void OnTimedStopAndReset();

    /// <summary>매 프레임 입력/로직 처리. 결과가 확정되기 전까지만 호출된다.</summary>
    protected abstract void OnTimedUpdate();

    /// <summary>
    /// 제한 시간을 다 써서 실패로 확정될 때 호출된다. 기본 구현은 바로 ReportFinished(false).
    /// 실패 연출(onFail 이벤트 등)이 필요하면 재정의해서 처리한 뒤 base.OnTimeUp() 을 호출한다.
    /// </summary>
    protected virtual void OnTimeUp()
    {
        ReportFinished(false);
    }

    /// <summary>
    /// 잘못된 조작으로 즉시 실패시킨다. 남은 시간과 무관하게 바로 끝난다(와리오웨어 스타일).
    /// 실패 애니메이션이 필요하면 이 호출 전에 파생 클래스에서 재생하면 된다.
    /// </summary>
    protected void FailImmediately()
    {
        if (resultLocked)
            return;

        resultLocked = true;
        ReportFinished(false);
    }

    /// <summary>
    /// 성공 조건을 달성했을 때 호출한다. 즉시 끝나지 않고 입력만 잠근 채
    /// 제한 시간이 다 찰 때까지 기다렸다가 ReportFinished(true) 가 호출된다.
    /// 그 사이에 성공 연출(코루틴 등)을 자유롭게 재생하면 된다.
    /// </summary>
    protected void SucceedWhenTimeUp()
    {
        if (resultLocked)
            return;

        resultLocked = true;
        successPending = true;
    }
}
