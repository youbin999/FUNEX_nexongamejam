using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 한 판(run) 동안의 변화 미니게임 결과를 누적하는 싱글턴.
/// 엔딩 씬으로 넘어가서도 살아 있어야 하므로 DontDestroyOnLoad 로 유지된다.
/// 씬에 미리 배치할 필요는 없다 — <see cref="Instance"/> 접근 시 자동으로 생성된다.
/// </summary>
public class RunResult : MonoBehaviour
{
    private static RunResult instance;

    /// <summary>싱글턴 인스턴스. 없으면 만들어서 반환한다.</summary>
    public static RunResult Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("[RunResult]");
                instance = go.AddComponent<RunResult>();
                DontDestroyOnLoad(go);
            }

            return instance;
        }
    }

    private readonly List<MiniGameOutcome> outcomes = new List<MiniGameOutcome>();

    /// <summary>이번 판에서 플레이한 변화 미니게임 결과들. 등록 순서 = 시대 순서.</summary>
    public IReadOnlyList<MiniGameOutcome> Outcomes => outcomes;

    /// <summary>핵심 미니게임 실패로 흐름이 중단됐는지 여부.</summary>
    public bool EndedEarly { get; private set; }

    /// <summary>흐름이 중단된 시대(한국어). <see cref="EndedEarly"/> 가 false 면 빈 문자열.</summary>
    public string EndedAtEra { get; private set; } = string.Empty;

    /// <summary>중단을 유발한 사건 이름. <see cref="EndedEarly"/> 가 false 면 빈 문자열.</summary>
    public string EndedByEvent { get; private set; } = string.Empty;

    private void Awake()
    {
        // 씬에 직접 배치한 경우의 중복 방지.
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>변화 미니게임 결과를 하나 기록한다.</summary>
    public void Record(MiniGameOutcome outcome)
    {
        if (outcome == null)
            return;

        outcomes.Add(outcome);
    }

    /// <summary>핵심 미니게임 실패로 흐름이 중단됐음을 기록한다.</summary>
    public void MarkEarlyEnding(string era, string eventLabel)
    {
        EndedEarly = true;
        EndedAtEra = era ?? string.Empty;
        EndedByEvent = eventLabel ?? string.Empty;
    }

    /// <summary>새 판을 시작할 때 호출한다. 재시작 시 이전 판 결과가 섞이지 않게 한다.</summary>
    public void Clear()
    {
        outcomes.Clear();
        EndedEarly = false;
        EndedAtEra = string.Empty;
        EndedByEvent = string.Empty;
    }

    /// <summary>
    /// 변화 미니게임 성공/실패 조합을 비트마스크로 만든다(i번째 비트 = i번째 결과의 성공 여부).
    /// 엔딩 이미지 캐시 키이자 갤러리 식별자로 쓴다.
    /// </summary>
    public int CombinationMask
    {
        get
        {
            int mask = 0;
            for (int i = 0; i < outcomes.Count && i < 31; i++)
            {
                if (outcomes[i].success)
                    mask |= 1 << i;
            }

            return mask;
        }
    }

    /// <summary>
    /// 캐시 파일명 등에 쓸 조합 키. 결과 개수까지 포함해야
    /// "3개 중 전부 실패(0)"와 "5개 중 전부 실패(0)"가 충돌하지 않는다.
    /// </summary>
    public string CombinationKey => $"{outcomes.Count}_{CombinationMask:X}{(EndedEarly ? "_early" : string.Empty)}";

    /// <summary>LLM 에 넘길 결과 요약을 사람이 읽는 형태로 직렬화한다.</summary>
    public string ToPromptPayload()
    {
        var sb = new StringBuilder();
        sb.AppendLine(EndedEarly
            ? $"[진행 결과] 인류는 {EndedAtEra}에서 '{EndedByEvent}'에 실패해 더 나아가지 못했다."
            : "[진행 결과] 인류는 미래까지 도달했다.");

        if (outcomes.Count == 0)
        {
            sb.AppendLine("[변화 사건] 없음.");
            return sb.ToString();
        }

        sb.AppendLine("[변화 사건]");
        foreach (MiniGameOutcome o in outcomes)
        {
            sb.AppendLine($"- 시대: {o.era} / 사건: {o.eventLabel} / 결과: {(o.success ? "성공" : "실패")}");
            sb.AppendLine($"  의미: {o.meaning}");
            sb.AppendLine($"  시각요소(weight {o.visualWeight}): {o.visual}");
        }

        return sb.ToString();
    }
}
