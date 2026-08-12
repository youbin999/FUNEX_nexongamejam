using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 네트워크·API 없이 동작하는 엔딩 대본 생성기.
/// 인스펙터에 적어둔 meaning/visual 조각을 이어붙인다 — 문장이 매끄럽지는 않지만
/// 어떤 상황에서도 엔딩이 완주된다는 것을 보장한다. LLM 생성기의 최종 폴백이자,
/// LLM 배선 전에 크레딧 롤을 먼저 완성하기 위한 기준 구현이기도 하다.
///
/// LLM 쪽과 마찬가지로 <see cref="NarratorEra"/> 가 고른 시대의 말투를 따른다.
/// 다만 한계가 있다 — 사건 본문(<c>meaning</c>)은 인스펙터에 적힌 평범한 서술체 그대로라
/// 여기서 말투를 입힐 수 없다. 그래서 <b>도입부·시대 머리글·마무리·에필로그</b>가 문체를 짊어지고,
/// 말이 가장 많이 부서지는 석기·미래 문체에서는 본문 대신 사건 이름(<c>eventLabel</c>)을
/// 짧게 끊어 쓴다. 문장이 통째로 그 시대 말이 되지는 않지만 톤은 확실히 갈린다.
/// </summary>
public class FallbackEndingNarrator : IEndingNarrator
{
    /// <summary>한 시대 말투가 크레딧을 어떻게 짜는지 담아 두는 묶음.</summary>
    private sealed class Voice
    {
        /// <summary>크레딧 맨 앞에 붙는 도입부.</summary>
        public string[] opening;

        /// <summary>시대가 바뀔 때 넣는 머리글. 비우면 머리글을 넣지 않는다.</summary>
        public Func<string, string> eraHeader;

        /// <summary>사건 하나를 크레딧 줄들로 바꾼다. 빈 배열이면 그 사건은 건너뛴다.</summary>
        public Func<MiniGameOutcome, string[]> lines;

        /// <summary>흐름이 중단된 판의 마무리.</summary>
        public Func<RunResult, string[]> closingEarly;

        /// <summary>완주한 판의 마무리.</summary>
        public string[] closingFull;

        public string epilogueEarly;
        public string epilogueFull;

        /// <summary>말이 짧게 부서지는 화자(석기·미래)인지. 정조 문장을 짧은 쪽으로 고르는 데 쓴다.</summary>
        public bool terse;
    }

    /// <summary>
    /// 정조를 한 줄로 보여 주는 문장. 마무리 직전에 끼워 넣는다.
    /// 형용사로 분위기를 선언하지 않고 세계의 상태만 적는다 — LLM 쪽 <c>ToneGuides</c> 와 같은 원칙이다.
    /// </summary>
    private static readonly Dictionary<EndingTone, string> ToneLines = new Dictionary<EndingTone, string>
    {
        [EndingTone.Ruined] = "남은 자리에는 쓰지 않는 것들이 그대로 있다.",
        [EndingTone.Struggling] = "새로 지은 것보다 고쳐 쓴 것이 많다.",
        [EndingTone.Mixed] = "이룬 것과 이루지 못한 것이 같은 거리에 서 있다.",
        [EndingTone.Rising] = "사람들은 다음 세대가 있을 것을 전제로 짓기 시작했다.",
        [EndingTone.Flourishing] = "가장 큰 것은 너무 당연해져서 아무도 말하지 않는다.",
    };

    /// <summary>말이 짧게 부서지는 화자(석기·미래)를 위한 정조 문장.</summary>
    private static readonly Dictionary<EndingTone, string> TerseToneLines = new Dictionary<EndingTone, string>
    {
        [EndingTone.Ruined] = "남은 것. 없다.",
        [EndingTone.Struggling] = "남은 것. 조금.",
        [EndingTone.Mixed] = "있다. 없다. 반씩.",
        [EndingTone.Rising] = "많다. 더. 온다.",
        [EndingTone.Flourishing] = "많다. 당연하다.",
    };

    /// <summary>본문을 그대로 쓰는 시대들이 공유하는 사건 → 줄 변환.</summary>
    private static readonly Func<MiniGameOutcome, string[]> MeaningLines =
        o => string.IsNullOrWhiteSpace(o.meaning) ? Array.Empty<string>() : new[] { o.meaning };

    /// <summary>표준 시대 머리글.</summary>
    private static readonly Func<string, string> DashHeader = era => $"— {era} —";

    /// <summary>
    /// 시대별 말투 표. 문구를 고치고 싶으면 여기만 손보면 된다.
    /// <see cref="GeminiEndingNarrator"/> 의 <c>EraVoices</c> 와 같은 톤을 유지할 것.
    /// </summary>
    private static readonly Dictionary<Era, Voice> Voices = new Dictionary<Era, Voice>
    {
        // 문자가 없다. 단어와 마침표만 남는다.
        [Era.Prehistoric] = new Voice
        {
            opening = new[] { "우리. 여기.", "오래. 전.", string.Empty },
            eraHeader = null,
            lines = o => new[] { $"{o.eventLabel}.", o.success ? "됐다." : "안 됐다." },
            closingEarly = _ => new[] { string.Empty, "우리. 세계. 끝." },
            closingFull = new[] { string.Empty, "우리. 세계. 끝." },
            epilogueEarly = "우리. 아직. 여기.",
            epilogueFull = "우리. 아직. 여기.",
            terse = true,
        },

        // 왕의 서기가 점토판에 새긴 장부.
        [Era.BronzeAge] = new Voice
        {
            opening = new[]
            {
                "왕이 서기에게 명하여 이를 새기게 하였다.",
                "아래는 그 기록이다.",
                string.Empty,
            },
            eraHeader = DashHeader,
            lines = MeaningLines,
            closingEarly = r => new[]
            {
                string.Empty,
                $"{r.EndedAtEra}에 이르러 기록이 그쳤다.",
                "이 뒤로는 새겨진 것이 없다.",
            },
            closingFull = new[] { string.Empty, "서기가 마지막 판을 덮었다." },
            epilogueEarly = "기록은 여기서 그친다.",
            epilogueFull = "이를 돌에 새겨 남긴다.",
        },

        // 수도원 필경사의 연대기.
        [Era.Medieval] = new Voice
        {
            opening = new[]
            {
                "주(主)의 해에, 이 연대기를 적노라.",
                "빛이 있으라 하시매 별이 타올랐고, 그 재에서 사람이 났느니라.",
                string.Empty,
            },
            eraHeader = DashHeader,
            lines = MeaningLines,
            closingEarly = r => new[]
            {
                string.Empty,
                $"인류는 {r.EndedAtEra}에서 걸음을 멈추었더라.",
                "그 다음의 역사는 끝내 쓰이지 아니하였느니라.",
            },
            closingFull = new[] { string.Empty, "이 모든 것이 뜻대로 이루어졌느니라." },
            epilogueEarly = "인류는 아직 그곳에 있느니라.",
            epilogueFull = "이것이 사람이 걸어온 길이니라.",
        },

        // 계몽기의 활판 인쇄물.
        [Era.Modern] = new Voice
        {
            opening = new[]
            {
                "빅뱅으로부터 백삼십팔억 년.",
                "우주는 팽창하였고, 그 잔해에서 인간이 태어났다.",
                string.Empty,
            },
            eraHeader = DashHeader,
            lines = MeaningLines,
            closingEarly = r => new[]
            {
                string.Empty,
                $"그리고 인류는 {r.EndedAtEra}에서 걸음을 멈췄다.",
                "그 다음의 역사는, 끝내 쓰이지 않았다.",
            },
            closingFull = new[]
            {
                string.Empty,
                "이로써 인류는 미래에 도달하였다.",
                "이것이 당신이 만든 역사다.",
            },
            epilogueEarly = "인류는 아직 그곳에 있다.",
            epilogueFull = "우리는 이런 세계에 살고 있다.",
        },

        [Era.Contemporary] = new Voice
        {
            opening = new[]
            {
                "빅뱅으로부터 138억 년 ㅇㅇ",
                "그동안 있었던 일 요약함.",
                string.Empty,
            },
            eraHeader = era => $"— {era} —",
            lines = MeaningLines,
            closingEarly = r => new[]
            {
                string.Empty,
                $"그리고 {r.EndedAtEra}에서 그냥 끝남.",
                "뒷얘기는 없음. ㄹㅇ 여기까지임.",
            },
            closingFull = new[]
            {
                string.Empty,
                "그래서 미래까지 오긴 왔음.",
                "이게 니가 만든 세계임.",
            },
            epilogueEarly = "우리 아직 거기 있음.",
            epilogueFull = "우리 이런 데 살고 있음.",
        },

        // 기계의 자동 기록. 인간은 더 이상 자기 역사를 쓰지 않는다.
        [Era.Future] = new Voice
        {
            opening = new[] { "아카이브 재생.", "대상: 인류.", string.Empty },
            eraHeader = era => $"구간: {era}.",
            lines = o => new[] { $"기록: {o.eventLabel} — {(o.success ? "완료" : "미완")}." },
            closingEarly = r => new[]
            {
                string.Empty,
                $"기록 중단 지점: {r.EndedAtEra}.",
                "이후 데이터 없음.",
                "끝.",
            },
            closingFull = new[]
            {
                string.Empty,
                "동기화 완료.",
                "기록자: 본 기기.",
                "끝.",
            },
            epilogueEarly = "보관됨.",
            epilogueFull = "보관됨.",
            terse = true,
        },
    };

    /// <summary>즉시 폴백 대본을 만들어 넘긴다. 네트워크를 타지 않으므로 실패 경로가 없다.</summary>
    public IEnumerator Generate(RunResult result, Action<EndingScript> onDone, Action<string> onFail)
    {
        onDone(Build(result));
        yield break;
    }


    // ── 대본 조립 ──

    /// <summary>
    /// 코루틴 없이 즉시 대본을 만든다. LLM 실패 시 다른 생성기에서도 직접 호출한다.
    /// 화자(=끝난 시대)의 말투로 도입부 → 시대별 사건 → 마무리를 엮는다.
    /// </summary>
    public static EndingScript Build(RunResult result)
    {
        Voice voice = VoiceFor(NarratorEra.Resolve(result));

        var lines = new List<string>();
        lines.AddRange(voice.opening);

        IReadOnlyList<MiniGameOutcome> outcomes = result.Outcomes;
        string lastEra = null;

        foreach (MiniGameOutcome o in outcomes)
        {
            if (o.era != lastEra)
            {
                if (lastEra != null)
                    lines.Add(string.Empty);

                if (voice.eraHeader != null)
                    lines.Add(voice.eraHeader(o.era));

                lastEra = o.era;
            }

            foreach (string line in voice.lines(o))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    lines.Add(line);
            }
        }

        // 마무리 직전에 이 판이 만든 세계의 상태를 한 줄 끼워 넣는다.
        // 성패 비율이 크레딧에 드러나는 유일한 지점이라, 폴백에서도 판끼리 구분이 된다.
        EndingTone tone = EndingToneRule.Resolve(result);
        Dictionary<EndingTone, string> toneTable = voice.terse ? TerseToneLines : ToneLines;

        if (toneTable.TryGetValue(tone, out string toneLine))
        {
            lines.Add(string.Empty);
            lines.Add(toneLine);
        }

        string epilogue;
        if (result.EndedEarly)
        {
            lines.AddRange(voice.closingEarly(result));
            epilogue = voice.epilogueEarly;
        }
        else
        {
            lines.AddRange(voice.closingFull);
            epilogue = voice.epilogueFull;
        }

        return new EndingScript
        {
            credit_lines = lines.ToArray(),
            epilogue = epilogue,
            image_prompt = BuildImagePrompt(result),
        };
    }

    /// <summary>해당 시대의 말투. 표에 없는 시대(빅뱅 등)는 미래 말투로 떨어진다.</summary>
    private static Voice VoiceFor(Era era)
        => Voices.TryGetValue(era, out Voice voice) ? voice : Voices[Era.Future];

    /// <summary>
    /// 시각 요소를 비중 순으로 이어붙여 이미지 프롬프트를 만든다.
    /// LLM 이 실패해도 이미지 생성은 계속 시도할 수 있게 하기 위한 것이다.
    /// </summary>
    public static string BuildImagePrompt(RunResult result)
    {
        var visuals = new List<MiniGameOutcome>();
        foreach (MiniGameOutcome o in result.Outcomes)
        {
            if (!string.IsNullOrWhiteSpace(o.visual))
                visuals.Add(o);
        }

        // 비중이 높은 것을 앞(전경)에 둔다. 이미지 모델은 앞쪽 구절을 더 강하게 반영한다.
        visuals.Sort((a, b) => b.visualWeight.CompareTo(a.visualWeight));

        var sb = new StringBuilder();
        sb.Append("A single photorealistic cinematic scene that contains all of the following: ");

        for (int i = 0; i < visuals.Count; i++)
        {
            if (i > 0)
                sb.Append("; ");

            sb.Append(visuals[i].visual);
        }

        sb.Append(". ");
        sb.Append(EndingImagePromptStyle.Suffix);
        return sb.ToString();
    }
}

/// <summary>엔딩 이미지의 아트 디렉션을 한 곳에 모아둔다. LLM 프롬프트와 폴백이 같은 톤을 쓰게 한다.</summary>
public static class EndingImagePromptStyle
{
    /// <summary>모든 엔딩 이미지 프롬프트 끝에 붙는 공통 스타일 지시.</summary>
    public const string Suffix =
        "Photorealistic, cinematic wide shot, 16:9, single coherent location and time of day, " +
        "consistent natural lighting, muted desaturated color grade, film grain. " +
        "No text, no watermark, no collage";
}
