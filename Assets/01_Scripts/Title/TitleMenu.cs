using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 타이틀 화면의 버튼 동작. NEW_WORLD / GALLERY / EXIT 버튼의 OnClick 에 연결해서 쓴다.
/// 씬 이름은 인스펙터에서 바꿀 수 있게 직렬화 필드로 둔다.
/// </summary>
public class TitleMenu : MonoBehaviour
{
    [Header("씬")]
    [Tooltip("NEW WORLD 를 눌렀을 때 넘어갈 씬 이름. Build Settings 에 등록돼 있어야 한다")]
    [SerializeField] private string playSceneName = "00000_Player";

    [Tooltip("GALLERY 를 눌렀을 때 넘어갈 씬 이름. Build Settings 에 등록돼 있어야 한다")]
    [SerializeField] private string gallerySceneName = "00000_Gallery";


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

    /// <summary>EXIT — 게임을 종료한다.</summary>
    public void QuitGame()
    {
        // 에디터에서는 Application.Quit 이 동작하지 않으므로 플레이 모드를 끈다.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
