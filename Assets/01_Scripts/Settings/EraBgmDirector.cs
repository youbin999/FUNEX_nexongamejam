using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 시대별 BGM 을 갈아 끼우는 컴포넌트. 인스펙터에 <see cref="Era"/> 마다 칸이 하나씩 있고,
/// 거기에 곡을 꽂아 두면 <see cref="GameFlowController"/> 가 그 시대의 미니게임을 시작할 때
/// 알아서 곡을 바꾼다(겹쳐 넘김).
///
/// 재생은 <see cref="AudioManager"/> 를 거치므로 설정 창(ESC)의 BGM 게이지가 그대로 먹는다.
/// 칸을 비워 두면 그 시대에는 직전 곡이 계속 흐른다 — 아직 곡이 없어도 게임은 그대로 돌아간다.
///
/// 놓는 곳: 플레이 씬(<c>00000_Player</c>)의 GameFlowController 옆에 빈 오브젝트를 만들어 붙인다.
/// 빅뱅 씬처럼 미니게임 흐름이 없는 씬에서는 <see cref="playOnStart"/> 를 켜고
/// <see cref="startEra"/> 를 지정하면 씬이 열릴 때 바로 튼다.
/// </summary>
[DisallowMultipleComponent]
public class EraBgmDirector : MonoBehaviour
{
    /// <summary>시대 하나에 대응하는 BGM 칸.</summary>
    [Serializable]
    public class EraTrack
    {
        [Tooltip("이 곡을 틀 시대")]
        public Era era;

        [Tooltip("이 시대에 흐를 BGM. 비워두면 직전 곡을 그대로 유지한다")]
        public AudioClip clip;

        [Tooltip("이 곡의 원본 크기. 설정 창 BGM 게이지 값에 이 값을 곱한다. 곡마다 녹음 크기가 다를 때 맞춘다")]
        [Range(0f, 1f)]
        public float volume = 1f;

        [Tooltip("이 곡으로 넘어갈 때 겹쳐 넘기는 시간(초). 음수면 아래 기본 페이드 시간을 쓴다")]
        public float fadeDuration = -1f;
    }

    private static EraBgmDirector instance;

    [Header("시대별 BGM")]
    [Tooltip("시대마다 한 칸씩. 칸이 비어 있으면 그 시대에는 직전 곡이 계속 흐른다")]
    [SerializeField] private List<EraTrack> tracks = new List<EraTrack>();

    [Header("옵션")]
    [Tooltip("곡을 바꿀 때 겹쳐 넘기는 기본 시간(초)")]
    [Min(0f)]
    [SerializeField] private float fadeDuration = 1.5f;

    [Tooltip("씬이 열리자마자 아래 시대의 곡을 튼다. 미니게임 흐름이 없는 씬에서 쓴다")]
    [SerializeField] private bool playOnStart;

    [Tooltip("playOnStart 가 켜져 있을 때 틀 시대")]
    [SerializeField] private Era startEra = Era.BigBang;

    [Tooltip("씬이 바뀌어도 유지한다. 보통은 꺼둔다 — 씬마다 따로 놓는 편이 다루기 쉽다")]
    [SerializeField] private bool dontDestroyOnLoad;

    private bool hasPlayed;
    private Era currentEra;

    /// <summary>지금 살아 있는 디렉터. 없으면 null.</summary>
    public static EraBgmDirector Instance => instance;

    /// <summary>마지막으로 요청받은 시대.</summary>
    public Era CurrentEra => currentEra;


    // ── 수명주기 ──

    /// <summary>컴포넌트를 처음 붙였을 때 시대별 칸을 채운다.</summary>
    private void Reset()
    {
        SyncEraSlots();
    }

    /// <summary>인스펙터 값이 바뀔 때마다 빠진 시대 칸을 다시 채운다.</summary>
    private void OnValidate()
    {
        SyncEraSlots();
    }

    /// <summary>싱글턴 자리를 잡고, 옵션이 켜져 있으면 씬 전환에도 살아남게 한다.</summary>
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (dontDestroyOnLoad)
        {
            // DontDestroyOnLoad 는 루트에만 통한다.
            if (transform.parent != null)
                transform.SetParent(null, worldPositionStays: true);

            DontDestroyOnLoad(gameObject);
        }
    }

    /// <summary>싱글턴 자리를 비운다.</summary>
    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    /// <summary>옵션이 켜져 있으면 씬이 열리자마자 지정 시대의 곡을 튼다.</summary>
    private void Start()
    {
        if (playOnStart)
            Play(startEra);
    }


    // ── 재생 ──

    /// <summary>
    /// 해당 시대의 BGM 으로 넘어간다. 같은 시대를 다시 요청하면 곡을 끊지 않는다.
    /// 칸이 비어 있으면 아무것도 하지 않는다 — 직전 곡이 그대로 흐른다.
    /// </summary>
    public void Play(Era era)
    {
        bool sameEra = hasPlayed && currentEra == era;

        currentEra = era;
        hasPlayed = true;

        if (sameEra)
            return;

        EraTrack track = Find(era);
        if (track == null || track.clip == null)
            return;

        float duration = track.fadeDuration >= 0f ? track.fadeDuration : fadeDuration;
        AudioManager.EnsureInstance().PlayBgm(track.clip, track.volume, restartIfSame: false, fadeDuration: duration);
    }

    /// <summary>BGM 을 서서히 끈다. 엔딩 전환처럼 소리를 비워야 할 때 쓴다.</summary>
    public void Stop()
    {
        hasPlayed = false;

        AudioManager manager = AudioManager.Instance;
        if (manager != null)
            manager.StopBgm(fadeDuration);
    }


    // ── 시대 칸 ──

    /// <summary>해당 시대의 칸을 찾는다. 없으면 null.</summary>
    private EraTrack Find(Era era)
    {
        foreach (EraTrack track in tracks)
        {
            if (track != null && track.era == era)
                return track;
        }

        return null;
    }

    /// <summary>인스펙터에 시대별 칸이 하나씩 다 보이도록 빠진 시대를 채운다.</summary>
    private void SyncEraSlots()
    {
        foreach (Era era in (Era[])Enum.GetValues(typeof(Era)))
        {
            if (Find(era) == null)
                tracks.Add(new EraTrack { era = era });
        }
    }
}
