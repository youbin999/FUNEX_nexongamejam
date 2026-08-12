using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 타이틀 화면의 버튼 동작. NEW_WORLD / GALLERY / GITHUB 버튼의 OnClick 에 연결해서 쓴다.
/// 씬 이름과 링크는 인스펙터에서 바꿀 수 있게 직렬화 필드로 둔다.
/// </summary>
public class TitleMenu : MonoBehaviour
{
    [Header("씬")]
    [Tooltip("NEW WORLD 를 눌렀을 때 넘어갈 씬 이름. Build Settings 에 등록돼 있어야 한다")]
    [SerializeField] private string playSceneName = "00000_Player";

    [Tooltip("GALLERY 를 눌렀을 때 넘어갈 씬 이름. Build Settings 에 등록돼 있어야 한다")]
    [SerializeField] private string gallerySceneName = "00000_Gallery";

    [Header("링크")]
    [Tooltip("GITHUB 를 눌렀을 때 열 주소")]
    [SerializeField] private string githubUrl = "https://github.com/youbin999/FUNEX_nexongamejam";


    // ── 버튼 동작 ──

    /// <summary>NEW WORLD — 게임 씬으로 넘어간다.</summary>
    public void NewWorld()
    {
        LoadScene(playSceneName);
    }

    /// <summary>GALLERY — 지금까지 남긴 엔딩을 모아둔 갤러리로 넘어간다.</summary>
    public void Gallery()
    {
        LoadScene(gallerySceneName);
    }

    /// <summary>
    /// GITHUB — 저장소를 브라우저로 연다.
    /// 웹 빌드에서는 새 탭이 열린다. 버튼 클릭이 사용자 제스처라 팝업 차단에 걸리지 않는다.
    ///
    /// <b>이 메서드 이름은 씬의 Button OnClick 에 문자열로 저장돼 있다</b> —
    /// 이름을 바꾸면 컴파일은 되지만 버튼이 조용히 죽으므로 씬 배선도 같이 고쳐야 한다.
    /// </summary>
    public void OpenGitHub()
    {
        if (string.IsNullOrWhiteSpace(githubUrl))
        {
            Debug.LogWarning($"[{name}] 열 주소가 비어 있다.", this);
            return;
        }

        Application.OpenURL(githubUrl);
    }

    /// <summary>씬 이름이 비어 있지 않을 때만 전환한다.</summary>
    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning($"[{name}] 넘어갈 씬 이름이 비어 있다.", this);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
