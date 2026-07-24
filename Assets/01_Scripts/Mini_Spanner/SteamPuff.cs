using UnityEngine;

/// <summary>
/// 좌우로 흔들리며 올라갔다 사라지는 증기 한 덩이.
/// 직접 씬에 두지 않고 <see cref="SteamBurst"/> 가 복제해서 뿜어낸다.
/// </summary>
public class SteamPuff : MonoBehaviour
{
    [Header("움직임")]
    [Tooltip("올라가는 속도 (초당 유닛)")]
    [SerializeField] private float riseSpeed = 2f;

    [Tooltip("좌우로 흔들리는 폭. 올라갈수록 이 폭까지 서서히 커진다")]
    [SerializeField] private float swayAmplitude = 0.5f;

    [Tooltip("좌우로 흔들리는 빠르기 (초당 왕복 횟수)")]
    [SerializeField] private float swayFrequency = 1.2f;

    [Tooltip("나타나서 완전히 사라질 때까지의 시간(초)")]
    [SerializeField] private float lifeTime = 1.6f;

    [Header("모양")]
    [Tooltip("수명에 따른 크기 배수. 올라가면서 퍼지도록 뒤로 갈수록 크게 잡는다")]
    [SerializeField]
    private AnimationCurve scaleOverLife = new AnimationCurve(
        new Keyframe(0f, 0.4f),
        new Keyframe(1f, 1.4f));

    [Tooltip("수명에 따른 불투명도. 훅 나타났다가 천천히 흩어지도록 앞이 가파르다")]
    [SerializeField]
    private AnimationCurve alphaOverLife = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.15f, 1f),
        new Keyframe(1f, 0f));

    private SpriteRenderer spriteRenderer;
    private Vector3 restScale;
    private Color restColor;
    private bool cached;

    private Vector3 origin;
    private float elapsed;

    // 덩이마다 흔들리는 시작 지점과 방향을 다르게 해야 여러 개가 한 몸처럼 움직이지 않는다.
    private float phase;
    private float swayDirection = 1f;

    private void Awake()
    {
        CacheRest();
    }

    /// <summary>
    /// 원래 크기/색을 기억해 둔다.
    /// 비활성 상태로 복제되어 Awake 가 늦게 도는 경우가 있어서 값을 쓰기 전에 매번 부른다.
    /// </summary>
    private void CacheRest()
    {
        if (cached)
            return;

        cached = true;
        restScale = transform.localScale;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            restColor = spriteRenderer.color;
    }

    /// <summary>지정한 자리에서 한 덩이를 피워 올린다. 재사용되는 오브젝트라 매번 상태를 초기화한다.</summary>
    public void Play(Vector3 worldPosition)
    {
        CacheRest();

        origin = worldPosition;
        elapsed = 0f;
        phase = Random.value;
        swayDirection = Random.value < 0.5f ? 1f : -1f;

        gameObject.SetActive(true);
        Apply();
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        if (elapsed >= lifeTime)
        {
            // 다 흩어졌으면 꺼 두고 다음 차례에 다시 쓰인다.
            gameObject.SetActive(false);
            return;
        }

        Apply();
    }

    private void Apply()
    {
        float t = lifeTime > 0f ? Mathf.Clamp01(elapsed / lifeTime) : 1f;

        // t 를 곱해서 올라갈수록 흔들림이 커진다. 처음부터 크게 흔들면 뿜어져 나온 게 아니라 떠다니는 것처럼 보인다.
        float sway = Mathf.Sin((phase + elapsed * swayFrequency) * Mathf.PI * 2f)
                     * swayAmplitude * t * swayDirection;

        transform.position = new Vector3(
            origin.x + sway,
            origin.y + riseSpeed * elapsed,
            origin.z);

        transform.localScale = restScale * scaleOverLife.Evaluate(t);

        if (spriteRenderer != null)
        {
            Color color = restColor;
            color.a = restColor.a * alphaOverLife.Evaluate(t);
            spriteRenderer.color = color;
        }
    }
}
