using System;

// Gemini generateContent 응답을 JsonUtility 로 파싱하기 위한 DTO 모음.
// 필드 이름이 곧 JSON 키이므로 이름을 바꾸면 파싱이 깨진다.
// 텍스트 생성과 이미지 생성이 같은 응답 구조를 쓰므로 한 곳에 모아둔다.

[Serializable]
public class GeminiResponse
{
    public GeminiCandidate[] candidates;
    public GeminiPromptFeedback promptFeedback;

    /// <summary>첫 후보의 파트 배열. 없으면 null.</summary>
    public GeminiPart[] FirstParts =>
        candidates != null && candidates.Length > 0 && candidates[0].content != null
            ? candidates[0].content.parts
            : null;

    /// <summary>첫 후보의 종료 사유. 안전 필터 차단 등을 진단할 때 쓴다.</summary>
    public string FirstFinishReason =>
        candidates != null && candidates.Length > 0 ? candidates[0].finishReason : null;
}

[Serializable]
public class GeminiCandidate
{
    public GeminiContent content;
    public string finishReason;
}

[Serializable]
public class GeminiContent
{
    public GeminiPart[] parts;
}

[Serializable]
public class GeminiPart
{
    /// <summary>텍스트 응답. 이미지 파트에서는 비어 있다.</summary>
    public string text;

    /// <summary>이미지 등 바이너리 응답(base64). 텍스트 파트에서는 null.</summary>
    public GeminiInlineData inlineData;
}

[Serializable]
public class GeminiInlineData
{
    public string mimeType;

    /// <summary>base64 로 인코딩된 바이너리 본문.</summary>
    public string data;
}

[Serializable]
public class GeminiPromptFeedback
{
    /// <summary>프롬프트가 통째로 차단된 경우의 사유. 정상 응답에서는 비어 있다.</summary>
    public string blockReason;
}
