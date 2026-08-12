using System;

/// <summary>
/// 이번 판의 크레딧을 "누가 썼는지"를 정하는 규칙.
///
/// 크레딧은 살아남은 세계가 자기 역사를 직접 기록한 문서라는 설정이다.
/// 그래서 완주한 판은 미래의 기록자가 쓰고, 핵심 미니게임 실패로 끊긴 판은 끊긴 그 시대의 기록자가 쓴다 —
/// 문체가 곧 그 판의 도달점을 말해 준다.
///
/// LLM 생성기(<see cref="GeminiEndingNarrator"/>)와 폴백(<see cref="FallbackEndingNarrator"/>)이
/// 같은 화자를 골라야 하므로 규칙을 여기 한 곳에 둔다.
/// </summary>
public static class NarratorEra
{
    /// <summary>
    /// 이번 판의 기록자가 속한 시대를 고른다.
    ///
    /// <see cref="RunResult"/> 는 시대를 한국어 이름으로만 들고 있으므로 <see cref="EraNames"/> 를
    /// 기준으로 되짚는다. 여기에 이름 리터럴을 다시 적지 않아야 시대명이 바뀌어도 따라간다.
    /// </summary>
    public static Era Resolve(RunResult result)
    {
        if (result == null || !result.EndedEarly)
            return Era.Future;

        foreach (Era candidate in (Era[])Enum.GetValues(typeof(Era)))
        {
            if (candidate.ToKorean() == result.EndedAtEra)
                return candidate;
        }

        return Era.Future;
    }
}
