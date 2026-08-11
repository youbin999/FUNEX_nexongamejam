using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Gemini API 키를 런타임에 읽어오는 로더.
/// 키를 소스에 하드코딩하지 않기 위해 프로젝트 밖에서 읽는다.
///
/// 로드 우선순위:
///   1. 환경변수 GEMINI_API_KEY (에디터/개발 PC에서 편하다)
///   2. Unity Remote Config 의 <see cref="RemoteKeyName"/> 키 — <b>빌드의 기본 경로</b>.
///      대시보드에서 값만 바꾸면 재빌드 없이 키를 교체·폐기할 수 있고, 빌드에 키 파일을 동봉하지 않아도 된다.
///   3. StreamingAssets/gemini_api_key.txt (오프라인·서비스 장애 시의 마지막 폴백)
///
/// 주의: Remote Config 값은 결국 클라이언트로 내려오므로, 빌드에 동봉하는 것과 마찬가지로
/// 추출될 수 있다. 사용량 한도를 낮게 건 잼 전용 키를 쓰고 제출 후에는 폐기할 것.
/// Remote Config 의 이점은 "추출 불가"가 아니라 "빌드에서 분리 + 사후 교체 가능"이다.
/// </summary>
public static class GeminiApiConfig
{
    /// <summary>StreamingAssets 안의 키 파일 이름.</summary>
    public const string KeyFileName = "gemini_api_key.txt";

    /// <summary>Remote Config 대시보드에 등록된 키 이름(String 타입).</summary>
    public const string RemoteKeyName = "Gemini_Youbean999";

    /// <summary>
    /// Remote Config 응답을 기다리는 최대 시간(초).
    /// 엔딩 크레딧 진입 직전에 호출되므로, 오프라인일 때 화면이 오래 멎지 않도록 짧게 끊고 폴백으로 넘어간다.
    /// </summary>
    private const float RemoteTimeoutSeconds = 6f;

    /// <summary>
    /// Remote Config 가 요구하는 조건부 배포용 속성 구조체.
    /// 지금은 모든 플레이어에게 같은 값을 내려주므로 비워 둔다 — 필드를 넣으면 대시보드에서 조건으로 쓸 수 있다.
    /// </summary>
    private struct UserAttributes { }

    private struct AppAttributes { }

    private static string cachedKey;
    private static bool loadAttempted;

    /// <summary>로드된 API 키. 아직 로드하지 않았거나 실패했으면 빈 문자열.</summary>
    public static string ApiKey => cachedKey ?? string.Empty;

    /// <summary>사용 가능한 키가 있는지.</summary>
    public static bool HasKey => !string.IsNullOrWhiteSpace(cachedKey);


    // ── 로드 ──

    /// <summary>
    /// 키를 로드한다. 멱등 — 이미 시도했으면 즉시 반환한다.
    /// 환경변수 → Remote Config → StreamingAssets 파일 순으로 시도하고, 처음 성공한 값을 쓴다.
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
            Debug.Log($"GeminiApiConfig: 환경변수 조회 실패 — 다음 경로로 넘어갑니다. ({e.Message})");
        }

        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            cachedKey = fromEnv.Trim();
            Debug.Log("GeminiApiConfig: 환경변수 GEMINI_API_KEY 에서 키를 읽었습니다.");
            yield break;
        }

        // 2) Remote Config. 빌드에서 키 파일을 떼어내기 위한 주 경로다.
        yield return LoadFromRemoteConfig();

        if (HasKey)
            yield break;

        // 3) StreamingAssets 파일. 오프라인이거나 Remote Config 가 막혔을 때의 폴백.
        //    StreamingAssets 는 플랫폼에 따라 직접 파일 접근이 안 되므로 UnityWebRequest 로 읽는다.
        string path = Path.Combine(Application.streamingAssetsPath, KeyFileName);
        string uri = path.Contains("://") ? path : "file://" + path;

        using (UnityWebRequest req = UnityWebRequest.Get(uri))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                cachedKey = StripComments(req.downloadHandler.text);
                Debug.Log($"GeminiApiConfig: StreamingAssets/{KeyFileName} 에서 키를 읽었습니다.");
            }
            else
            {
                Debug.LogWarning(
                    $"GeminiApiConfig: API 키를 찾지 못했습니다 ({req.error}). " +
                    $"엔딩은 폴백 텍스트로 진행됩니다.\n" +
                    $"해결: Remote Config 대시보드에 '{RemoteKeyName}' 키를 등록하거나, " +
                    $"환경변수 GEMINI_API_KEY 를 설정하거나, {path} 에 키를 저장하세요.");
            }
        }
    }

    /// <summary>
    /// Remote Config 에서 키를 받아 <see cref="cachedKey"/> 에 채운다. 실패하면 조용히 넘어간다(호출부가 폴백을 탄다).
    ///
    /// Remote Config API 는 async/await 기반이라 코루틴에서 바로 쓸 수 없어, Task 를 만들고 완료를 폴링한다.
    /// 타임아웃을 두는 이유는 엔딩 진입 직전에 호출되기 때문이다 — 오프라인에서 응답이 영영 안 와도
    /// 화면이 멎지 않고 파일 폴백으로 넘어가야 한다.
    /// </summary>
    private static IEnumerator LoadFromRemoteConfig()
    {
        Task<string> fetch = FetchRemoteKeyAsync();
        float deadline = Time.realtimeSinceStartup + RemoteTimeoutSeconds;

        while (!fetch.IsCompleted && Time.realtimeSinceStartup < deadline)
            yield return null;

        if (!fetch.IsCompleted)
        {
            Debug.LogWarning(
                $"GeminiApiConfig: Remote Config 응답이 {RemoteTimeoutSeconds}초 안에 오지 않았습니다. 파일 폴백으로 넘어갑니다.");
            yield break;
        }

        // FetchRemoteKeyAsync 는 내부에서 모든 예외를 삼키므로 Faulted 는 사실상 나오지 않지만,
        // 그래도 Result 에 손대기 전에 상태를 확인한다.
        if (fetch.Status != TaskStatus.RanToCompletion)
        {
            Debug.LogWarning($"GeminiApiConfig: Remote Config 조회가 실패했습니다. ({fetch.Exception?.GetBaseException().Message})");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(fetch.Result))
            yield break;

        cachedKey = fetch.Result.Trim();
        Debug.Log($"GeminiApiConfig: Remote Config '{RemoteKeyName}' 에서 키를 읽었습니다.");
    }

    /// <summary>
    /// Unity Services 초기화 → 익명 로그인 → Remote Config fetch 를 거쳐 키 문자열을 가져온다.
    /// 어느 단계에서 실패하든 예외를 던지지 않고 null 을 반환한다 — 엔딩은 폴백으로도 굴러가야 하므로
    /// 키를 못 받는 것이 게임을 멈출 이유는 되지 않는다.
    /// </summary>
    private static async Task<string> FetchRemoteKeyAsync()
    {
        try
        {
            // Remote Config 는 프로젝트가 클라우드에 연결돼 있고 플레이어가 로그인돼 있어야 동작한다.
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            // FetchConfigs 는 완료를 이벤트로 알리므로 Task 로 바꿔 기다린다.
            // RunContinuationsAsynchronously 로 두어야 이어지는 코드가 이벤트 디스패치 안에서 인라인으로 돌지 않는다.
            var completion = new TaskCompletionSource<ConfigResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            void OnFetchCompleted(ConfigResponse response) => completion.TrySetResult(response);

            RemoteConfigService.Instance.FetchCompleted += OnFetchCompleted;
            ConfigResponse result;
            try
            {
                RemoteConfigService.Instance.FetchConfigs(new UserAttributes(), new AppAttributes());
                result = await completion.Task;
            }
            finally
            {
                RemoteConfigService.Instance.FetchCompleted -= OnFetchCompleted;
            }

            // Default 는 서버 값을 한 번도 못 받았다는 뜻이다(로컬 기본값). 여기엔 키가 없다.
            if (result.requestOrigin == ConfigOrigin.Default)
            {
                Debug.LogWarning("GeminiApiConfig: Remote Config 에서 값을 받지 못했습니다(기본값 응답).");
                return null;
            }

            RuntimeConfig config = RemoteConfigService.Instance.appConfig;
            if (config == null || !config.HasKey(RemoteKeyName))
            {
                // 무엇이 내려왔는지 같이 찍어야 원인이 특정된다.
                // 목록이 비어 있으면 = 이 환경에 배포(Push)된 값이 없다는 뜻이고,
                // 다른 이름이 보이면 = 키 이름이 어긋난 것이다(대소문자 구분).
                string[] keys = config != null ? config.GetKeys() : Array.Empty<string>();
                string received = keys.Length > 0 ? string.Join(", ", keys) : "(없음)";

                Debug.LogWarning(
                    $"GeminiApiConfig: Remote Config 에 '{RemoteKeyName}' 키가 없습니다.\n" +
                    $"  응답 출처: {result.requestOrigin} / 상태: {result.status}\n" +
                    $"  내려온 키 목록: {received}\n" +
                    "  확인 순서: (1) 값을 프로젝트 설정의 '비밀(Secrets)' 이 아니라 " +
                    "Remote Config > Config 에 넣었는지 — Secrets 는 서버 쪽 서비스 전용이라 클라이언트가 읽지 못한다. " +
                    "(2) 키 이름 철자·대소문자. " +
                    "(3) 값을 넣은 Environment 가 클라이언트가 읽는 환경(기본 Production)인지.");
                return null;
            }

            return config.GetString(RemoteKeyName);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"GeminiApiConfig: Remote Config 조회 실패 — 파일 폴백으로 넘어갑니다. ({e.Message})");
            return null;
        }
    }


    // ── 파싱 ──

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
/// JSON 문자열을 다루는 최소 유틸.
/// 요청 본문은 JsonUtility 로 만들기 번거로워 템플릿 + 이스케이프로 조립하고,
/// 응답 본문은 로그에 통째로 싣기엔 길어서 잘라 쓴다.
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

    /// <summary>실패 응답을 로그에 남길 때 쓰는 말줄임. 길면 잘라내고 …을 붙인다.</summary>
    public static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Length <= max ? value : value.Substring(0, max) + "…";
    }
}
