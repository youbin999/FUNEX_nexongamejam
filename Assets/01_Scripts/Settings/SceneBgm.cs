using UnityEngine;

/// <summary>
/// 씬 하나에 깔 BGM. 빈 오브젝트에 붙이고 곡을 꽂아두면 씬이 열릴 때 튼다.
/// 시대별로 곡이 바뀌는 플레이 씬은 <see cref="EraBgmDirector"/> 를 쓰고,
/// 타이틀·갤러리·엔딩처럼 씬 내내 한 곡이면 이걸 쓴다.
///
/// 재생은 <see cref="AudioManager"/> 를 거치므로 설정 창(ESC)의 BGM 게이지가 그대로 먹는다.
/// 앞 씬과 같은 곡이면 끊지 않고 이어서 흐른다.
/// </summary>
[DisallowMultipleComponent]
public class SceneBgm : MonoBehaviour
{
    [Tooltip("이 씬에서 흐를 BGM. 비워두면 직전 곡을 그대로 유지한다")]
    [SerializeField] private AudioClip clip;

    [Tooltip("이 곡의 원본 크기. 설정 창 BGM 게이지 값에 이 값을 곱한다")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;

    [Tooltip("앞 곡과 겹쳐 넘기는 시간(초)")]
    [Min(0f)]
    [SerializeField] private float fadeDuration = 1.2f;

    [Tooltip("앞 씬에서 같은 곡이 흐르고 있어도 처음부터 다시 튼다")]
    [SerializeField] private bool restartIfSame;

    /// <summary>씬이 열리면 곡을 튼다.</summary>
    private void Start()
    {
        Play();
    }

    /// <summary>버튼이나 UnityEvent 로 다시 틀고 싶을 때 연결한다.</summary>
    public void Play()
    {
        if (clip == null)
            return;

        AudioManager.EnsureInstance().PlayBgm(clip, volume, restartIfSame, fadeDuration);
    }
}
