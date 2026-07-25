using System.Collections;
using UnityEngine;

/// <summary>
/// 미니게임 실패 후 한 판 동안 유지되는 패널티 프리팹의 공통 베이스.
/// <see cref="Apply"/>가 끝날 때까지 다음 미니게임 진행을 보류하며,
/// 인스턴스 자체는 <see cref="FailurePenaltyController.ClearAll"/> 전까지 유지된다.
/// </summary>
public abstract class FailurePenalty : MonoBehaviour
{
    /// <summary>
    /// 최초 실패 연출을 재생한다. 다음 미니게임으로 넘어가도 되는 시점에 종료한다.
    /// </summary>
    public abstract IEnumerator Apply();
}
