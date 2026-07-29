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

    [Header("꺼짐")]
    [Tooltip("실패했을 때 불이 사그라들어 사라지는 시간(초). 0이면 즉시 사라진다")]
    [SerializeField] private float extinguishTime = 0.3f;

    private Vector3 restScale;
    private Vector3 restPosition;
    private SpriteRenderer spriteRenderer;
    private Color restColor;

    // 원래 크기 기준, 피벗에서 스프라이트 밑변까지의 로컬 거리(음수).
    private float bottomOffset;
    private bool cached;

    private float targetRatio;
    private float currentRatio;
    private float popAmount;

    // 1 이면 그대로 보이고, 0 이면 완전히 꺼진 상태. 크기와 투명도에 함께 곱한다.
    private float visibility = 1f;
    private bool extinguishing;

    /// <summary>기준 크기를 기억하고 불이 꺼진 상태로 시작한다.</summary>
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

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            restColor = spriteRenderer.color;

            if (spriteRenderer.sprite != null)
                bottomOffset = spriteRenderer.sprite.bounds.min.y;
        }
    }

    /// <summary>
    /// 불을 꺼뜨린다. 실패 연출용으로 <see cref="FireMiniGame.onFail"/> 에 연결한다.
    /// 사그라들면서 완전히 사라지고, 다음 판을 시작하면 되살아난다.
    /// </summary>
    public void Extinguish()
    {
        CacheRest();
        extinguishing = true;
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

        // 꺼져 있었더라도 다시 불을 붙일 수 있어야 한다.
        extinguishing = false;
        visibility = 1f;
        if (spriteRenderer != null)
            spriteRenderer.enabled = true;

        ApplyRatio(0f, 0f);
    }

    /// <summary>목표 크기를 프레임 독립적으로 따라가고, 꺼지는 중이면 서서히 사그라든다.</summary>
    private void Update()
    {
        // 프레임 독립적으로 목표를 따라간다.
        currentRatio = Mathf.Lerp(currentRatio, targetRatio, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        popAmount = Mathf.Lerp(popAmount, 0f, 1f - Mathf.Exp(-popDecay * Time.deltaTime));

        if (extinguishing && visibility > 0f)
        {
            visibility = extinguishTime > 0f
                ? Mathf.MoveTowards(visibility, 0f, Time.deltaTime / extinguishTime)
                : 0f;

            // 크기가 0이어도 보이지 않지만, 확실히 없애기 위해 렌더러까지 끈다.
            if (visibility <= 0f && spriteRenderer != null)
                spriteRenderer.enabled = false;
        }

        ApplyRatio(currentRatio, popAmount);
    }

    /// <summary>비율을 크기·투명도에 반영한다. 밑변 고정 옵션이 켜져 있으면 위치도 보정한다.</summary>
    private void ApplyRatio(float ratio, float extra)
    {
        // 꺼지는 중이면 크기와 투명도가 함께 줄어서 사그라드는 것처럼 보인다.
        float scale = Mathf.LerpUnclamped(minScale, maxScale, growEase.Evaluate(ratio)) * (1f + extra) * visibility;
        transform.localScale = restScale * scale;

        if (spriteRenderer != null)
        {
            Color color = restColor;
            color.a = restColor.a * visibility;
            spriteRenderer.color = color;
        }

        if (!keepBottomFixed)
            return;

        // 원래 크기에서의 밑변 높이를 그대로 유지하도록 위치를 보정한다.
        float restBottom = restPosition.y + bottomOffset * restScale.y;
        Vector3 position = transform.localPosition;
        position.y = restBottom - bottomOffset * restScale.y * scale;
        transform.localPosition = position;
    }
}
