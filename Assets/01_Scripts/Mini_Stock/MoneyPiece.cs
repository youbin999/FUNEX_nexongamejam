using UnityEngine;

/// <summary>
/// 튀어나왔다가 떨어지며 사라지는 돈 한 조각(동전 또는 지폐).
/// 직접 씬에 두지 않고 <see cref="MoneyBurst"/> 가 복제해서 뿜어낸다.
/// 리지드바디 없이 위치를 직접 적분한다 — 콜라이더도 물리 설정도 건드리지 않아 풀링이 깔끔하다.
/// </summary>
public class MoneyPiece : MonoBehaviour
{
    [Header("움직임")]
    [Tooltip("아래로 잡아당기는 세기 (초당 유닛). 동전은 크게, 지폐는 작게 잡으면 지폐가 천천히 떠내려간다")]
    [SerializeField] private float gravity = 18f;

    [Tooltip("초당 회전 각도(도)의 범위. 조각마다 이 사이에서 무작위로 정해지고 부호도 무작위다")]
    [SerializeField] private Vector2 spinSpeedRange = new Vector2(180f, 540f);

    [Tooltip("좌우로 흔들리는 폭. 지폐가 팔랑거리게 하려면 키운다. 0이면 그냥 포물선")]
    [SerializeField] private float swayAmplitude = 0f;

    [Tooltip("좌우로 흔들리는 빠르기 (초당 왕복 횟수)")]
    [SerializeField] private float swayFrequency = 1.5f;

    [Tooltip("나타나서 완전히 사라질 때까지의 시간(초)")]
    [SerializeField] private float lifeTime = 1.2f;

    [Header("모양")]
    [Tooltip("수명에 따른 크기 배수")]
    [SerializeField]
    private AnimationCurve scaleOverLife = new AnimationCurve(
        new Keyframe(0f, 0.7f),
        new Keyframe(0.2f, 1.1f),
        new Keyframe(1f, 1f));

    [Tooltip("수명에 따른 불투명도. 튀어나올 때는 바로 보이고 끝에서 사라지도록 잡는다")]
    [SerializeField]
    private AnimationCurve alphaOverLife = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.7f, 1f),
        new Keyframe(1f, 0f));

    private SpriteRenderer spriteRenderer;
    private Vector3 restScale;
    private Color restColor;
    private bool cached;

    private Vector3 origin;
    private Vector2 velocity;
    private float elapsed;

    // 조각마다 회전과 흔들림을 다르게 해야 여러 개가 한 몸처럼 움직이지 않는다.
    private float spinSpeed;
    private float phase;

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

    /// <summary>지정한 자리에서 주어진 속도로 튀어나가게 한다. 재사용되는 오브젝트라 매번 상태를 초기화한다.</summary>
    public void Launch(Vector3 worldPosition, Vector2 initialVelocity)
    {
        CacheRest();

        origin = worldPosition;
        velocity = initialVelocity;
        elapsed = 0f;
        phase = Random.value;

        float speed = Random.Range(spinSpeedRange.x, spinSpeedRange.y);
        spinSpeed = Random.value < 0.5f ? -speed : speed;

        gameObject.SetActive(true);
        Apply();
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        if (elapsed >= lifeTime)
        {
            // 다 사라졌으면 꺼 두고 다음 차례에 다시 쓰인다.
            gameObject.SetActive(false);
            return;
        }

        Apply();
    }

    private void Apply()
    {
        float t = lifeTime > 0f ? Mathf.Clamp01(elapsed / lifeTime) : 1f;

        // 포물선. 처음 속도로 튀어올랐다가 중력에 끌려 내려온다.
        float sway = swayAmplitude > 0f
            ? Mathf.Sin((phase + elapsed * swayFrequency) * Mathf.PI * 2f) * swayAmplitude
            : 0f;

        transform.position = new Vector3(
            origin.x + velocity.x * elapsed + sway,
            origin.y + velocity.y * elapsed - 0.5f * gravity * elapsed * elapsed,
            origin.z);

        transform.localRotation = Quaternion.Euler(0f, 0f, spinSpeed * elapsed);
        transform.localScale = restScale * scaleOverLife.Evaluate(t);

        if (spriteRenderer != null)
        {
            Color color = restColor;
            color.a = restColor.a * alphaOverLife.Evaluate(t);
            spriteRenderer.color = color;
        }
    }
}
