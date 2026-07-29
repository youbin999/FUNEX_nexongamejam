using UnityEngine;

/// <summary>
/// 미니게임 연출에서 공통으로 쓰는 이징(easing) 곡선 모음.
/// 같은 식을 게임마다 따로 들고 있으면 느낌이 조금씩 어긋나므로 한 곳에 모아 둔다.
///
/// 인자는 0~1 진행도이고, 돌려주는 값도 대체로 0~1이다.
/// <see cref="OutBack"/> 처럼 튀는 곡선은 중간에 1을 넘길 수 있으므로
/// <see cref="Vector3.LerpUnclamped"/> 처럼 범위를 안 자르는 보간과 함께 쓴다.
/// </summary>
public static class Ease
{
    // 튀어나가는 정도. 표준 back-ease 계수다.
    private const float BackOvershoot = 1.70158f;


    // ── 곡선 ──

    /// <summary>
    /// 목표를 살짝 넘겼다가 되돌아오며 정착한다(통통 튀는 느낌).
    /// 팝인 연출처럼 "톡 튀어나오는" 인상을 줄 때 쓴다.
    /// </summary>
    public static float OutBack(float t)
    {
        float p = t - 1f;
        return 1f + (BackOvershoot + 1f) * p * p * p + BackOvershoot * p * p;
    }

    /// <summary>빠르게 시작해 끝에서 감속한다. 튀어나가는 연출에 쓴다.</summary>
    public static float OutQuad(float t)
    {
        float p = 1f - t;
        return 1f - p * p;
    }

    /// <summary>천천히 시작해 갈수록 가속한다. 빨려 들어가는 연출에 쓴다.</summary>
    public static float InQuad(float t) => t * t;
}
