using UnityEngine;

/// <summary>
/// 스프라이트를 다른 그림으로 바꿨다가 되돌린다.
/// 꺼진 전구 → 빛나는 전구처럼 상태가 바뀌는 연출에 쓴다.
/// 켜는 순간 <see cref="SwapOn"/>, 다음 판을 시작할 때 <see cref="ResetPose"/> 를 연결한다.
/// </summary>
public class SpriteSwapper : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("그림을 바꿀 렌더러. 비워두면 자기 자신에게서 찾는다")]
    [SerializeField] private SpriteRenderer target;

    [Header("바뀔 모습")]
    [Tooltip("켜졌을 때의 그림")]
    [SerializeField] private Sprite onSprite;

    [Tooltip("켜졌을 때 색도 같이 바꾼다. 꺼진 전구를 어둡게 깔아 뒀다면 켜고 흰색으로 되돌리는 식")]
    [SerializeField] private bool changeColor = true;

    [SerializeField] private Color onColor = Color.white;

    private Sprite restSprite;
    private Color restColor;
    private bool cached;

    private void Awake()
    {
        CacheRest();
    }

    /// <summary>
    /// 원래 그림과 색을 기억해 둔다.
    /// Awake 순서가 보장되지 않아 값을 쓰기 전에 매번 부른다.
    /// </summary>
    private void CacheRest()
    {
        if (cached)
            return;

        cached = true;

        if (target == null)
            target = GetComponent<SpriteRenderer>();

        if (target == null)
            return;

        restSprite = target.sprite;
        restColor = target.color;
    }

    /// <summary>켜진 모습으로 바꾼다.</summary>
    public void SwapOn()
    {
        CacheRest();

        if (target == null)
            return;

        if (onSprite != null)
            target.sprite = onSprite;

        if (changeColor)
            target.color = onColor;
    }

    /// <summary>처음 모습으로 되돌린다. (재시작용)</summary>
    public void ResetPose()
    {
        CacheRest();

        if (target == null)
            return;

        target.sprite = restSprite;
        target.color = restColor;
    }
}
