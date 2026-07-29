using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 미니게임 하나를 자기 씬에서 바로 돌려보기 위한 테스트용 호스트.
/// 미니게임은 스스로 시작하지 않으므로(계약 1.3), 씬에 프리팹만 놓고 Play 를 누르면
/// 아무 반응이 없다. 이 스크립트가 <see cref="MiniGame.Play"/> 를 대신 불러준다.
///
/// 개발 중 확인용이다. 실제 흐름은 <see cref="MiniGamePlayer"/> 와
/// <see cref="GameFlowController"/> 가 담당하므로, 완성한 프리팹은 그쪽에 등록한다.
/// </summary>
public class MiniGameSoloTester : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("돌려볼 미니게임. 비워두면 같은 오브젝트나 자식에서 찾는다")]
    [SerializeField] private MiniGame target;

    [Header("옵션")]
    [Tooltip("Start 에서 바로 시작한다")]
    [SerializeField] private bool playOnStart = true;

    [Tooltip("시작 전에 기다리는 시간(초). 화면을 보고 준비할 여유를 준다")]
    [SerializeField] private float startDelay = 0.5f;

    [Tooltip("게임이 끝나고 이 시간이 지나면 자동으로 다시 시작한다. 0이면 자동 재시작 안 함")]
    [SerializeField] private float restartDelay = 1.5f;

    [Tooltip("아무 때나 눌러서 다시 시작할 수 있는 키")]
    [SerializeField] private Key restartKey = Key.R;

    [Tooltip("시작/종료를 콘솔에 찍는다")]
    [SerializeField] private bool logResult = true;

    private float countdown;
    private bool waiting;


    // ── 수명주기 ──

    /// <summary>대상 미니게임을 지정받지 못했으면 자식에서 찾는다.</summary>
    private void Awake()
    {
        if (target == null)
            target = GetComponentInChildren<MiniGame>(true);
    }

    /// <summary>종료 통지 구독을 건다.</summary>
    private void OnEnable()
    {
        if (target != null)
            target.Finished += OnFinished;
    }

    /// <summary>종료 통지 구독을 해제한다.</summary>
    private void OnDisable()
    {
        if (target != null)
            target.Finished -= OnFinished;
    }

    /// <summary>옵션이 켜져 있으면 지정한 지연 뒤에 첫 판을 시작한다.</summary>
    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning($"{name}: 돌려볼 MiniGame 을 찾지 못했다", this);
            return;
        }

        if (playOnStart)
            Schedule(startDelay);
    }

    /// <summary>재시작 키를 살피고, 예약된 시작 시각이 되면 판을 띄운다.</summary>
    private void Update()
    {
        if (target == null)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && restartKey != Key.None && keyboard[restartKey].wasPressedThisFrame)
            Schedule(0f);

        if (!waiting)
            return;

        countdown -= Time.deltaTime;
        if (countdown > 0f)
            return;

        waiting = false;
        PlayNow();
    }


    // ── 재생 제어 ──

    /// <summary>지금 즉시 다시 시작한다. 버튼 OnClick 에 연결해도 된다.</summary>
    public void PlayNow()
    {
        if (target == null)
            return;

        // 이전 판이 남아 있을 수 있으니 초기 상태로 돌려놓고 시작한다.
        target.StopAndReset();
        target.gameObject.SetActive(true);
        target.Play();

        if (logResult)
            Debug.Log($"[SoloTester] {target.name} 시작", target);
    }

    /// <summary>지정 시간 뒤에 시작하도록 예약한다. 0이면 곧바로 시작한다.</summary>
    private void Schedule(float delay)
    {
        if (delay <= 0f)
        {
            waiting = false;
            PlayNow();
            return;
        }

        countdown = delay;
        waiting = true;
    }

    /// <summary>결과를 콘솔에 남기고, 자동 재시작이 켜져 있으면 다음 판을 예약한다.</summary>
    private void OnFinished(MiniGame game, bool success)
    {
        if (logResult)
            Debug.Log($"[SoloTester] {game.name} 종료 — {(success ? "클리어" : "실패")}", game);

        if (restartDelay > 0f)
            Schedule(restartDelay);
    }
}
