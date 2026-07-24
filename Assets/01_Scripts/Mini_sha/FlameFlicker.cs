using UnityEngine;

/// <summary>
/// 제자리에서 아주 살짝 일렁이는 불꽃. (살아있는 쪽)
/// 불 오브젝트에 붙여 두기만 하면 켜져 있는 동안 계속 흔들린다.
/// 값을 키우면 금방 촐랑거려 보이므로 기본값은 눈에 겨우 띌 정도로 잡아 뒀다.
/// </summary>
public class FlameFlicker : MonoBehaviour
{
    [Header("흔들림")]
    [Tooltip("좌우로 기우는 각도. 2~3도면 충분하다")]
    [Range(0f, 15f)]
    [SerializeField] private float swayAngle = 2.5f;

    [Tooltip("세로로 늘었다 줄었다 하는 비율")]
    [Range(0f, 0.3f)]
    [SerializeField] private float stretch = 0.04f;

    [Tooltip("가로로 좁아졌다 넓어졌다 하는 비율. 세로와 반대로 움직여야 불처럼 보인다")]
    [Range(0f, 0.3f)]
    [SerializeField] private float squeeze = 0.025f;

    [Tooltip("좌우로 미끄러지는 거리(유닛)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float drift = 0.03f;

    [Header("속도")]
    [Tooltip("일렁이는 빠르기. 높이면 파르르 떨고, 낮추면 느긋하게 흔들린다")]
    [SerializeField] private float speed = 1.6f;

    private Vector3 restPosition;
    private Vector3 restScale;
    private Quaternion restRotation;
    private bool cached;

    // 개체마다 다른 흐름을 타게 하는 값. 없으면 불 세 개가 한 몸처럼 똑같이 흔들린다.
    private float seed;

    private void Awake()
    {
        CacheRest();
    }

    /// <summary>
    /// 원래 자세를 기억해 둔다. 매 프레임 여기서부터 다시 계산하므로 흔들림이 누적되지 않는다.
    /// Awake 순서가 보장되지 않아 값을 쓰기 전에 매번 부른다.
    /// </summary>
    private void CacheRest()
    {
        if (cached)
            return;

        cached = true;
        restPosition = transform.localPosition;
        restScale = transform.localScale;
        restRotation = transform.localRotation;
        seed = Random.value * 100f;
    }

    /// <summary>처음 자세로 되돌린다. (재시작용)</summary>
    public void ResetPose()
    {
        CacheRest();
        transform.localPosition = restPosition;
        transform.localScale = restScale;
        transform.localRotation = restRotation;
    }

    private void Update()
    {
        CacheRest();

        float t = Time.time * speed;

        // 축마다 노이즈를 어긋나게 뽑아야 각각 따로 논다. 한 값을 돌려쓰면 기계적으로 보인다.
        float sway = Noise(t, 0f);
        float breath = Noise(t, 17.3f);
        float side = Noise(t, 41.7f);

        transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, sway * swayAngle);

        // 세로로 늘 때 가로로 좁아져야 불꽃이 솟는 것처럼 보인다.
        transform.localScale = new Vector3(
            restScale.x * (1f - breath * squeeze),
            restScale.y * (1f + breath * stretch),
            restScale.z);

        transform.localPosition = restPosition + new Vector3(side * drift, 0f, 0f);
    }

    /// <summary>-1 ~ 1 로 편 Perlin 노이즈. Sin 과 달리 불규칙해서 불처럼 보인다.</summary>
    private float Noise(float t, float channel)
    {
        return Mathf.PerlinNoise(seed + channel, t) * 2f - 1f;
    }
}
