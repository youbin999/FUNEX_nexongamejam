using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 횟수가 올라갈 때마다 하나씩 켜지는 불빛들. (켜지는 쪽)
/// <see cref="RubMiniGame.onRub"/> 에 <see cref="SetCount"/> 를 연결하면
/// <c>thresholds</c> 에 적어 둔 횟수를 넘길 때마다 순서대로 하나씩 켜진다.
/// </summary>
public class StepLights : MonoBehaviour
{
    // 인스펙터에 확실히 노출되도록 제네릭 UnityEvent 는 구체 타입으로 선언한다.
    [Serializable] public class LitEvent : UnityEvent<int> { }

    [Header("불빛")]
    [Tooltip("순서대로 켜질 오브젝트들. 재생을 시작하면 전부 꺼진 상태에서 출발한다")]
    [SerializeField] private GameObject[] lights;

    [Tooltip("각 불빛이 켜지는 횟수. lights 와 같은 순서로 넣는다 (예: 3, 6, 10)")]
    [SerializeField] private int[] thresholds = { 3, 6, 10 };

    [Header("이벤트")]
    [Tooltip("불빛이 새로 켜지는 순간 그 번호(0부터)를 넘긴다. 효과음이나 이펙트를 연결한다")]
    public LitEvent onLit;

    // 지금 켜져 있는지. SetCount 가 매 횟수마다 불리므로, 바뀔 때만 처리하려고 들고 있는다.
    private bool[] litState;

    private void Awake()
    {
        EnsureState();
        ResetPose();
    }

    private void EnsureState()
    {
        int count = lights != null ? lights.Length : 0;

        if (litState == null || litState.Length != count)
            litState = new bool[count];
    }

    /// <summary>지금까지의 횟수를 넘긴다. 기준을 넘긴 불빛이 켜진다.</summary>
    public void SetCount(int count)
    {
        EnsureState();

        for (int i = 0; i < litState.Length; i++)
            Apply(i, count >= ThresholdAt(i));
    }

    /// <summary>전부 켠다. 클리어 순간처럼 횟수와 상관없이 다 켜야 할 때 쓴다.</summary>
    public void LightAll()
    {
        EnsureState();

        for (int i = 0; i < litState.Length; i++)
            Apply(i, true);
    }

    /// <summary>전부 끈다. (재시작용)</summary>
    public void ResetPose()
    {
        EnsureState();

        for (int i = 0; i < litState.Length; i++)
            Apply(i, false);
    }

    /// <summary>
    /// index 번째 불빛이 켜지는 기준 횟수.
    /// thresholds 가 lights 보다 짧으면 마지막 값을 그대로 쓴다.
    /// </summary>
    private int ThresholdAt(int index)
    {
        if (thresholds == null || thresholds.Length == 0)
            return int.MaxValue;

        return index < thresholds.Length ? thresholds[index] : thresholds[thresholds.Length - 1];
    }

    private void Apply(int index, bool lit)
    {
        // 상태가 그대로면 아무것도 하지 않는다. 안 그러면 켜져 있는 동안 매 횟수마다 onLit 이 터진다.
        if (litState[index] == lit)
            return;

        litState[index] = lit;

        if (lights[index] != null)
            lights[index].SetActive(lit);

        if (lit)
            onLit.Invoke(index);
    }
}
