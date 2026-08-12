using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 빅뱅 미니게임.
/// W(또는 위 방향키)를 꾹 누르고 있으면 온도가 오르고, 떼면 천천히 내려간다.
/// 온도가 끝까지 차오르면 클리어, 0 까지 식으면 실패다.
/// 온도 값은 그대로 <see cref="BigBangVisual"/> 의 진행도로 전달되어 빅뱅 영상을 앞뒤로 움직인다.
/// 프리팹으로 만들어 <see cref="MiniGamePlayer"/> 가 재생한다.
/// </summary>
public class BigBangMiniGame : MiniGame
{
    // 인스펙터에 확실히 노출되도록 제네릭 UnityEvent 는 구체 타입으로 선언한다.
    [Serializable] public class HeatEvent : UnityEvent<float> { }

    private enum State
    {
        Idle,
        Playing,
        Cleared,
        Failed,
    }

    [Header("연출")]
    [Tooltip("온도를 진행도로 받아 빅뱅 영상을 스크럽하는 컴포넌트")]
    [SerializeField] private BigBangVisual visual;

    [Header("규칙")]
    [Tooltip("게임 시작 시의 온도(0~1). 여기서부터 오르내린다")]
    [SerializeField, Range(0f, 1f)] private float startHeat = 0.5f;

    [Tooltip("누르고 있는 동안 초당 오르는 온도")]
    [SerializeField] private float heatRisePerSecond = 0.3f;

    [Tooltip("떼고 있는 동안 초당 내려가는 온도")]
    [SerializeField] private float heatFallPerSecond = 0.15f;

    [Header("이벤트")]
    [Tooltip("0에서 1로 차오르는 온도계 값. 온도계 Image.fillAmount 에 그대로 연결하면 된다")]
    public HeatEvent onHeatChanged;

    public UnityEvent onClear;
    public UnityEvent onFail;

    private float heat;
    private State state = State.Idle;

    /// <summary>현재 온도(0~1).</summary>
    public float Heat => heat;

    /// <summary>키를 누르면 열이 오르고 떼면 식는다. 가득 차면 클리어, 바닥나면 실패다.</summary>
    private void Update()
    {
        if (state != State.Playing)
            return;

        float delta = IsHeatKeyPressed() ? heatRisePerSecond : -heatFallPerSecond;
        heat = Mathf.Clamp01(heat + delta * Time.deltaTime);

        onHeatChanged.Invoke(heat);

        if (visual != null)
            visual.SetProgress(heat);

        if (heat >= 1f)
        {
            state = State.Cleared;
            onClear.Invoke();
            ReportFinished(true);
            return;
        }

        if (heat <= 0f)
        {
            state = State.Failed;
            onFail.Invoke();
            ReportFinished(false);
        }
    }

    /// <summary>게임을 시작한다. 시작 온도에서 출발해 Playing 으로 전환한다.</summary>
    protected override void OnPlay()
    {
        ResetInternal();

        if (visual != null)
        {
            visual.Begin();
            visual.SetProgress(heat);
        }

        state = State.Playing;
    }

    /// <summary>게임을 강제 중단하고 초기 상태(Idle)로 되돌린다.</summary>
    protected override void OnStopAndReset()
    {
        ResetInternal();
        state = State.Idle;
    }

    /// <summary>온도와 영상 위치를 처음으로 되돌린다.</summary>
    private void ResetInternal()
    {
        heat = Mathf.Clamp01(startHeat);
        onHeatChanged.Invoke(heat);

        if (visual != null)
            visual.ResetVisual();
    }

    /// <summary>온도를 올리는 키(W / 위 방향키)를 누르고 있는지.</summary>
    private bool IsHeatKeyPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        return keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
    }
}
