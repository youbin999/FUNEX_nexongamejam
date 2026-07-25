using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미니게임 프리팹마다 들고 있던 타이머 UI를 씬 하나로 합친 표시기.
/// <see cref="MiniGamePlayer.Current"/> 가 재생 중인 게임의 <see cref="MiniGame.TimerRatio"/> 를
/// 매 프레임 읽어 게이지에 밀어 넣는다.
///
/// 프리팹의 UnityEvent 는 씬 오브젝트를 참조할 수 없어 인스펙터로 이어 붙일 수 없다.
/// 그렇다고 런타임에 onTimerChanged 를 구독하면 타이머 이벤트 타입이 게임마다 달라 타입 분기가 필요하고,
/// 일부 게임이 Awake 에서 발화하는 초기값을 놓치거나 정리 순서에 따라 값이 남는 문제가 생긴다.
/// 그래서 이벤트 구독 대신 베이스에 뚫어둔 조회창(<see cref="MiniGame.HasTimer"/>/<see cref="MiniGame.TimerRatio"/>)을
/// 폴링한다. 매 프레임 float 하나를 읽는 비용이라 구독/해제 타이밍 버그를 없애는 값으로 충분히 싸다.
///
/// 씬이 UI 를 소유하고 미니게임 인스턴스에서 값을 당겨오는 구조라
/// <see cref="MiniGameIntroPresenter"/> 와 같은 계열이다. 그쪽은 한 번만 읽고, 이쪽은 매 프레임 읽는다.
/// </summary>
[DisallowMultipleComponent]
public class MiniGameTimerPresenter : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("타이머를 읽어올 플레이어. 비워두면 같은 오브젝트에서 찾는다")]
    [SerializeField] private MiniGamePlayer player;

    [Header("UI")]
    [Tooltip("타이머 전체를 켜고 끄는 루트")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("차오르는 게이지. Image Type 을 Filled / Horizontal 로 맞춰 둔다")]
    [SerializeField] private Image fillImage;

    [Header("연출")]
    [Tooltip("나타나고 사라지는 데 걸리는 시간(초). 0이면 즉시 전환")]
    [SerializeField] private float fadeDuration = 0.15f;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<MiniGamePlayer>();

        if (player == null)
            Debug.LogWarning($"[{name}] Player 를 못 찾아 타이머가 동작하지 않는다.", this);

        // 재생 전에는 숨겨 둔다. 게임 설명 인트로 동안 빈 게이지가 보이면 안 된다.
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private void Update()
    {
        MiniGame game = player != null ? player.Current : null;

        // Current 는 트랜지션이 시작될 때 이미 채워지지만 IsPlaying 은 Play() 후에야 켜진다.
        // 덕분에 게임 설명 인트로 동안에는 타이머가 나오지 않는다.
        bool show = game != null && game.IsPlaying && game.HasTimer;

        if (show && fillImage != null)
            fillImage.fillAmount = game.TimerRatio;

        ApplyVisibility(show);
    }

    private void ApplyVisibility(bool show)
    {
        if (canvasGroup == null)
            return;

        float target = show ? 1f : 0f;

        if (fadeDuration <= 0f)
        {
            canvasGroup.alpha = target;
            return;
        }

        // 설정 창이 열려 게임이 멈춰 있어도 페이드가 진행되도록 스케일 안 받는 시간을 쓴다.
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, Time.unscaledDeltaTime / fadeDuration);
    }
}
