using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 엔딩 이미지를 런타임에 생성한다.
/// 변화 미니게임 성공/실패 조합이 2^N 가지라 사전 제작이 불가능하므로 매 판 생성하고,
/// 조합 키로 디스크에 캐시해 같은 조합을 다시 뽑으면 즉시 로드한다(= 갤러리 저장소이기도 하다).
///
/// 실패는 흔한 일로 취급한다 — 무료 티어 한도, 안전 필터, 네트워크 모두 실패 요인이다.
/// 어떤 경우에도 예외를 던지지 않고 null 을 돌려주며, 호출 측이 폴백 배경으로 넘어간다.
/// </summary>
public class EndingImageGenerator
{
    private const string Endpoint =
        "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent";

    private readonly string model;
    private readonly int timeoutSeconds;

    public EndingImageGenerator(string model = "gemini-2.5-flash-image", int timeoutSeconds = 60)
    {
        this.model = model;
        this.timeoutSeconds = timeoutSeconds;
    }

    /// <summary>생성된 엔딩 이미지가 쌓이는 폴더. 갤러리 기능이 그대로 읽어 쓰면 된다.</summary>
    public static string CacheDirectory => Path.Combine(Application.persistentDataPath, "endings");

    /// <summary>조합 키에 대응하는 캐시 파일 경로.</summary>
    public static string CachePathFor(string combinationKey)
        => Path.Combine(CacheDirectory, $"ending_{combinationKey}.png");

    /// <summary>
    /// 엔딩 이미지를 얻는다. 캐시가 있으면 즉시, 없으면 생성 후 캐시에 저장한다.
    /// 실패 시 <paramref name="onDone"/> 에 null 이 전달된다.
    /// </summary>
    public IEnumerator GetOrCreate(string combinationKey, string imagePrompt, Action<Texture2D> onDone)
    {
        // 1) 캐시 조회.
        Texture2D cached = TryLoadCache(combinationKey);
        if (cached != null)
        {
            onDone(cached);
            yield break;
        }

        // 2) 생성.
        if (!GeminiApiConfig.HasKey)
        {
            Debug.LogWarning("EndingImageGenerator: API 키가 없어 이미지 생성을 건너뜁니다.");
            onDone(null);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(imagePrompt))
        {
            Debug.LogWarning("EndingImageGenerator: 이미지 프롬프트가 비어 있습니다.");
            onDone(null);
            yield break;
        }

        Texture2D generated = null;
        yield return Request(imagePrompt, tex => generated = tex);

        if (generated != null)
            TrySaveCache(combinationKey, generated);

        onDone(generated);
    }

    /// <summary>Gemini 이미지 모델을 호출해 Texture2D 를 만든다. 실패하면 null.</summary>
    private IEnumerator Request(string imagePrompt, Action<Texture2D> onDone)
    {
        string url = string.Format(Endpoint, model);
        string json =
            "{" +
              "\"contents\":[{\"role\":\"user\",\"parts\":[{\"text\":\"" + JsonText.Escape(imagePrompt) + "\"}]}]," +
              "\"generationConfig\":{\"responseModalities\":[\"IMAGE\"]}" +
            "}";

        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("x-goog-api-key", GeminiApiConfig.ApiKey);
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning(
                    $"EndingImageGenerator: 생성 실패 HTTP {req.responseCode} {req.error}\n" +
                    Truncate(req.downloadHandler.text, 500));
                onDone(null);
                yield break;
            }

            Texture2D tex = null;
            try
            {
                tex = DecodeImage(req.downloadHandler.text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"EndingImageGenerator: 응답 디코드 실패 — {e.Message}");
            }

            if (tex == null)
            {
                // 안전 필터에 걸리면 200 이면서 이미지 파트가 없는 응답이 온다.
                Debug.LogWarning(
                    "EndingImageGenerator: 응답에 이미지가 없습니다(안전 필터 가능성).\n" +
                    Truncate(req.downloadHandler.text, 500));
            }

            onDone(tex);
        }
    }

    /// <summary>응답에서 inlineData(base64)를 찾아 Texture2D 로 디코드한다.</summary>
    private static Texture2D DecodeImage(string json)
    {
        var response = JsonUtility.FromJson<GeminiResponse>(json);
        GeminiPart[] parts = response?.FirstParts;

        if (parts == null)
            return null;

        foreach (GeminiPart part in parts)
        {
            if (part.inlineData == null || string.IsNullOrEmpty(part.inlineData.data))
                continue;

            byte[] bytes = Convert.FromBase64String(part.inlineData.data);

            // LoadImage 가 크기를 알아서 맞춰주므로 초기 크기는 의미가 없다.
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(bytes))
                return tex;

            UnityEngine.Object.Destroy(tex);
        }

        return null;
    }

    private static Texture2D TryLoadCache(string combinationKey)
    {
        try
        {
            string path = CachePathFor(combinationKey);
            if (!File.Exists(path))
                return null;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(File.ReadAllBytes(path)))
                return tex;

            UnityEngine.Object.Destroy(tex);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"EndingImageGenerator: 캐시 로드 실패 — {e.Message}");
        }

        return null;
    }

    private static void TrySaveCache(string combinationKey, Texture2D tex)
    {
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            File.WriteAllBytes(CachePathFor(combinationKey), tex.EncodeToPNG());
        }
        catch (Exception e)
        {
            // 저장 실패는 치명적이지 않다 — 이번 판 이미지는 이미 메모리에 있다.
            Debug.LogWarning($"EndingImageGenerator: 캐시 저장 실패 — {e.Message}");
        }
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s.Substring(0, max) + "…");
}
