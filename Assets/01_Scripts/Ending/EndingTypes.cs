using System;
using System.Collections.Generic;

/// <summary>
/// 미니게임의 서사적 성격. 기획서의 미니게임 3분류에 대응한다.
/// </summary>
public enum MiniGameKind
{
    /// <summary>일반 미니게임. 실패해도 흐름과 엔딩에 영향이 없다.</summary>
    Normal,

    /// <summary>변화 미니게임. 실패해도 흐름은 이어지지만 엔딩 서사와 엔딩 이미지에 반영된다.</summary>
    Change,

    /// <summary>핵심 미니게임. 실패하면 즉시 엔딩으로 이동한다(기존 isCritical).</summary>
    Critical,
}

/// <summary>
/// 시대(스테이지) 구분.
/// 값(정수)이 곧 직렬화 데이터이므로 <b>순서를 바꾸거나 중간에 끼워 넣지 말 것</b> —
/// 씬에 저장된 <c>era:</c> 숫자가 통째로 밀린다. 이름만 바꾸는 것은 안전하다.
/// </summary>
public enum Era
{
    BigBang,
    Prehistoric,

    /// <summary>청동기 시대. 상형 문자·청동기·부력이 여기 묶여 있다(옛 이름: Greek).</summary>
    BronzeAge,

    Medieval,
    Modern,
    Contemporary,
    Future,
}

/// <summary>
/// <see cref="Era"/> 를 프롬프트/자막에 쓸 한국어 이름으로 바꾼다.
///
/// <b>여기 적힌 이름이 곧 플레이어가 엔딩 크레딧에서 읽는 시대명이고, Gemini 프롬프트에 실리는 시대명이다.</b>
/// 화면에 뜨는 시대 자막은 <c>GameFlowController.GameEntry.eraYearText</c> 가 따로 들고 있으므로,
/// 둘 중 하나만 고치면 "화면에는 청동기 시대, 크레딧에는 고대 그리스"처럼 어긋난다.
/// 아래 값은 현재 <c>00000_Player.unity</c> 의 자막과 일치시켜 둔 것이다 — 한쪽을 바꾸면 반드시 같이 바꾼다.
/// </summary>
public static class EraNames
{
    private static readonly Dictionary<Era, string> Korean = new Dictionary<Era, string>
    {
        { Era.BigBang, "빅뱅" },
        { Era.Prehistoric, "석기시대" },
        { Era.BronzeAge, "청동기 시대" },
        { Era.Medieval, "중세 시대" },
        { Era.Modern, "근대 시대" },
        { Era.Contemporary, "현대 시대" },
        { Era.Future, "미래 시대" },
    };

    public static string ToKorean(this Era era)
        => Korean.TryGetValue(era, out string name) ? name : era.ToString();
}

/// <summary>
/// 변화 미니게임 하나의 결과. 엔딩 프롬프트를 만드는 최소 단위다.
/// </summary>
[Serializable]
public class MiniGameOutcome
{
    /// <summary>사건 이름. 예: "마녀사냥에서 마녀를 색출"</summary>
    public string eventLabel;

    /// <summary>이 사건이 속한 시대(한국어).</summary>
    public string era;

    /// <summary>클리어 여부.</summary>
    public bool success;

    /// <summary>결과에 따라 선택된 역사적 의미(한국어). 크레딧 텍스트의 재료.</summary>
    public string meaning;

    /// <summary>결과에 따라 선택된 시각 요소(영문). 엔딩 이미지 프롬프트의 재료.</summary>
    public string visual;

    /// <summary>서사 비중(0~10). 높을수록 이미지 전경에 배치하도록 유도한다.</summary>
    public int visualWeight;
}

/// <summary>
/// LLM(또는 폴백)이 만들어낸 엔딩 대본. 크레딧 롤과 이미지 생성이 이걸 소비한다.
/// JsonUtility 로 파싱하므로 필드 이름이 곧 JSON 키다 — 이름을 바꾸면 스키마도 같이 바꿔야 한다.
/// </summary>
[Serializable]
public class EndingScript
{
    /// <summary>스타워즈풍으로 흘러갈 크레딧 본문. 한 줄이 화면 한 줄에 대응한다.</summary>
    public string[] credit_lines;

    /// <summary>롤이 끝난 뒤 화면 중앙에 남길 마무리 한 문장.</summary>
    public string epilogue;

    /// <summary>엔딩 이미지 생성용 영문 프롬프트. 모든 변화 미니게임 요소가 융합돼 있어야 한다.</summary>
    public string image_prompt;

    /// <summary>최소한의 유효성 검사. LLM 응답이 비어 있으면 폴백으로 넘겨야 한다.</summary>
    public bool IsValid =>
        credit_lines != null && credit_lines.Length > 0 && !string.IsNullOrWhiteSpace(image_prompt);
}
