using System;
using System.Collections.Generic;

/// <summary>
/// 갤러리에 영구 보관되는 엔딩 한 편. 크레딧 텍스트와 이미지가 한 쌍으로 묶인다.
/// JsonUtility 로 직렬화하므로 필드 이름이 곧 JSON 키다 — 이름을 바꾸면 기존 저장본을 읽지 못한다.
/// </summary>
[Serializable]
public class GalleryEntry
{
    /// <summary>엔트리 식별자이자 이미지 파일명. 예: 20260725_213044_9A3F1C4B</summary>
    public string id;

    /// <summary>저장 시각(ISO 8601, 로컬). 목록 정렬과 표시에 쓴다.</summary>
    public string savedAt;

    /// <summary>images 폴더 안의 이미지 파일명.</summary>
    public string imageFile;

    /// <summary>크레딧 본문. 엔딩 씬에서 흘러갔던 줄 그대로다.</summary>
    public string[] creditLines;

    /// <summary>롤이 끝난 뒤 화면 중앙에 남았던 마무리 문장.</summary>
    public string epilogue;

    /// <summary>이 판의 조합 키. 같은 판을 다시 겪었는지 판별할 때 쓴다.</summary>
    public string combinationKey;

    /// <summary>핵심 미니게임 실패로 중단된 판인지.</summary>
    public bool endedEarly;

    /// <summary>중단된 시대(한국어). <see cref="endedEarly"/> 가 false 면 빈 문자열.</summary>
    public string endedAtEra;

    /// <summary>저장 시각을 <see cref="DateTime"/> 으로. 파싱할 수 없으면 <see cref="DateTime.MinValue"/>.</summary>
    public DateTime SavedAtTime =>
        DateTime.TryParse(savedAt, out DateTime parsed) ? parsed : DateTime.MinValue;

    /// <summary>목록에 표시할 짧은 날짜 문자열.</summary>
    public string DisplayDate
    {
        get
        {
            DateTime time = SavedAtTime;
            return time == DateTime.MinValue ? string.Empty : time.ToString("yyyy.MM.dd HH:mm");
        }
    }

    /// <summary>상세 화면에 그대로 뿌릴 크레딧 본문.</summary>
    public string CreditBody => creditLines == null ? string.Empty : string.Join("\n", creditLines);
}

/// <summary>
/// 갤러리 인덱스 파일(index.json)의 최상위 구조.
/// JsonUtility 는 배열을 최상위로 직렬화하지 못해 래퍼가 필요하다.
/// </summary>
[Serializable]
public class GalleryDatabase
{
    /// <summary>저장된 엔트리들. 추가 순서가 곧 시간 순서다.</summary>
    public List<GalleryEntry> entries = new List<GalleryEntry>();
}
