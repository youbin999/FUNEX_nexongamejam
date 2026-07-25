using UnityEngine;

/// <summary>
/// 한 점에서 빛이 확 퍼져 나가는 연출. (터지는 쪽)
/// 클리어 순간 <see cref="RubMiniGame.onClear"/> 에 <see cref="Burst"/> 를 연결해서 쓴다.
/// 여러 개를 <c>delay</c> 만 다르게 겹쳐 두면 파문처럼 연달아 퍼진다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class RadialBurst : MonoBehaviour
{
    [Header("타이밍")]
    [Tooltip("불린 뒤 터지기까지 기다리는 시간(초). 여러 개를 어긋나게 터뜨릴 때 쓴다")]
    [SerializeField] private float delay = 0f;

    [Tooltip("다 퍼지는 데 걸리는 시간(초)")]
    [SerializeField] private float duration = 0.55f;

    [Header("크기")]
    [Tooltip("터지기 시작할 때의 크기 배수")]
    [SerializeField] private float startScale = 0.2f;

    [Tooltip("다 퍼졌을 때의 크기 배수. 화면을 덮으려면 넉넉히 준다")]
    [SerializeField] private float endScale = 8f;

    [Tooltip("퍼지는 곡선. 앞이 가파를수록 '화악' 하고 터진다")]
    [SerializeField]
    private AnimationCurve expandEase = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3.5f),
        new Keyframe(1f, 1f, 0f, 0f));

    [Header("색")]
    [Tooltip("퍼지는 빛의 색")]
    [SerializeField] private Color color = new Color(0.35f, 0.7f, 1f, 1f);

    [Tooltip("수명에 따른 불투명도. 확 밝아졌다가 서서히 사라지도록 앞이 가파르다")]
    [SerializeField]
    private AnimationCurve alphaOverLife = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.12f, 1f),
        new Keyframe(1f, 0f));

    private SpriteRenderer spriteRenderer;
    private Vector3 restScale;
    private bool cached;

    // 대기 시간을 음수 구간으로 흘려보내면 delay 처리를 따로 안 해도 된다.
    private float elapsed;
    private bool playing;

    private void Awake()
    {
        CacheRest();
        Hide();
    }

    /// <summary>
    /// 원래 크기를 기억해 둔다. 매번 여기서부터 다시 계산하므로 여러 번 터뜨려도 크기가 누적되지 않는다.
    /// Awake 순서가 보장되지 않아 값을 쓰기 전에 매번 부른다.
    /// </summary>
    private void CacheRest()
    {
        if (cached)
            return;

        cached = true;
        restScale = transform.localScale;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>빛을 터뜨린다. 이미 터지는 중이면 처음부터 다시 시작한다.</summary>
    public void Burst()
    {
        CacheRest();
        elapsed = -delay;
        playing = true;
        Apply(0f);
    }

    /// <summary>꺼진 상태로 되돌린다. (재시작용)</summary>
    public void ResetPose()
    {
        CacheRest();
        playing = false;
        elapsed = 0f;
        Hide();
    }

    private void Update()
    {
        if (!playing)
            return;

        elapsed += Time.deltaTime;

        // 아직 대기 중.
        if (elapsed < 0f)
            return;

        if (duration <= 0f)
        {
            playing = false;
            Hide();
            return;
        }

        float t = elapsed / duration;

        if (t >= 1f)
        {
            playing = false;
            Hide();
            return;
        }

        Apply(t);
    }

    private void Apply(float t)
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.enabled = true;

        float scale = Mathf.LerpUnclamped(startScale, endScale, expandEase.Evaluate(t));
        transform.localScale = restScale * scale;

        Color c = color;
        c.a = color.a * alphaOverLife.Evaluate(t);
        spriteRenderer.color = c;
    }

    private void Hide()
    {
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        transform.localScale = restScale * startScale;
    }
}
