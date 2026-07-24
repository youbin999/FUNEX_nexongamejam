using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 미니게임 프리팹들을 프리로드해 두었다가 명령이 오면 재생하는 플레이어.
/// 비활성 홀더의 자식으로 Instantiate 하므로, 프리팹 루트에 저장된 활성 상태와 무관하게
/// Instantiate 시점에 Awake 가 돌지 않고, 재생을 위해 꺼내 활성화하는 순간 처음 초기화된다.
/// </summary>
public class MiniGamePlayer : MonoBehaviour
{
    // 인스펙터에 확실히 노출되도록 제네릭 UnityEvent 는 구체 타입으로 선언한다.
    [Serializable] public class GameFinishedEvent : UnityEvent<MiniGame, bool> { }

    [Header("프리팹")]
    [Tooltip("재생할 미니게임 프리팹 목록. 루트의 저장된 활성 상태는 상관없다 (프리로드 시 자동으로 비활성 처리된다)")]
    [SerializeField] private List<MiniGame> gamePrefabs = new List<MiniGame>();

    [Header("옵션")]
    [Tooltip("Awake 시점에 모든 프리팹을 미리 Instantiate 한다")]
    [SerializeField] private bool preloadOnAwake = true;

    [Tooltip("테스트용. Start 에서 첫 번째 게임을 자동으로 재생한다")]
    [SerializeField] private bool playFirstOnStart = false;

    [Header("이벤트")]
    [Tooltip("게임이 끝나면 (인스턴스, 성공 여부) 를 넘긴다. 이 통지 이후 인스턴스는 자동으로 초기화·비활성화된다")]
    public GameFinishedEvent onGameFinished;

    // 프리팹 인덱스 -> 프리로드된 인스턴스.
    private readonly Dictionary<int, MiniGame> instances = new Dictionary<int, MiniGame>();
    private bool preloaded;

    // 프리로드된 인스턴스를 담아 두는 비활성 홀더. 부모가 비활성이면 자식의
    // activeInHierarchy 도 false 가 되므로, 프리팹 루트의 저장된 활성 상태와 무관하게
    // Awake/OnEnable 이 돌지 않는 것을 보장한다.
    private Transform preloadHolder;

    /// <summary>현재 재생 중인 인스턴스. 없으면 null.</summary>
    public MiniGame Current { get; private set; }

    /// <summary>현재 재생 중인 게임이 있는지 여부.</summary>
    public bool IsPlaying => Current != null;

    private void Awake()
    {
        if (preloadOnAwake)
            Preload();
    }

    private void Start()
    {
        if (playFirstOnStart)
            PlayGame(0);
    }

    /// <summary>
    /// 모든 프리팹을 비활성 홀더의 자식으로 Instantiate 해 둔다. 멱등.
    /// 홀더가 비활성이므로 프리팹 루트의 저장된 활성 상태와 무관하게 Awake 가 돌지 않는다.
    /// </summary>
    public void Preload()
    {
        if (preloaded)
            return;

        EnsurePreloadHolder();

        for (int i = 0; i < gamePrefabs.Count; i++)
        {
            MiniGame prefab = gamePrefabs[i];
            if (prefab == null)
                continue;

            MiniGame instance = Instantiate(prefab, preloadHolder);
            instances[i] = instance;
        }

        preloaded = true;
    }

    /// <summary>비활성 상태의 프리로드 홀더를 (없으면) 만든다.</summary>
    private void EnsurePreloadHolder()
    {
        if (preloadHolder != null)
            return;

        var holder = new GameObject("[Preload Pool]");
        holder.transform.SetParent(transform, false);
        holder.SetActive(false);
        preloadHolder = holder.transform;
    }

    /// <summary>
    /// index 번째 게임을 재생한다. 성공 여부를 반환한다.
    /// 이미 재생 중이거나 인덱스 범위 밖이면 false.
    /// 프리로드가 안 돼 있으면 먼저 프리로드한다(지연 로드 허용).
    /// </summary>
    public bool PlayGame(int index)
    {
        if (IsPlaying)
            return false;

        if (index < 0 || index >= gamePrefabs.Count)
            return false;

        if (!preloaded)
            Preload();

        if (!instances.TryGetValue(index, out MiniGame instance) || instance == null)
            return false;

        Current = instance;
        instance.transform.SetParent(transform, false);
        instance.gameObject.SetActive(true);
        instance.Finished += OnGameFinished;
        instance.Play();
        return true;
    }

    /// <summary>
    /// gamePrefabs 에 등록된 프리팹 에셋으로 게임을 재생한다. 내부적으로 인덱스를 찾아 PlayGame(int) 를 호출한다.
    /// 등록되지 않은 프리팹이면 false.
    /// </summary>
    public bool PlayGame(MiniGame prefab)
    {
        int index = gamePrefabs.IndexOf(prefab);
        if (index < 0)
            return false;

        return PlayGame(index);
    }

    /// <summary>현재 게임을 강제로 중단·초기화하고 비활성화한다. (onGameFinished 는 발화하지 않는다)</summary>
    public void StopCurrent()
    {
        if (Current == null)
            return;

        MiniGame instance = Current;
        Current = null;

        instance.Finished -= OnGameFinished;
        instance.StopAndReset();
        instance.gameObject.SetActive(false);
        instance.transform.SetParent(preloadHolder, false);
    }

    /// <summary>
    /// 게임 종료 통지 수신. Current 를 먼저 비운 뒤 onGameFinished 를 발화하고,
    /// 그다음 인스턴스를 초기화·비활성화하고 구독을 해제한다("종료된 게임의 자동 초기화" 경로).
    /// </summary>
    private void OnGameFinished(MiniGame instance, bool success)
    {
        // Invoke 이전에 Current 를 먼저 비워서, 콜백 안에서 즉시 다음 게임을 재생해도(재진입)
        // IsPlaying 가드에 걸려 PlayGame 이 막히지 않도록 한다.
        if (Current == instance)
            Current = null;

        onGameFinished.Invoke(instance, success);

        instance.Finished -= OnGameFinished;
        instance.StopAndReset();
        instance.gameObject.SetActive(false);
        instance.transform.SetParent(preloadHolder, false);
    }
}
