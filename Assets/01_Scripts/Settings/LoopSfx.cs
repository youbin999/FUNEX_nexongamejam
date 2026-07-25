using UnityEngine;

/// <summary>
/// 켜져 있는 동안 계속 도는 배경 효과음(경보음, 기계음 같은 앰비언스).
/// 한 번 울리고 끝나는 소리는 <see cref="SfxPlayer"/> 를, 깔아두는 소리는 이것을 쓴다.
///
/// 미니게임 프리팹은 <see cref="MiniGamePlayer"/> 가 껐다 켜서 재사용하므로
/// AudioSource 의 Play On Awake 로는 두 번째 판부터 안 울릴 수 있다.
/// 그래서 켜질 때 직접 재생하고 꺼질 때 멈추기만 한다.
///
/// 크기는 건드리지 않는다. 같은 오브젝트에 <see cref="VolumeTrackedSource"/> 를 붙이면
/// AudioSource 의 Volume 에 넣어둔 값을 원본 크기로 삼아 설정 창 게이지에 연동해 준다.
/// BGM 보다 작게 깔고 싶으면 그 Volume 값을 낮추면 된다.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public class LoopSfx : MonoBehaviour
{
    [Tooltip("반복 재생할 클립. 비워두면 AudioSource 에 이미 꽂혀 있는 것을 쓴다")]
    [SerializeField] private AudioClip clip;

    private AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();

        // 재생 시점은 이 스크립트가 정한다. Play On Awake 는 꺼둔다.
        source.playOnAwake = false;
        source.loop = true;

        // 3D 로 두면 카메라와의 거리에 따라 소리가 죽는다.
        source.spatialBlend = 0f;

        if (clip != null)
            source.clip = clip;
    }

    private void OnEnable()
    {
        if (source.clip != null)
            source.Play();
    }

    private void OnDisable()
    {
        source.Stop();
    }
}
