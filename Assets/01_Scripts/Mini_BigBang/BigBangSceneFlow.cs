using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 빅뱅 씬(00_BigBang)의 흐름 처리기.
/// <see cref="MiniGamePlayer"/> 의 종료 통지를 받아 성공이면 메인 씬으로 넘어가고,
/// 실패면 실패 UI 를 띄운 뒤 애플리케이션을 종료한다.
/// 씬 전환·종료 같은 바깥 일은 미니게임이 아니라 여기서 처리해야 미니게임을 재사용할 수 있다.
/// </summary>
public class BigBangSceneFlow : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private MiniGamePlayer player;

    [Tooltip("실패했을 때 켤 UI. 미니게임 프리팹은 종료 직후 비활성화되므로 반드시 씬에 두어야 한다")]
    [SerializeField] private GameObject failUI;

    [Header("성공")]
    [Tooltip("클리어 시 이동할 씬 이름. Build Settings 에 등록돼 있어야 한다")]
    [SerializeField] private string mainSceneName = "00000_Player";

    [Header("실패")]
    [Tooltip("실패 UI 를 보여준 뒤 종료하기까지의 시간(초). 0 이면 즉시 종료한다")]
    [SerializeField] private float quitDelay = 1.5f;

    private bool handled;

    private void OnEnable()
    {
        if (player != null)
            player.onGameFinished.AddListener(OnGameFinished);
    }

    private void OnDisable()
    {
        if (player != null)
            player.onGameFinished.RemoveListener(OnGameFinished);
    }

    private void Start()
    {
        if (failUI != null)
            failUI.SetActive(false);
    }

    private void OnGameFinished(MiniGame instance, bool success)
    {
        // 종료 통지는 한 번뿐이지만, 씬 전환/종료는 되돌릴 수 없으니 한 번만 타도록 막아 둔다.
        if (handled)
            return;

        handled = true;

        if (success)
        {
            SceneManager.LoadScene(mainSceneName);
            return;
        }

        if (failUI != null)
            failUI.SetActive(true);

        if (quitDelay > 0f)
            StartCoroutine(QuitAfterDelay());
        else
            Quit();
    }

    private IEnumerator QuitAfterDelay()
    {
        yield return new WaitForSecondsRealtime(quitDelay);
        Quit();
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
