using UnityEngine;

/// <summary>
/// 이번 판이 만들어 낸 세계가 어디쯤에 있는지. 변화 미니게임의 성패 비율로 정한다.
///
/// 이름은 내부 표기일 뿐이고 <b>플레이어에게 이 단어들이 보이면 안 된다</b> —
/// 크레딧은 '암울한', '성공적인' 같은 형용사 대신 그 세계의 상태를 묘사해서 정조를 전달한다.
/// </summary>
public enum EndingTone
{
    /// <summary>대부분이 이루어지지 않았다.</summary>
    Ruined,

    /// <summary>유지는 되지만 나아가지는 못한다.</summary>
    Struggling,

    /// <summary>이룬 것과 이루지 못한 것이 반씩 섞여 있다.</summary>
    Mixed,

    /// <summary>대부분이 자리를 잡았다.</summary>
    Rising,

    /// <summary>이룬 것이 당연해질 만큼 이루어졌다.</summary>
    Flourishing,
}

/// <summary>
/// 성패 비율에서 <see cref="EndingTone"/> 을 뽑는 규칙.
///
/// 5단계로 나누는 이유는 이분법의 절벽을 피하기 위해서다 —
/// 한 판 차이로 세계가 정반대가 되면 인과가 아니라 동전 던지기로 읽힌다.
///
/// <see cref="NarratorEra"/>(문체)와는 <b>직교하는 축</b>이다.
/// 시대는 '어떻게 쓰는지'를, 정조는 '무엇을 골라 어떻게 보여주는지'를 정한다.
/// </summary>
public static class EndingToneRule
{
    /// <summary>이번 판의 정조를 정한다.</summary>
    public static EndingTone Resolve(RunResult result)
    {
        if (result == null)
            return EndingTone.Mixed;

        int total = 0;
        int success = 0;

        foreach (MiniGameOutcome o in result.Outcomes)
        {
            total++;
            if (o.success)
                success++;
        }

        // 기록이 하나도 없으면 판단할 재료가 없다. 가운데에서 시작한다.
        EndingTone tone = total == 0
            ? EndingTone.Mixed
            : FromRatio((float)success / total);

        // 핵심 미니게임 실패로 끊긴 판은 한 단계 내린다.
        // 잘하다 끊긴 판과 모두 망치고 끊긴 판이 구분돼야 한다 — 전자는 아까운 미완, 후자는 참담.
        if (result.EndedEarly)
            tone = Darken(tone);

        return tone;
    }

    /// <summary>성공 비율(0~1)을 5단계로 자른다.</summary>
    private static EndingTone FromRatio(float ratio)
    {
        if (ratio < 0.2f) return EndingTone.Ruined;
        if (ratio < 0.4f) return EndingTone.Struggling;
        if (ratio < 0.6f) return EndingTone.Mixed;
        if (ratio < 0.8f) return EndingTone.Rising;
        return EndingTone.Flourishing;
    }

    /// <summary>한 단계 어둡게. 가장 아래에서는 더 내려가지 않는다.</summary>
    private static EndingTone Darken(EndingTone tone)
        => tone == EndingTone.Ruined ? tone : (EndingTone)((int)tone - 1);
}
