using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 마녀 찾기 실패 패널티. 실패한 얼굴이 귀신이 되어 화면 위를 둥둥 떠다니며 시야를 방해한다.
/// 잘못된 얼굴을 골랐을 때는 푸른 틴트가 함께 적용되고, 시간 초과일 때는 반투명 효과만 적용된다.
/// 어떤 얼굴을 띄울지와 틴트 여부는 <see cref="WitchGhostPenaltyRequest"/> 로 전달받는다.
/// </summary>
public sealed class WitchGhostFailurePenalty : FailurePenalty
{
    [Header("귀신 이미지")]
    [Tooltip("화면을 떠다닐 얼굴 Image. 실패한 칸의 스프라이트로 교체된다.")]
    [SerializeField] private Image ghostImage;

    [Tooltip("요청이 비어 있을 때 사용할 대체 스프라이트. 비워두면 패널티가 표시되지 않는다.")]
    [SerializeField] private Sprite fallbackFace;

    [Tooltip("귀신 이미지 크기(px, 1920x1080 기준).")]
    [SerializeField] private Vector2 ghostSize = new Vector2(320f, 320f);

    [Header("반투명 / 틴트")]
    [Tooltip("귀신의 불투명도. 0 이면 완전 투명, 1 이면 원본 그대로.")]
    [Range(0f, 1f)]
    [SerializeField] private float ghostAlpha = 0.45f;

    [Tooltip("잘못된 얼굴을 골랐을 때 덧씌우는 푸른 틴트 색.")]
    [SerializeField] private Color ghostTintColor = new Color(0.45f, 0.75f, 1f, 1f);

    [Tooltip("귀신이 나타나는 데 걸리는 시간(초).")]
    [SerializeField] private float fadeInDuration = 0.4f;

    [Header("부유 움직임")]
    [Tooltip("화면을 떠다니는 속도(px/초, 1920x1080 기준).")]
    [SerializeField] private float floatSpeed = 160f;

    [Tooltip("직진 경로에 섞이는 좌우 흔들림 폭(px).")]
    [SerializeField] private float wobbleAmplitude = 40f;

    [Tooltip("좌우 흔들림 주기(회/초).")]
    [SerializeField] private float wobbleFrequency = 0.6f;

    [Tooltip("화면 가장자리에서 튕길 때 남기는 여백(px). 음수면 화면 밖으로 조금 빠져나간다.")]
    [SerializeField] private float edgePadding = -60f;

    private RectTransform ghostRect;
    private RectTransform canvasRect;
    private Vector2 direction = Vector2.right;
    private Vector2 basePosition;
    private float wobblePhase;
    private bool floating;

    public override IEnumerator Apply()
    {
        Sprite face = WitchGhostPenaltyRequest.ConsumeFace(out bool useTint);
        if (face == null)
            face = fallbackFace;

        if (!Prepare(face, useTint))
            yield break;

        yield return FadeIn();

        floating = true;
    }

    private void Update()
    {
        if (!floating || ghostRect == null || canvasRect == null)
            return;

        basePosition += direction * (floatSpeed * Time.deltaTime);
        BounceOnEdges();

        wobblePhase += Time.deltaTime * wobbleFrequency * Mathf.PI * 2f;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        ghostRect.anchoredPosition = basePosition + perpendicular * (Mathf.Sin(wobblePhase) * wobbleAmplitude);
    }

    /// <summary>이미지와 초기 위치/방향을 준비한다. 표시할 스프라이트가 없으면 false.</summary>
    private bool Prepare(Sprite face, bool useTint)
    {
        if (ghostImage == null || face == null)
        {
            if (ghostImage != null)
                ghostImage.gameObject.SetActive(false);
            return false;
        }

        ghostImage.gameObject.SetActive(true);
        ghostImage.sprite = face;
        ghostImage.raycastTarget = false;
        ghostImage.preserveAspect = true;

        Color color = useTint ? ghostTintColor : Color.white;
        color.a = 0f;
        ghostImage.color = color;

        ghostRect = ghostImage.rectTransform;
        ghostRect.sizeDelta = ghostSize;
        canvasRect = transform as RectTransform;

        Vector2 half = GetHalfBounds();
        basePosition = new Vector2(
            Random.Range(-half.x, half.x),
            Random.Range(-half.y, half.y));
        ghostRect.anchoredPosition = basePosition;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        wobblePhase = 0f;
        return true;
    }

    private IEnumerator FadeIn()
    {
        float targetAlpha = Mathf.Clamp01(ghostAlpha);
        float duration = Mathf.Max(0f, fadeInDuration);

        // 페이드 중에도 움직이도록 부유를 먼저 켜 둔다.
        floating = true;

        if (duration <= 0f)
        {
            SetAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(0f, targetAlpha, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    private void SetAlpha(float alpha)
    {
        if (ghostImage == null)
            return;

        Color color = ghostImage.color;
        color.a = alpha;
        ghostImage.color = color;
    }

    /// <summary>화면 경계에 닿으면 진행 방향을 반사시킨다.</summary>
    private void BounceOnEdges()
    {
        Vector2 half = GetHalfBounds();

        if (basePosition.x < -half.x && direction.x < 0f)
            direction.x = -direction.x;
        else if (basePosition.x > half.x && direction.x > 0f)
            direction.x = -direction.x;

        if (basePosition.y < -half.y && direction.y < 0f)
            direction.y = -direction.y;
        else if (basePosition.y > half.y && direction.y > 0f)
            direction.y = -direction.y;

        basePosition.x = Mathf.Clamp(basePosition.x, -half.x, half.x);
        basePosition.y = Mathf.Clamp(basePosition.y, -half.y, half.y);
    }

    /// <summary>귀신 중심이 움직일 수 있는 범위(캔버스 중심 기준 반값).</summary>
    private Vector2 GetHalfBounds()
    {
        Vector2 canvasSize = canvasRect != null ? canvasRect.rect.size : new Vector2(1920f, 1080f);
        Vector2 half = canvasSize * 0.5f - ghostSize * 0.5f - new Vector2(edgePadding, edgePadding);
        return new Vector2(Mathf.Max(0f, half.x), Mathf.Max(0f, half.y));
    }
}

/// <summary>
/// 마녀 찾기 미니게임이 실패 직전에 남기는 귀신 패널티 요청.
/// 패널티 프리팹은 코어가 생성하므로 미니게임 인스턴스를 직접 참조할 수 없어
/// 이 정적 슬롯으로 "어떤 얼굴을, 틴트를 씌워서 띄울지"만 한 번 전달한다.
/// </summary>
public static class WitchGhostPenaltyRequest
{
    private static Sprite pendingFace;
    private static bool pendingUseTint;

    /// <summary>실패 직전에 이번 패널티가 사용할 얼굴과 틴트 여부를 예약한다.</summary>
    public static void Set(Sprite face, bool useTint)
    {
        pendingFace = face;
        pendingUseTint = useTint;
    }

    /// <summary>예약된 요청을 꺼내고 비운다. 예약이 없으면 null 을 돌려준다.</summary>
    public static Sprite ConsumeFace(out bool useTint)
    {
        Sprite face = pendingFace;
        useTint = pendingUseTint;
        pendingFace = null;
        pendingUseTint = false;
        return face;
    }
}
