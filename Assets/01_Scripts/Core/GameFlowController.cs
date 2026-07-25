using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// MiniGamePlayer 를 제어하여 인스펙터에 등록된 미니게임들을 순서대로 재생하는 게임 흐름 컨트롤러.
/// 크리티컬로 표시된 게임에서 실패하면 흐름을 멈추고 게임 엔딩으로 진행한다(onGameEnding 발화).
/// 크리티컬이 아닌 게임은 실패해도 다음 게임으로 계속 진행한다.
/// </summary>
public class GameFlowController : MonoBehaviour
{
    [Serializable]
    private class GameEntry
    {
        [Tooltip("재생할 미니게임 프리팹. player 쪽에는 따로 등록할 필요 없다 — Start 시점에 이 목록이 자동으로 주입된다")]
        public MiniGame prefab;

        [Tooltip("미니게임의 서사적 성격.\n" +
            "Normal: 실패해도 아무 영향 없음\n" +
            "Change: 실패해도 흐름은 이어지지만 엔딩 서사·엔딩 이미지에 반영됨\n" +
            "Critical: 실패 시 즉시 엔딩으로 이동")]
        public MiniGameKind kind = MiniGameKind.Normal;

        [Tooltip("이 게임이 속한 시대")]
        public Era era = Era.Prehistoric;

        [Tooltip("이 게임 시작 시 GameBorder 에 적용할 시대 테마 스프라이트. 비워두면 기존 테마를 유지한다")]
        public Sprite borderSprite;

        [Header("엔딩 맥락 (Change 일 때만 사용)")]
        [Tooltip("사건 이름. 예: '마녀사냥에서 마녀를 색출'")]
        public string eventLabel;

        [Tooltip("성공했을 때의 역사적 의미(한국어). 크레딧 문장의 재료")]
        [TextArea(2, 4)] public string successMeaning;

        [Tooltip("실패했을 때의 역사적 의미(한국어). 크레딧 문장의 재료")]
        [TextArea(2, 4)] public string failureMeaning;

        [Tooltip("성공했을 때 엔딩 이미지에 들어갈 시각 요소(영문 구절).\n" +
            "예: a witch burned at the stake, shown only as a faded illustration in a children's storybook")]
        [TextArea(2, 4)] public string successVisual;

        [Tooltip("실패했을 때 엔딩 이미지에 들어갈 시각 요소(영문 구절).\n" +
            "예: a young witch in a modern school uniform sitting among ordinary students")]
        [TextArea(2, 4)] public string failureVisual;

        [Tooltip("서사 비중. 높을수록 엔딩 이미지 전경에 배치되도록 유도한다")]
        [Range(0, 10)] public int visualWeight = 5;

        /// <summary>실패 시 즉시 엔딩으로 가야 하는 게임인지.</summary>
        public bool IsCritical => kind == MiniGameKind.Critical;

        /// <summary>결과에 따라 이 항목을 엔딩 재료(<see cref="MiniGameOutcome"/>)로 변환한다.</summary>
        public MiniGameOutcome ToOutcome(bool success) => new MiniGameOutcome
        {
            eventLabel = string.IsNullOrWhiteSpace(eventLabel) && prefab != null ? prefab.name : eventLabel,
            era = era.ToKorean(),
            success = success,
            meaning = success ? successMeaning : failureMeaning,
            visual = success ? successVisual : failureVisual,
            visualWeight = visualWeight,
        };
    }

    [Header("참조")]
    [SerializeField] private MiniGamePlayer player;

    [Header("게임 순서")]
    [Tooltip("순서대로 재생할 게임 목록")]
    [SerializeField] private List<GameEntry> games = new List<GameEntry>();

    [Header("옵션")]
    [Tooltip("Start 에서 자동으로 흐름을 시작한다")]
    [SerializeField] private bool playOnStart = true;

    [Header("이벤트")]
    [Tooltip("크리티컬 게임 실패 시 발화. 게임 엔딩 씬 전환 등에 연결한다")]
    public UnityEvent onGameEnding;

    [Tooltip("등록된 모든 게임을 클리어(또는 통과)하면 발화")]
    public UnityEvent onAllGamesCleared;

    private int currentIndex = -1;
    private bool ended;

    /// <summary>현재 흐름이 진행 중인지 여부.</summary>
    public bool IsRunning => currentIndex >= 0 && currentIndex < games.Count && !ended;

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
        InjectGamePrefabs();

        if (playOnStart)
            StartFlow();
    }

    /// <summary>
    /// games 에 등록된 프리팹들을 player 에 주입하고 Preload 한다.
    /// player.gamePrefabs 를 별도로 채우지 않아도 되게 하여 이중 등록을 없앤다.
    /// player 의 preloadOnAwake 가 켜져 있으면 이 시점엔 이미 Preload 가 끝나 주입이 무시되므로 꺼둘 것.
    /// </summary>
    private void InjectGamePrefabs()
    {
        if (player == null)
            return;

        if (player.IsPreloaded)
        {
            Debug.LogWarning("GameFlowController: player 가 이미 Preload 되어 프리팹 주입이 무시됩니다. player 의 Preload On Awake 를 꺼주세요.", this);
            return;
        }

        var prefabs = new List<MiniGame>(games.Count);
        foreach (GameEntry entry in games)
        {
            if (entry.prefab != null)
                prefabs.Add(entry.prefab);
        }

        player.SetGamePrefabs(prefabs);
        player.Preload();
    }

    /// <summary>처음부터 등록된 순서대로 게임 흐름을 시작한다.</summary>
    public void StartFlow()
    {
        if (player != null)
        {
            player.StopCurrent();
            player.ClearFailurePenalties();
        }

        ended = false;
        currentIndex = -1;

        // 재시작 시 이전 판의 엔딩 재료가 섞이지 않도록 비운다.
        RunResult.Instance.Clear();

        PlayNext();
    }

    private void PlayNext()
    {
        currentIndex++;

        if (currentIndex >= games.Count)
        {
            ended = true;
            onAllGamesCleared.Invoke();
            return;
        }

        GameEntry entry = games[currentIndex];
        if (entry.prefab == null || player == null ||
            !player.PlayGame(entry.prefab, entry.borderSprite, entry.eraYearText))
        {
            Debug.LogWarning($"GameFlowController: index {currentIndex} 게임을 재생할 수 없습니다.", this);
            PlayNext();
        }
    }

    private void OnGameFinished(MiniGame instance, bool success)
    {
        if (ended)
            return;

        if (currentIndex < 0 || currentIndex >= games.Count)
            return;

        GameEntry entry = games[currentIndex];

        // 변화 미니게임은 성공/실패 모두 엔딩 재료가 된다 — 실패만 기록하는 게 아니다.
        if (entry.kind == MiniGameKind.Change)
            RunResult.Instance.Record(entry.ToOutcome(success));

        if (!success && entry.IsCritical)
        {
            ended = true;
            RunResult.Instance.MarkEarlyEnding(entry.era.ToKorean(), entry.eventLabel);
            onGameEnding.Invoke();
            return;
        }

        PlayNext();
    }
}
