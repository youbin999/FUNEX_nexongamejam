using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Gemini 로 엔딩 대본을 생성하는 <see cref="IEndingNarrator"/> 구현.
/// responseSchema(structured output)를 걸어 파싱 실패 가능성을 없앤다.
/// 어떤 이유로든 실패하면 onFail 을 부르고, 호출 측이 <see cref="FallbackEndingNarrator"/> 로 넘어간다.
/// </summary>
public class GeminiEndingNarrator : IEndingNarrator
{
    private const string Endpoint =
        "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent";

    private readonly string model;
    private readonly int timeoutSeconds;

    // gemini-2.5-flash 는 신규 사용자에게 더 이상 제공되지 않는다(404). 3.6-flash 로 실호출 검증 완료.
    public GeminiEndingNarrator(string model = "gemini-3.6-flash", int timeoutSeconds = 20)
    {
        this.model = model;
        this.timeoutSeconds = timeoutSeconds;
    }

    /// <summary>
    /// 엔딩 대본 작성 규칙. 이 게임의 서사 톤이 여기에 집중돼 있으므로
    /// 문구를 고칠 일이 있으면 이 상수만 손보면 된다.
    /// </summary>
    private const string SystemInstruction =
@"너는 시대별 미니게임 게임의 엔딩 크레딧 작가다.
플레이어가 각 시대에서 내린 선택의 결과를 받아, 그 선택들이 모여 만들어진 '대체 역사'를 서술한다.

[크레딧 텍스트 규칙]
- 한국어로 쓴다.
- 스타워즈 인트로처럼 위로 흘러가는, 담담하고 서사적인 문체를 쓴다. 
- credit_lines 는 8~14줄. 각 줄은 45자 이내로 화면 한 줄에 들어가야 한다.
- 한 편의 사평처럼 써라. 상상력을 발휘해봐라. 
- 빈 줄로 호흡을 줘도 된다.
- 절대 '성공'이나 '실패'라는 단어를 쓰지 않는다. 플레이어를 평가하지 않는다.
- 결과를 '그렇게 된 역사'로 당연하게 서술한다.
  예) 마녀를 잡지 못한 경우: '마녀는 살아남았고, 지금 당신 옆자리에 앉아 있다.'
  예) 마녀를 잡은 경우: '마녀는 동화 속으로 물러났고, 세계에서 마법이 지워졌다.'
- epilogue 는 롤이 끝난 뒤 화면 중앙에 남길 한 문장이다.

[이미지 프롬프트 규칙]
- image_prompt 는 영어로 쓴다.
- 주어진 시각 요소를 하나도 빠뜨리지 말고 전부 포함한다.
- 요소를 나열하지 말고, 같은 장소·같은 시간대·같은 광원을 공유하는 '하나의 장면'으로 융합한다.
- weight 가 높은 요소는 전경의 주요 피사체로, 낮은 요소는 배경 디테일
  (창밖 풍경, 벽에 붙은 포스터, 지나가는 행인, 책상 위 물건)로 배치한다.
- 폭력적이거나 유혈이 있는 묘사는 피한다. 필요하면 삽화·조각상·오래된 사진 같은
  간접 표현으로 우회한다.
- 프롬프트 끝에 반드시 다음 스타일 지시를 그대로 덧붙인다:
  ""Photorealistic, cinematic wide shot, 16:9, single coherent location and time of day, consistent natural lighting, muted desaturated color grade, film grain. No text, no watermark, no collage, no split panels.""";

    public IEnumerator Generate(RunResult result, Action<EndingScript> onDone, Action<string> onFail)
    {
        string promptPayload = result.ToPromptPayload();
        Debug.Log(
            "GeminiEndingNarrator: 엔딩 이미지 프롬프트 생성에 전달하는 결과 목록\n" +
            promptPayload);

        if (!GeminiApiConfig.HasKey)
        {
            onFail("API 키가 없습니다.");
            yield break;
        }

        string url = string.Format(Endpoint, model);
        byte[] body = Encoding.UTF8.GetBytes(BuildRequestBody(promptPayload));

        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("x-goog-api-key", GeminiApiConfig.ApiKey);
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                // 응답 본문에 원인이 담겨 있는 경우가 많아 같이 남긴다.
                onFail($"HTTP {req.responseCode} {req.error} / {Truncate(req.downloadHandler.text, 400)}");
                yield break;
            }

            EndingScript script = null;
            string parseError = null;

            try
            {
                script = ParseScript(req.downloadHandler.text);
            }
            catch (Exception e)
            {
                parseError = e.Message;
            }

            if (script != null && script.IsValid)
                onDone(script);
            else
                onFail($"응답 파싱 실패: {parseError ?? "내용이 비어 있음"} / {Truncate(req.downloadHandler.text, 400)}");
        }
    }

    /// <summary>
    /// generateContent 요청 본문을 만든다.
    /// responseSchema 는 고정이라 리터럴로 두고, 가변 부분만 이스케이프해 끼워 넣는다.
    /// </summary>
    private string BuildRequestBody(string promptPayload)
    {
        string system = JsonText.Escape(SystemInstruction);
        string user = JsonText.Escape(promptPayload);

        return
            "{" +
              "\"systemInstruction\":{\"parts\":[{\"text\":\"" + system + "\"}]}," +
              "\"contents\":[{\"role\":\"user\",\"parts\":[{\"text\":\"" + user + "\"}]}]," +
              "\"generationConfig\":{" +
                "\"responseMimeType\":\"application/json\"," +
                "\"responseSchema\":{" +
                  "\"type\":\"OBJECT\"," +
                  "\"properties\":{" +
                    "\"credit_lines\":{\"type\":\"ARRAY\",\"items\":{\"type\":\"STRING\"}}," +
                    "\"epilogue\":{\"type\":\"STRING\"}," +
                    "\"image_prompt\":{\"type\":\"STRING\"}" +
                  "}," +
                  "\"required\":[\"credit_lines\",\"epilogue\",\"image_prompt\"]" +
                "}" +
              "}" +
            "}";
    }

    /// <summary>응답 봉투를 벗기고 안쪽 JSON 을 <see cref="EndingScript"/> 로 파싱한다.</summary>
    private static EndingScript ParseScript(string json)
    {
        var response = JsonUtility.FromJson<GeminiResponse>(json);
        GeminiPart[] parts = response?.FirstParts;

        if (parts == null || parts.Length == 0)
            return null;

        foreach (GeminiPart part in parts)
        {
            if (string.IsNullOrWhiteSpace(part.text))
                continue;

            // 최신 모델은 답변 앞에 사고(thought) 파트를 끼워 넣기도 한다.
            // JSON 이 아닌 파트에서 FromJson 이 던지는 예외로 전체 파싱이 중단되지 않도록
            // 파트 단위로 예외를 삼키고 다음 파트를 계속 시도한다.
            try
            {
                var script = JsonUtility.FromJson<EndingScript>(part.text);
                if (script != null && script.IsValid)
                    return script;
            }
            catch (Exception)
            {
                // 이 파트는 대본이 아니다 — 다음 파트로.
            }
        }

        return null;
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s.Substring(0, max) + "…");
}
