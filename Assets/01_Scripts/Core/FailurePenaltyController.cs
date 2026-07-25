using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 실패 패널티를 생성하고 현재 한 판 동안 보존하는 씬 수명 관리자.
/// 새 게임 흐름을 시작할 때 <see cref="ClearAll"/>로 누적 패널티를 제거한다.
/// </summary>
public sealed class FailurePenaltyController : MonoBehaviour
{
    [Header("패널티 배치")]
    [Tooltip("생성한 패널티 인스턴스의 부모. 비워두면 이 컴포넌트의 Transform을 사용한다.")]
    [SerializeField] private Transform penaltyRoot;

    private readonly List<FailurePenalty> activePenalties = new List<FailurePenalty>();

    /// <summary>
    /// 패널티를 생성해 최초 연출을 재생한다.
    /// 연출이 끝나면 onCompleted를 호출하며 생성된 패널티는 계속 유지한다.
    /// </summary>
    /// <returns>패널티 적용을 시작했으면 true, 프리팹이 없으면 false.</returns>
    public bool Apply(FailurePenalty prefab, Action onCompleted)
    {
        if (prefab == null)
            return false;

        Transform parent = penaltyRoot != null ? penaltyRoot : transform;
        FailurePenalty instance = Instantiate(prefab, parent);
        activePenalties.Add(instance);
        StartCoroutine(ApplyRoutine(instance, onCompleted));
        return true;
    }

    /// <summary>현재 한 판에 누적된 모든 패널티와 진행 중인 최초 연출을 제거한다.</summary>
    public void ClearAll()
    {
        StopAllCoroutines();

        for (int i = 0; i < activePenalties.Count; i++)
        {
            FailurePenalty penalty = activePenalties[i];
            if (penalty != null)
                Destroy(penalty.gameObject);
        }

        activePenalties.Clear();
    }

    private IEnumerator ApplyRoutine(FailurePenalty instance, Action onCompleted)
    {
        if (instance != null)
        {
            IEnumerator routine = instance.Apply();
            if (routine != null)
                yield return routine;
        }

        onCompleted?.Invoke();
    }
}
