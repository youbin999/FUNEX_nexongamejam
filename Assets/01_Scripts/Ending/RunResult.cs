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

    private readonly List<MiniGameOutcome> outcomes = new List<MiniGameOutcome>();

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

    /// <summary>
    /// 이번 판의 엔딩에 반영할 Change 결과와 Critical 실패 결과. <b>플레이어가 실제로 겪은 재생 순서</b>다.
    /// 시대 순서는 항상 지켜지지만, 같은 시대 안의 순서는 판마다 섞인다(<c>GameFlowController.shuffleWithinEra</c>).
    /// 크레딧 본문과 LLM 프롬프트는 이 순서를 그대로 읽는다. 조합 키는 <see cref="CanonicalOutcomes"/> 를 쓴다.
    /// </summary>
    public IReadOnlyList<MiniGameOutcome> Outcomes => outcomes;

    /// <summary>핵심 미니게임 실패로 흐름이 중단됐는지 여부.</summary>
    public bool EndedEarly { get; private set; }

    /// <summary>흐름이 중단된 시대(한국어). <see cref="EndedEarly"/> 가 false 면 빈 문자열.</summary>
    public string EndedAtEra { get; private set; } = string.Empty;

    /// <summary>중단을 유발한 사건 이름. <see cref="EndedEarly"/> 가 false 면 빈 문자열.</summary>
    public string EndedByEvent { get; private set; } = string.Empty;

    /// <summary>중단을 유발한 사건의 실패 의미(한국어). <see cref="EndedEarly"/> 가 false 면 빈 문자열.</summary>
    public string EndedFailureMeaning { get; private set; } = string.Empty;


    // ── 수명주기 ──

    /// <summary>싱글턴 자리를 잡고 씬 전환에도 살아남게 한다.</summary>
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


    // ── 결과 기록 ──

    /// <summary>변화 미니게임 결과를 하나 기록한다.</summary>
    public void Record(MiniGameOutcome outcome)
    {
        if (outcome == null)
            return;

        outcomes.Add(outcome);
    }

    /// <summary>핵심 미니게임 실패로 흐름이 중단됐음을 기록한다.</summary>
    public void MarkEarlyEnding(string era, string eventLabel, string failureMeaning = null)
    {
        EndedEarly = true;
        EndedAtEra = era ?? string.Empty;
        EndedByEvent = eventLabel ?? string.Empty;
        EndedFailureMeaning = failureMeaning ?? string.Empty;
    }

    /// <summary>새 판을 시작할 때 호출한다. 재시작 시 이전 판 결과가 섞이지 않게 한다.</summary>
    public void Clear()
    {
        outcomes.Clear();
        EndedEarly = false;
        EndedAtEra = string.Empty;
        EndedByEvent = string.Empty;
        EndedFailureMeaning = string.Empty;
    }


    // ── 조합 키 ──

    /// <summary>
    /// 결과들을 <see cref="MiniGameOutcome.sortIndex"/>(= 원본 등록 인덱스) 오름차순으로 정렬한 사본.
    ///
    /// 재생 순서는 시대 안에서 판마다 섞이므로, 조합 키를 재생 순서로 계산하면
    /// 성패가 완전히 같은 판인데도 순서만 달라 다른 세계로 갈린다 — 엔딩 이미지가 매번 다시 생성되고
    /// 갤러리에 같은 세계가 중복으로 쌓인다. 그래서 키 계산만큼은 항상 이 정규 순서를 쓴다.
    /// (완주 판 기준으로 이 순서는 셔플 이전의 고정 순서와 동일하므로, 예전에 쌓인 캐시·갤러리와도 키가 맞는다.)
    /// </summary>
    private List<MiniGameOutcome> CanonicalOutcomes
    {
        get
        {
            var sorted = new List<MiniGameOutcome>(outcomes);
            sorted.Sort((a, b) => a.sortIndex.CompareTo(b.sortIndex));
            return sorted;
        }
    }

    /// <summary>
    /// 변화 미니게임 성공/실패 조합을 비트마스크로 만든다(i번째 비트 = 정규 순서상 i번째 결과의 성공 여부).
    /// 엔딩 이미지 캐시 키이자 갤러리 식별자로 쓴다.
    /// </summary>
    public int CombinationMask
    {
        get
        {
            List<MiniGameOutcome> canonical = CanonicalOutcomes;

            int mask = 0;
            for (int i = 0; i < canonical.Count && i < 31; i++)
            {
                if (canonical[i].success)
                    mask |= 1 << i;
            }

            return mask;
        }
    }

    /// <summary>
    /// 캐시 파일명 등에 쓸 조합 키. 결과 개수까지 포함해야
    /// "3개 중 전부 실패(0)"와 "5개 중 전부 실패(0)"가 충돌하지 않는다.
    ///
    /// 재생 순서가 아니라 <see cref="CanonicalOutcomes"/>(원본 등록 순서) 기준으로 계산하므로,
    /// 시대 내 순서가 섞여도 같은 성패 조합이면 같은 키가 나온다. 덕분에 완주한 판끼리는
    /// 개수와 사건 목록이 같고 성패 패턴만 다르다 — 사실상 <see cref="CombinationMask"/> 가 판을 가른다.
    /// 그래도 사건 목록(<see cref="EventsSignature"/>)을 키에 넣어 두는 이유는,
    /// 핵심 미니게임 실패로 중단된 판들이 서로 다른 시대에서 끊겼는데도
    /// 개수와 패턴이 같아질 수 있어서다. 흐름 구성을 바꿔도 옛 캐시가 딸려 나오지 않는 효과도 있다.
    /// </summary>
    public string CombinationKey =>
        $"{outcomes.Count}_{CombinationMask:X}_{StableHash.Of(EventsSignature):X8}{(EndedEarly ? "_early" : string.Empty)}";

    /// <summary>
    /// 이번 판에 어떤 사건을 겪었는지를 나타내는 문자열. 조합 키의 재료다.
    /// 재생 순서가 아니라 정규 순서(원본 등록 순서)로 이어 붙이므로, 시대 내 셔플에 흔들리지 않는다.
    /// 겪은 사건 <b>집합</b>이 달라지면 다른 판으로 본다.
    /// 중단으로 끝난 판은 중단을 유발한 사건까지 포함해야 마지막 시대가 구분된다.
    /// </summary>
    private string EventsSignature
    {
        get
        {
            var sb = new StringBuilder();
            foreach (MiniGameOutcome o in CanonicalOutcomes)
                sb.Append(o.era).Append(':').Append(o.eventLabel).Append('|');

            if (EndedEarly)
                sb.Append("early:").Append(EndedAtEra).Append(':').Append(EndedByEvent);

            return sb.ToString();
        }
    }


    // ── 프롬프트 직렬화 ──

    /// <summary>LLM 에 넘길 결과 요약을 사람이 읽는 형태로 직렬화한다.</summary>
    public string ToPromptPayload()
    {
        var sb = new StringBuilder();
        sb.AppendLine(EndedEarly
            ? $"[진행 결과] 인류는 {EndedAtEra}에서 '{EndedByEvent}'에 실패해 더 나아가지 못했다."
              + (string.IsNullOrWhiteSpace(EndedFailureMeaning) ? string.Empty : $" 그 의미: {EndedFailureMeaning}")
            : "[진행 결과] 인류는 미래까지 도달했다.");

        if (outcomes.Count == 0)
        {
            sb.AppendLine("[변화 사건] 없음.");
            return sb.ToString();
        }

        sb.AppendLine("[엔딩 반영 사건]");
        foreach (MiniGameOutcome o in outcomes)
        {
            sb.AppendLine($"- 시대: {o.era} / 사건: {o.eventLabel} / 결과: {(o.success ? "성공" : "실패")}");
            sb.AppendLine($"  의미: {o.meaning}");
            sb.AppendLine($"  시각요소(weight {o.visualWeight}): {o.visual}");
        }

        return sb.ToString();
    }
}
