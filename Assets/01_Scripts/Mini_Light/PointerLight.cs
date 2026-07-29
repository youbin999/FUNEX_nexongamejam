using UnityEngine;

/// <summary>
/// 마우스를 따라다니는 빛. (더듬는 쪽)
/// 암흑 위에서 이 자리만 희미하게 보이게 만드는 역할이라, 보통 SpriteMask 를 붙여서 쓴다.
/// 위치는 <see cref="LightMiniGame"/> 이 매 프레임 넘겨준다.
/// </summary>
public class PointerLight : MonoBehaviour
{
    [Header("따라가기")]
    [Tooltip("포인터를 따라가는 속도. 낮출수록 빛이 늦게 따라와서 손으로 더듬는 느낌이 난다")]
    [SerializeField] private float followSpeed = 14f;

    [Header("흔들림")]
    [Tooltip("빛의 크기가 흔들리는 폭 (원래 크기 대비 비율). 0이면 흔들리지 않는다")]
    [Range(0f, 0.5f)]
    [SerializeField] private float flicker = 0.08f;

    [Tooltip("흔들리는 빠르기")]
    [SerializeField] private float flickerSpeed = 7f;

    private Vector3 restScale;
    private Vector3 restPosition;
    private bool cached;

    private Vector3 target;

    // 노이즈 시작 지점. 빛이 여러 개여도 같은 박자로 흔들리지 않게 한다.
    private float seed;

    /// <summary>손전등의 기준 위치와 크기를 기억해 둔다.</summary>
    private void Awake()
    {
        CacheRest();
    }

    /// <summary>
    /// 원래 크기/위치를 기억해 둔다.
    /// Awake 순서가 보장되지 않아 값을 쓰기 전에 매번 부른다.
    /// </summary>
    private void CacheRest()
    {
        if (cached)
            return;

        cached = true;
        restScale = transform.localScale;
        restPosition = transform.position;
        target = restPosition;
        seed = Random.value * 100f;
    }

    /// <summary>이 자리로 서서히 따라간다. 매 프레임 불러도 된다.</summary>
    public void MoveTo(Vector2 worldPosition)
    {
        CacheRest();
        target = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
    }

    /// <summary>이 자리로 즉시 옮긴다. 재생을 시작할 때 빛이 날아오지 않도록 쓴다.</summary>
    public void SnapTo(Vector2 worldPosition)
    {
        MoveTo(worldPosition);
        transform.position = target;
    }

    /// <summary>처음 자리와 크기로 되돌린다. (재시작용)</summary>
    public void ResetPose()
    {
        CacheRest();
        target = restPosition;
        transform.position = restPosition;
        transform.localScale = restScale;
    }

    /// <summary>목표 지점을 프레임 독립적으로 따라가고, 촛불처럼 크기를 흔든다.</summary>
    private void Update()
    {
        // 프레임 독립적으로 따라간다.
        transform.position = Vector3.Lerp(
            transform.position,
            target,
            1f - Mathf.Exp(-followSpeed * Time.deltaTime));

        if (flicker <= 0f)
            return;

        // Perlin 은 Sin 과 달리 불규칙해서 촛불처럼 보인다. -1~1 로 펴서 쓴다.
        float noise = Mathf.PerlinNoise(seed, Time.time * flickerSpeed) * 2f - 1f;
        transform.localScale = restScale * (1f + noise * flicker);
    }
}
