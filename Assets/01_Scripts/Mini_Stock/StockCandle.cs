using UnityEngine;

/// <summary>
/// 차트 봉 하나의 등장 연출만 담당한다. 게임 로직은 없고 진행도(0~1)를 받아 그리기만 한다.
/// 양봉은 아래 끝을 고정한 채 위로, 음봉은 위 끝을 고정한 채 아래로 쓸려나가듯 자란다.
///
/// 스프라이트를 차트 위 최종 위치/크기에 맞춰 배치해 두면 그 모습이 곧 진행도 1 이다.
/// 별도의 앵커 오브젝트를 만들 필요 없이, 봉 스프라이트에 그대로 붙여서 쓰면 된다.
/// </summary>
public class StockCandle : MonoBehaviour
{
    /// <summary>자라는 동안 고정되는 모서리.</summary>
    public enum GrowFrom
    {
        /// <summary>아래 끝을 고정하고 위로 자란다. 양봉용.</summary>
        Bottom,

        /// <summary>위 끝을 고정하고 아래로 자란다. 음봉용.</summary>
        Top,
    }

    [Header("대상")]
    [Tooltip("봉 스프라이트. 비워두면 자기 자신이나 자식에서 찾는다")]
    [SerializeField] private SpriteRenderer view;

    [Header("연출")]
    [Tooltip("어느 쪽 끝을 고정하고 자랄지. 양봉은 Bottom, 음봉은 Top")]
    [SerializeField] private GrowFrom growFrom = GrowFrom.Bottom;

    [Tooltip("진행도 보정 커브. 그대로 두면 등속으로 자란다")]
    [SerializeField] private AnimationCurve growCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private Transform target;

    // 진행도 1 일 때의 배치. 에디터에서 맞춰 둔 값을 기준으로 삼는다.
    private Vector3 basePosition;
    private Vector3 baseScale;

    // 부모 로컬 단위로 잰 봉의 세로 길이와, 트랜스폼 원점에서 스프라이트 중심까지의 거리.
    private float fullHeight;
    private float centerOffset;

    private bool cached;

    /// <summary>고정되는 모서리. 게임 쪽에서 양봉/음봉에 맞춰 바꿔줄 수 있다.</summary>
    public GrowFrom Direction
    {
        get => growFrom;
        set => growFrom = value;
    }

    private void Awake()
    {
        Cache();
    }

    /// <summary>진행도(0~1)만큼 자란 모습으로 그린다. 0이면 완전히 숨는다.</summary>
    public void Show(float progress)
    {
        Cache();

        if (target == null)
            return;

        float eased = Mathf.Clamp01(growCurve.Evaluate(Mathf.Clamp01(progress)));

        // 두께 0짜리 선이 한 줄 남는 것을 막는다.
        if (view != null)
            view.enabled = eased > 0.0001f;

        target.localScale = new Vector3(baseScale.x, baseScale.y * eased, baseScale.z);

        // 스프라이트는 트랜스폼 원점을 기준으로 축소되므로, 고정하려는 모서리가 제자리에 있도록
        // 줄어든 만큼 위치를 되밀어 준다. eased 가 1이면 원래 배치 그대로가 된다.
        float half = fullHeight * 0.5f;
        float anchor = growFrom == GrowFrom.Bottom
            ? basePosition.y + centerOffset - half   // 아래 끝
            : basePosition.y + centerOffset + half;  // 위 끝

        float y = growFrom == GrowFrom.Bottom
            ? anchor + eased * (half - centerOffset)
            : anchor - eased * (half + centerOffset);

        target.localPosition = new Vector3(basePosition.x, y, basePosition.z);
    }

    /// <summary>완전히 숨긴다. 언제 호출해도 안전하다.</summary>
    public void Hide()
    {
        Show(0f);
    }

    /// <summary>
    /// 최종 배치(진행도 1)를 한 번만 기억해 둔다.
    /// <see cref="Show"/> 가 위치/스케일을 건드리므로, 두 번 캐시하면 줄어든 상태를 원본으로 착각한다.
    /// </summary>
    private void Cache()
    {
        if (cached)
            return;

        cached = true;

        if (view == null)
            view = GetComponent<SpriteRenderer>();

        if (view == null)
            view = GetComponentInChildren<SpriteRenderer>(true);

        if (view == null)
        {
            Debug.LogWarning($"{name}: 봉 스프라이트를 찾지 못했다", this);
            return;
        }

        target = view.transform;
        basePosition = target.localPosition;
        baseScale = target.localScale;

        Sprite sprite = view.sprite;
        if (sprite == null)
            return;

        // Simple 이 아니면(Sliced/Tiled) 실제 크기는 스프라이트가 아니라 SpriteRenderer.size 가 정한다.
        // 이걸 구분하지 않으면 늘려 놓은 봉의 높이를 잘못 재서 고정하려던 모서리가 밀린다.
        float height;
        float center;

        if (view.drawMode == SpriteDrawMode.Simple)
        {
            height = sprite.bounds.size.y;
            center = sprite.bounds.center.y;
        }
        else
        {
            height = view.size.y;

            // Sliced/Tiled 는 피벗을 기준으로 size 만큼 그려진다. 피벗에서 중심까지의 거리를 구한다.
            float pivotRatio = sprite.rect.height > 0f ? sprite.pivot.y / sprite.rect.height : 0.5f;
            center = (0.5f - pivotRatio) * height;
        }

        fullHeight = height * baseScale.y;
        centerOffset = center * baseScale.y;
    }
}
