using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 페니실린 실패 패널티. 항생제를 얻지 못한 세계라, 판이 끝날 때까지 일정 간격으로 기침이 들린다.
///
/// 화면을 가리지 않고 소리로만 남는 패널티다 — 난이도를 올리는 대신 "이 세계에는 항생제가 없다"를
/// 계속 상기시키는 쪽에 가깝다. 다른 패널티(시야 방해)와 겹쳐도 서로 잡아먹지 않는다.
///
/// <see cref="Apply"/> 는 첫 기침 한 번만 들려주고 끝난다(계약 2.2) — 그 뒤로는 코루틴이
/// 알아서 반복하므로 다음 미니게임이 곧바로 진행된다.
/// 인스턴스는 스스로 지우지 않는다. 새 판을 시작할 때 <c>FailurePenaltyController</c> 가 정리한다.
///
/// 대기에 <c>WaitForSeconds</c>(스케일 시간)를 쓰는 것은 의도다 —
/// 설정 창(ESC)으로 게임을 멈추면 기침도 같이 멈춰야 한다.
/// </summary>
public sealed class CoughFailurePenalty : FailurePenalty
{
    [Header("소리")]
    [Tooltip("기침 소리를 낼 SfxPlayer. 비워두면 이 오브젝트와 자식에서 자동으로 찾는다.\n" +
        "SfxPlayer 의 clips 에 기침 변형을 3~4개 넣어 두면 매번 다른 소리가 나 단조로워지지 않는다")]
    [SerializeField] private SfxPlayer coughSfx;

    [Header("간격")]
    [Tooltip("기침 사이의 간격(초)")]
    [Min(0.1f)]
    [SerializeField] private float interval = 5f;

    [Tooltip("간격을 이만큼 무작위로 흔든다(초). 0이면 정확히 interval 마다 기침한다.\n" +
        "0.5 정도 주면 기계적으로 반복되는 느낌이 줄어든다")]
    [Min(0f)]
    [SerializeField] private float intervalJitter = 0f;

    [Header("최초 연출")]
    [Tooltip("실패 직후 첫 기침을 들려주고 다음 미니게임으로 넘어가기까지 붙잡는 시간(초).\n" +
        "기침 클립 길이 정도로 잡는다. 너무 길면 게임 사이가 늘어진다")]
    [Min(0f)]
    [SerializeField] private float introHoldDuration = 0.9f;

    [Header("이벤트")]
    [Tooltip("기침할 때마다 발화. 소리 말고 다른 연출(화면 흔들림 등)을 같이 붙이고 싶을 때 쓴다")]
    public UnityEvent onCough;

    private void Awake()
    {
        if (coughSfx == null)
            coughSfx = GetComponentInChildren<SfxPlayer>(true);
    }

    public override IEnumerator Apply()
    {
        // 소리 없는 패널티는 아무 일도 안 일어난 것과 같다 — 배선이 빠졌으면 바로 알려준다.
        if (coughSfx == null)
        {
            Debug.LogWarning($"[{name}] SfxPlayer 가 없어 기침 소리가 나지 않는다. " +
                "패널티 프리팹에 SfxPlayer 를 붙이고 clips 에 기침 클립을 넣는다.", this);
        }

        // 실패한 순간 첫 기침. 이게 들리는 동안만 다음 게임을 붙잡는다.
        Cough();

        if (introHoldDuration > 0f)
            yield return new WaitForSeconds(introHoldDuration);

        // 이후로는 판이 끝날 때까지 알아서 반복한다.
        StartCoroutine(LoopRoutine());
    }

    private IEnumerator LoopRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(NextInterval());
            Cough();
        }
    }

    private void Cough()
    {
        if (coughSfx != null)
            coughSfx.Play();

        onCough.Invoke();
    }

    /// <summary>다음 기침까지의 대기 시간. 흔들림을 줘도 최소 간격 아래로는 내려가지 않는다.</summary>
    private float NextInterval()
    {
        float jitter = intervalJitter > 0f ? Random.Range(-intervalJitter, intervalJitter) : 0f;
        return Mathf.Max(0.1f, interval + jitter);
    }
}
