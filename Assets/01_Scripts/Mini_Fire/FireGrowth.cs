using UnityEngine;

/// <summary>
/// 진행도(0~1)에 따라 커지는 불. (자라는 쪽)
/// <see cref="FireMiniGame.onProgressChanged"/> 에 이 컴포넌트의 <see cref="SetRatio"/> 를 연결해서 쓴다.
/// 연타 한 번마다 목표 크기가 한 단계 올라가고, 실제 크기는 그 목표를 부드럽게 따라간다.
/// </summary>
public class FireGrowth : MonoBehaviour
{
    [Header("크기")]
    // 기본값은 clearCount 20 기준. 절반인 10회에서 정확히 0.7 이 되도록 0.4 ~ 1 로 잡았다.
    [Tooltip("진행도 0일 때의 크기. 원래 크기에 곱한다")]
    [SerializeField] private float minScale = 0.4f;

    [Tooltip("진행도 1일 때의 크기. 원래 크기에 곱한다")]
    [SerializeField] private float maxScale = 1f;

    [Tooltip("진행도를 크기로 바꾸는 곡선. 뒤로 갈수록 가파르게 하면 마지막에 확 타오른다")]
    [SerializeField] private AnimationCurve growEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("따라가기")]
    [Tooltip("목표 크기를 따라가는 속도. 클수록 즉각적이다")]
    [SerializeField] private float followSpeed = 12f;

    [Tooltip("한 대 칠 때마다 순간적으로 더 부푸는 정도. 0이면 튀지 않는다")]
    [Range(0f, 0.5f)]
    [SerializeField] private float pop = 0.12f;

    [Tooltip("부푼 게 가라앉는 속도")]
    [SerializeField] private float popDecay = 8f;

    [Header("위치")]
    [Tooltip("켜면 커져도 불의 밑동이 제자리에 남는다. 끄면 중심을 기준으로 커진다")]
    [SerializeField] private bool keepBottomFixed = true;

    private Vector3 restScale;
    private Vector3 restPosition;

    // 원래 크기 기준, 피벗에서 스프라이트 밑변까지의 로컬 거리(음수).
    private float bottomOffset;
    private bool cached;

    private float targetRatio;
    private float currentRatio;
    private float popAmount;

    private void Awake()
    {
        CacheRest();
        ApplyRatio(0f, 0f);
    }

    /// <summary>
    /// 원래 크기/위치를 기억해 둔다.
    /// Awake 순서는 보장되지 않는데 다른 컴포넌트가 Awake 에서 <see cref="SetRatio"/> 를 부를 수 있으므로,
    /// 값을 쓰기 전에 매번 불러서 한 번만 캐시되게 한다.
    /// </summary>
    private void CacheRest()
    {
        if (cached)
            return;

        cached = true;
        restScale = transform.localScale;
        restPosition = transform.localPosition;

        var renderer2D = GetComponent<SpriteRenderer>();
        if (renderer2D != null && renderer2D.sprite != null)
            bottomOffset = renderer2D.sprite.bounds.min.y;
    }

    /// <summary>
    /// 진행도를 넘긴다. 0~1 범위. 0이면 즉시 처음 크기로 돌아간다(재시작 취급).
    /// </summary>
    public void SetRatio(float ratio)
    {
        CacheRest();
        ratio = Mathf.Clamp01(ratio);

        if (ratio <= 0f)
        {
            ResetPose();
            return;
        }

        // 한 단계 올라갈 때만 튄다. 같은 값이 다시 들어와도 부풀지 않는다.
        if (ratio > targetRatio)
            popAmount = pop;

        targetRatio = ratio;
    }

    /// <summary>처음 크기로 되돌린다. (재시작용)</summary>
    public void ResetPose()
    {
        CacheRest();
        targetRatio = 0f;
        currentRatio = 0f;
        popAmount = 0f;
        ApplyRatio(0f, 0f);
    }

    private void Update()
    {
        // 프레임 독립적으로 목표를 따라간다.
        currentRatio = Mathf.Lerp(currentRatio, targetRatio, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        popAmount = Mathf.Lerp(popAmount, 0f, 1f - Mathf.Exp(-popDecay * Time.deltaTime));

        ApplyRatio(currentRatio, popAmount);
    }

    private void ApplyRatio(float ratio, float extra)
    {
        float scale = Mathf.LerpUnclamped(minScale, maxScale, growEase.Evaluate(ratio)) * (1f + extra);
        transform.localScale = restScale * scale;

        if (!keepBottomFixed)
            return;

        // 원래 크기에서의 밑변 높이를 그대로 유지하도록 위치를 보정한다.
        float restBottom = restPosition.y + bottomOffset * restScale.y;
        Vector3 position = transform.localPosition;
        position.y = restBottom - bottomOffset * restScale.y * scale;
        transform.localPosition = position;
    }
}
