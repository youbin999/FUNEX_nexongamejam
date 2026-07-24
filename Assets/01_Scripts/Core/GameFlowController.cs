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
        [Tooltip("player 의 gamePrefabs 에 등록된 것과 동일한 프리팹 에셋이어야 한다")]
        public MiniGame prefab;

        [Tooltip("체크하면 이 게임 실패 시 즉시 게임 엔딩으로 진행한다")]
        public bool isCritical;
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
        if (playOnStart)
            StartFlow();
    }

    /// <summary>처음부터 등록된 순서대로 게임 흐름을 시작한다.</summary>
    public void StartFlow()
    {
        ended = false;
        currentIndex = -1;
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
        if (entry.prefab == null || player == null || !player.PlayGame(entry.prefab))
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
        if (!success && entry.isCritical)
        {
            ended = true;
            onGameEnding.Invoke();
            return;
        }

        PlayNext();
    }
}
