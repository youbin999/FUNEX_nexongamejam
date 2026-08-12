using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 빅뱅 씬(00_BigBang)의 흐름 처리기.
/// <see cref="MiniGamePlayer"/> 의 종료 통지를 받아 성공이면 다음 씬으로 넘어가고,
/// 실패면 실패 UI 와 다시하기 버튼을 띄운다. 기획상 빅뱅 실패는 "다시 빅뱅부터 시작" 이므로
/// 애플리케이션을 끄지 않고 버튼으로 같은 미니게임을 다시 재생한다.
/// 씬 전환 같은 바깥 일은 미니게임이 아니라 여기서 처리해야 미니게임을 재사용할 수 있다.
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
    [Tooltip("실패 시 띄울 다시하기 버튼. 비워두면 화면 중앙 하단에 코드로 만들어 붙인다")]
    [SerializeField] private Button retryButton;

    [Tooltip("자동 생성되는 다시하기 버튼의 문구")]
    [SerializeField] private string retryButtonLabel = "다시하기";

    [Tooltip("자동 생성되는 다시하기 버튼을 화면 아래 가장자리에서 띄우는 높이(px, 1920x1080 기준)")]
    [SerializeField] private float retryButtonBottomMargin = 160f;

    // 이 씬의 MiniGamePlayer 에는 빅뱅 프리팹 하나만 등록돼 있다.
    private const int BigBangGameIndex = 0;

    private bool handled;


    // ── 수명주기 ──

    /// <summary>미니게임 종료 통지를 구독한다.</summary>
    private void OnEnable()
    {
        if (player != null)
            player.onGameFinished.AddListener(OnGameFinished);
    }

    /// <summary>미니게임 종료 통지 구독을 해제한다.</summary>
    private void OnDisable()
    {
        if (player != null)
            player.onGameFinished.RemoveListener(OnGameFinished);
    }

    /// <summary>실패 UI 와 다시하기 버튼을 감춘 상태로 시작한다.</summary>
    private void Start()
    {
        if (failUI != null)
            failUI.SetActive(false);

        EnsureRetryButton();
    }


    // ── 결과 분기 ──

    /// <summary>성공이면 다음 씬으로 넘어가고, 실패면 실패 UI 와 다시하기 버튼을 띄운다.</summary>
    private void OnGameFinished(MiniGame instance, bool success)
    {
        // 종료 통지는 한 번뿐이지만, 씬 전환은 되돌릴 수 없으니 한 번만 타도록 막아 둔다.
        if (handled)
            return;

        handled = true;

        if (success)
        {
            SceneManager.LoadScene("00000_Title");
            return;
        }

        if (failUI != null)
            failUI.SetActive(true);

        if (retryButton != null)
            retryButton.gameObject.SetActive(true);
    }


    // ── 다시하기 ──

    /// <summary>
    /// 다시하기 버튼을 확보하고 리스너를 묶은 뒤 감춰 둔다.
    /// 인스펙터 배선이 있으면 그걸 쓰고, 없으면 화면 중앙 하단에 만들어 붙인다 —
    /// 버튼이 없으면 실패 후 아무것도 할 수 없게 되므로 배선 누락으로 막히면 안 된다.
    /// </summary>
    private void EnsureRetryButton()
    {
        if (retryButton == null)
            retryButton = BuildRetryButton();

        if (retryButton == null)
            return;

        retryButton.onClick.AddListener(Retry);
        retryButton.gameObject.SetActive(false);
    }

    /// <summary>화면 중앙 하단에 놓인 다시하기 버튼을 만든다.</summary>
    private Button BuildRetryButton()
    {
        SimpleUiBuilder.EnsureEventSystem();

        // 실패 UI 위에 확실히 뜨도록 별도 오버레이 캔버스에 올린다.
        Canvas canvas = SimpleUiBuilder.CreateOverlayCanvas("[BigBang Retry Canvas]", 300);
        canvas.transform.SetParent(transform, false);

        Button button = SimpleUiBuilder.CreateButton(
            canvas.transform, "RetryButton", retryButtonLabel, new Vector2(320f, 90f), 34f);

        // 아래 가장자리 중앙에 고정한다 — 해상도가 바뀌어도 바닥에서 같은 높이에 떠 있는다.
        SimpleUiBuilder.AnchorToCorner(
            (RectTransform)button.transform,
            new Vector2(0.5f, 0f),
            new Vector2(0f, retryButtonBottomMargin));

        return button;
    }

    /// <summary>
    /// 실패 UI 를 접고 빅뱅 미니게임을 처음부터 다시 재생한다.
    /// 씬을 다시 로드하지 않고 프리로드된 인스턴스를 재사용한다 — 빅뱅 영상 준비를 다시 기다리지
    /// 않아도 되고, 미니게임은 계약상 <c>StopAndReset()</c> 이후 다시 <c>Play()</c> 할 수 있다.
    /// </summary>
    public void Retry()
    {
        if (failUI != null)
            failUI.SetActive(false);

        if (retryButton != null)
            retryButton.gameObject.SetActive(false);

        // 다음 판의 종료 통지를 다시 받을 수 있도록 1회 가드를 풀어 준다.
        handled = false;

        if (player != null)
            player.PlayGame(BigBangGameIndex);
    }
}
