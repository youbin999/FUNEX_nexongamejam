using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 네트워크·API 없이 동작하는 엔딩 대본 생성기.
/// 인스펙터에 적어둔 meaning/visual 조각을 그대로 이어붙인다 — 문장이 매끄럽지는 않지만
/// 어떤 상황에서도 엔딩이 완주된다는 것을 보장한다. LLM 생성기의 최종 폴백이자,
/// LLM 배선 전에 크레딧 롤을 먼저 완성하기 위한 기준 구현이기도 하다.
/// </summary>
public class FallbackEndingNarrator : IEndingNarrator
{
    public IEnumerator Generate(RunResult result, Action<EndingScript> onDone, Action<string> onFail)
    {
        onDone(Build(result));
        yield break;
    }

    /// <summary>코루틴 없이 즉시 대본을 만든다. LLM 실패 시 다른 생성기에서도 직접 호출한다.</summary>
    public static EndingScript Build(RunResult result)
    {
        var lines = new List<string>();

        lines.Add("빅뱅으로부터 백삼십팔억 년.");
        lines.Add("우주는 팽창했고, 별이 타올랐고, 그 잔해에서 우리가 태어났다.");
        lines.Add(string.Empty);

        IReadOnlyList<MiniGameOutcome> outcomes = result.Outcomes;
        string lastEra = null;

        foreach (MiniGameOutcome o in outcomes)
        {
            if (o.era != lastEra)
            {
                if (lastEra != null)
                    lines.Add(string.Empty);

                lines.Add($"— {o.era} —");
                lastEra = o.era;
            }

            if (!string.IsNullOrWhiteSpace(o.meaning))
                lines.Add(o.meaning);
        }

        lines.Add(string.Empty);

        string epilogue;
        if (result.EndedEarly)
        {
            lines.Add($"그리고 인류는 {result.EndedAtEra}에서 걸음을 멈췄다.");

            if (!string.IsNullOrWhiteSpace(result.EndedFailureMeaning))
                lines.Add(result.EndedFailureMeaning);

            lines.Add("그 다음의 역사는, 끝내 쓰이지 않았다.");
            epilogue = "2026년. 인류는 아직 그곳에 있다.";
        }
        else
        {
            lines.Add("그리하여 인류는 미래에 도달했다.");
            lines.Add("이것이 당신이 만든 역사다.");
            epilogue = "2026년. 우리는 이런 세계에 살고 있다.";
        }

        return new EndingScript
        {
            credit_lines = lines.ToArray(),
            epilogue = epilogue,
            image_prompt = BuildImagePrompt(result),
        };
    }

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
        sb.Append("A single photorealistic cinematic scene of present-day 2026 that contains all of the following: ");

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
        "No text, no watermark, no collage, no split panels. Avoid graphic violence or gore.";
}
