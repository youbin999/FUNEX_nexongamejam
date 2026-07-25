using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Gemini API 키를 런타임에 읽어오는 로더.
/// 키를 소스에 하드코딩하지 않기 위해 StreamingAssets 의 외부 파일에서 읽는다
/// (파일 자체는 .gitignore 로 저장소에서 제외한다).
///
/// 로드 우선순위:
///   1. 환경변수 GEMINI_API_KEY (에디터/개발 PC에서 편하다)
///   2. StreamingAssets/gemini_api_key.txt (빌드에 동봉하는 경로)
///
/// 주의: 클라이언트 빌드에 동봉된 키는 추출될 수 있다. 사용량 한도를 낮게 건
/// 잼 전용 키를 쓰고, 제출 후에는 폐기할 것.
/// </summary>
public static class GeminiApiConfig
{
    /// <summary>StreamingAssets 안의 키 파일 이름.</summary>
    public const string KeyFileName = "gemini_api_key.txt";

    private static string cachedKey;
    private static bool loadAttempted;

    /// <summary>로드된 API 키. 아직 로드하지 않았거나 실패했으면 빈 문자열.</summary>
    public static string ApiKey => cachedKey ?? string.Empty;

    /// <summary>사용 가능한 키가 있는지.</summary>
    public static bool HasKey => !string.IsNullOrWhiteSpace(cachedKey);

    /// <summary>
    /// 키를 로드한다. 멱등 — 이미 시도했으면 즉시 반환한다.
    /// StreamingAssets 는 플랫폼에 따라 직접 파일 접근이 안 되므로 UnityWebRequest 로 읽는다.
    /// </summary>
    public static IEnumerator Load()
    {
        if (loadAttempted)
            yield break;

        loadAttempted = true;

        // 1) 환경변수.
        // catch 절이 있는 try 안에서는 yield 할 수 없으므로, 조회만 try 로 감싸고
        // 조기 종료는 블록 밖에서 처리한다.
        string fromEnv = null;
        try
        {
            fromEnv = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        }
        catch (Exception e)
        {
            // 일부 플랫폼에서는 환경변수 접근이 막혀 있다. 조용히 다음 경로로 넘어간다.
            Debug.Log($"GeminiApiConfig: 환경변수 조회 실패 — 파일에서 읽습니다. ({e.Message})");
        }

        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            cachedKey = fromEnv.Trim();
            yield break;
        }

        // 2) StreamingAssets 파일.
        string path = Path.Combine(Application.streamingAssetsPath, KeyFileName);
        string uri = path.Contains("://") ? path : "file://" + path;

        using (UnityWebRequest req = UnityWebRequest.Get(uri))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                cachedKey = StripComments(req.downloadHandler.text);
            }
            else
            {
                Debug.LogWarning(
                    $"GeminiApiConfig: API 키를 찾지 못했습니다 ({req.error}). " +
                    $"엔딩은 폴백 텍스트로 진행됩니다.\n" +
                    $"해결: 환경변수 GEMINI_API_KEY 를 설정하거나 {path} 에 키를 저장하세요.");
            }
        }
    }

    /// <summary>키 파일에 주석/빈 줄이 섞여 있어도 첫 유효 줄을 키로 인정한다.</summary>
    private static string StripComments(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        foreach (string line in raw.Split('\n'))
        {
            string trimmed = line.Trim().Trim('\r');
            if (trimmed.Length == 0 || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                continue;

            return trimmed;
        }

        return string.Empty;
    }
}

/// <summary>
/// JsonUtility 로는 만들기 번거로운 요청 본문을 문자열로 조립하기 위한 최소 유틸.
/// 응답 파싱은 JsonUtility 가 담당하고, 요청은 템플릿 + 이스케이프로 처리한다.
/// </summary>
public static class JsonText
{
    /// <summary>문자열을 JSON 문자열 리터럴 내용으로 안전하게 이스케이프한다(따옴표는 포함하지 않는다).</summary>
    public static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var sb = new StringBuilder(value.Length + 16);
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }
}
