using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이밍 미니게임 실패 후 화면에 반투명한 검은 구멍 배열을 남기는 패널티.
/// 구멍은 한 판이 끝날 때까지 유지되며 UI 입력은 막지 않는다.
/// </summary>
public sealed class PerforatedScreenFailurePenalty : FailurePenalty
{
    [Header("구멍 배열")]
    [Min(1)]
    [Tooltip("가로로 배치할 구멍 수.")]
    [SerializeField] private int columns = 6;

    [Min(1)]
    [Tooltip("세로로 배치할 구멍 수.")]
    [SerializeField] private int rows = 3;

    [Min(1f)]
    [Tooltip("기준 해상도에서 각 구멍의 지름(픽셀).")]
    [SerializeField] private float holeDiameter = 180f;

    [Range(0f, 1f)]
    [Tooltip("검은 구멍의 최종 불투명도.")]
    [SerializeField] private float holeAlpha = 0.48f;

    [Header("등장 연출")]
    [Min(0f)]
    [Tooltip("구멍 배열이 나타나는 데 걸리는 시간(초).")]
    [SerializeField] private float fadeInDuration = 0.35f;

    [Tooltip("다른 화면 패널티보다 위에 표시할 Canvas 정렬 순서.")]
    [SerializeField] private int sortingOrder = 95;

    private CanvasGroup overlayGroup;
    private Sprite circleSprite;
    private Texture2D circleTexture;

    /// <summary>구멍 뚫린 화면 오버레이를 서서히 덮어씌운다. 이후 판이 끝날 때까지 남는다.</summary>
    public override IEnumerator Apply()
    {
        EnsureOverlay();
        if (overlayGroup == null)
            yield break;

        overlayGroup.alpha = 0f;

        float duration = Mathf.Max(0f, fadeInDuration);
        if (duration <= 0f)
        {
            overlayGroup.alpha = 1f;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            overlayGroup.alpha = t * t * (3f - 2f * t);
            yield return null;
        }

        overlayGroup.alpha = 1f;
    }

    /// <summary>런타임에 만든 오버레이와 스프라이트를 정리한다.</summary>
    private void OnDestroy()
    {
        if (circleSprite != null)
            Destroy(circleSprite);

        if (circleTexture != null)
            Destroy(circleTexture);
    }

    /// <summary>화면을 덮는 오버레이를 (없으면) 만들고 구멍을 뚫어 둔다.</summary>
    private void EnsureOverlay()
    {
        if (overlayGroup != null)
            return;

        GameObject canvasObject = new GameObject(
            "Perforated Screen Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        overlayGroup = canvasObject.GetComponent<CanvasGroup>();
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = false;

        circleSprite = CreateCircleSprite();
        if (circleSprite == null)
            return;

        int safeColumns = Mathf.Max(1, columns);
        int safeRows = Mathf.Max(1, rows);
        float safeDiameter = Mathf.Max(1f, holeDiameter);

        for (int row = 0; row < safeRows; row++)
        {
            for (int column = 0; column < safeColumns; column++)
                CreateHole(canvasObject.transform, row, column, safeRows, safeColumns, safeDiameter);
        }
    }

    /// <summary>오버레이에 구멍 하나를 배치한다.</summary>
    private void CreateHole(
        Transform parent,
        int row,
        int column,
        int safeRows,
        int safeColumns,
        float safeDiameter)
    {
        GameObject holeObject = new GameObject(
            $"Hole {row + 1}-{column + 1}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        holeObject.transform.SetParent(parent, false);

        RectTransform rect = holeObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(
            (column + 0.5f) / safeColumns,
            (row + 0.5f) / safeRows);
        rect.anchorMax = rect.anchorMin;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.one * safeDiameter;

        Image image = holeObject.GetComponent<Image>();
        image.sprite = circleSprite;
        image.color = new Color(0f, 0f, 0f, Mathf.Clamp01(holeAlpha));
        image.raycastTarget = false;
        image.preserveAspect = true;
    }

    /// <summary>구멍 모양으로 쓸 원형 스프라이트를 런타임에 생성한다.</summary>
    private Sprite CreateCircleSprite()
    {
        const int textureSize = 64;
        const float radius = (textureSize - 1) * 0.5f;
        const float feather = 1.5f;

        circleTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "Runtime Perforated Circle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[textureSize * textureSize];
        Vector2 center = Vector2.one * radius;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01((radius - distance) / feather);
                pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        circleTexture.SetPixels(pixels);
        circleTexture.Apply(false, true);

        return Sprite.Create(
            circleTexture,
            new Rect(0f, 0f, textureSize, textureSize),
            Vector2.one * 0.5f,
            100f);
    }
}
