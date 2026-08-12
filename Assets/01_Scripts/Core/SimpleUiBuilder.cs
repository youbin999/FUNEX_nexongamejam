using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 씬 배선 없이 최소한의 UI를 코드로 세우기 위한 헬퍼.
///
/// 이 프로젝트의 UI는 원래 인스펙터로 배선하지만, 크레딧 건너뛰기 버튼이나 게임 오버 선택창처럼
/// <b>빠지면 진행이 막히는</b> 요소는 씬 배선이 누락돼도 반드시 떠 있어야 한다.
/// 그래서 인스펙터 연결이 있으면 그쪽을 쓰고, 비어 있으면 여기서 만들어 붙이는 폴백으로 쓴다.
///
/// 스타일은 의도적으로 수수하다 — 팀에서 아트를 입히고 싶으면 인스펙터에 직접 만든 UI 를 연결하면 된다.
/// </summary>
public static class SimpleUiBuilder
{
    /// <summary>기준 해상도. 프로젝트의 다른 캔버스들과 맞춰 둔다.</summary>
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    // 색은 GallerySceneBuilder 의 팔레트를 그대로 따른다 — 코드로 세운 UI 라도 갤러리와 톤이 어긋나면 안 된다.

    /// <summary>버튼·패널 바탕색.</summary>
    public static readonly Color Panel = new Color(0.13f, 0.13f, 0.16f, 1f);

    /// <summary>글자색.</summary>
    public static readonly Color Ink = new Color(0.93f, 0.93f, 0.9f, 1f);

    /// <summary>화면을 덮는 암전색.</summary>
    public static readonly Color Dim = new Color(0f, 0f, 0f, 0.88f);

    /// <summary>한글 지원 여부를 판별할 때 써 보는 글자. 'ㄱ' 계열 음절의 첫 코드포인트(U+AC00).</summary>
    private const char KoreanProbe = '가';

    private static TMP_FontAsset resolvedFont;
    private static bool fontResolved;

    /// <summary>
    /// 코드로 세운 UI 에 쓸 폰트.
    ///
    /// TMP 기본 폰트를 그냥 쓰면 안 된다 — 이 프로젝트의 기본값은 LiberationSans 이고 폴백도 비어 있어서
    /// 한글이 전부 두부(□)로 나온다. 그래서 기본 폰트가 한글을 못 그리면
    /// Resources 안의 TMP 폰트들 중 한글이 되는 것을 찾아 쓴다.
    /// </summary>
    public static TMP_FontAsset DefaultFont
    {
        get
        {
            if (!fontResolved)
            {
                fontResolved = true;
                resolvedFont = ResolveFont();
            }

            return resolvedFont;
        }
    }

    /// <summary>
    /// 코드로 세운 UI 가 쓸 폰트를 직접 지정한다. 팀에서 쓰는 폰트가 따로 있으면
    /// 아무 UI 가 만들어지기 전에(예: 부트스트랩 스크립트에서) 한 번 넣어 주면 된다.
    /// </summary>
    public static void SetFont(TMP_FontAsset font)
    {
        if (font == null)
            return;

        resolvedFont = font;
        fontResolved = true;
    }

    /// <summary>기본 폰트 → (한글이 안 되면) Resources 의 한글 가능 폰트 순으로 고른다.</summary>
    private static TMP_FontAsset ResolveFont()
    {
        TMP_FontAsset preferred = TMP_Settings.defaultFontAsset;

        // 폴백 체인까지 뒤져서 한글이 나오면 기본 폰트를 그대로 쓴다.
        if (preferred != null && preferred.HasCharacter(KoreanProbe, searchFallbacks: true))
            return preferred;

        // TMP 폰트는 관례상 Resources/Fonts & Materials 아래에 있다.
        foreach (TMP_FontAsset candidate in Resources.LoadAll<TMP_FontAsset>("Fonts & Materials"))
        {
            if (candidate != null && candidate.HasCharacter(KoreanProbe))
                return candidate;
        }

        Debug.LogWarning(
            "SimpleUiBuilder: 한글을 그릴 수 있는 TMP 폰트를 찾지 못했습니다. " +
            "코드로 만든 UI 의 한글이 깨져 보일 수 있습니다 — " +
            "TMP Settings 의 Default Font Asset 이나 Fallback 에 한글 폰트를 넣거나, " +
            $"{nameof(SimpleUiBuilder)}.{nameof(SetFont)} 로 직접 지정하세요.");

        return preferred;
    }


    // ── 캔버스 ──

    /// <summary>
    /// 화면 전체를 덮는 오버레이 캔버스를 만든다.
    /// <paramref name="sortingOrder"/> 는 다른 오버레이와의 앞뒤 관계를 정한다 —
    /// 실패 패널티(20~100)보다 크고 엔딩 암전(500)보다 작게 두면 게임 위에 뜨면서 암전에는 덮인다.
    /// </summary>
    public static Canvas CreateOverlayCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    /// <summary>화면 전체를 덮는 반투명 판을 만든다. 뒤쪽 클릭을 막는 역할도 겸한다.</summary>
    public static Image CreateFullScreenPanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        Stretch((RectTransform)go.transform);

        var image = go.GetComponent<Image>();
        image.color = color;

        return image;
    }


    // ── 텍스트와 버튼 ──

    /// <summary>부모를 가득 채우는 가운데 정렬 라벨을 만든다.</summary>
    public static TMP_Text CreateLabel(Transform parent, string name, string text, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Stretch((RectTransform)go.transform);

        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Ink;
        label.textWrappingMode = TextWrappingModes.Normal;

        // 라벨이 클릭을 가로채면 아래 버튼이 안 눌린다.
        label.raycastTarget = false;

        if (DefaultFont != null)
            label.font = DefaultFont;

        return label;
    }

    /// <summary>
    /// 반투명 배경 + 가운데 라벨로 된 버튼을 만든다.
    /// 색 전이는 Unity 기본값을 그대로 쓴다 — 어두운 배경 위에서도 눌린 티가 난다.
    /// </summary>
    public static Button CreateButton(Transform parent, string name, string label, Vector2 size, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.sizeDelta = size;

        var background = go.GetComponent<Image>();
        background.color = Panel;

        var button = go.GetComponent<Button>();
        button.targetGraphic = background;

        CreateLabel(rect, "Label", label, fontSize);

        return button;
    }


    // ── 배치 ──

    /// <summary>RectTransform 을 부모에 가득 채운다.</summary>
    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// RectTransform 을 부모 중앙 기준으로 놓는다. <paramref name="offset"/> 는 중앙에서의 이동량이며
    /// y 가 음수면 아래쪽이다. 앵커·피벗이 모두 중앙이라 해상도가 바뀌어도 가운데를 유지한다.
    /// </summary>
    public static void AnchorToCenter(RectTransform rect, Vector2 offset)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
    }

    /// <summary>
    /// RectTransform 을 화면(부모) 모서리에 고정한다.
    /// <paramref name="corner"/> 는 (0,0)=좌하 ~ (1,1)=우상. 앵커와 피벗을 같은 모서리에 두므로
    /// 해상도가 바뀌어도 그 모서리에 붙어 있는다.
    /// </summary>
    public static void AnchorToCorner(RectTransform rect, Vector2 corner, Vector2 margin)
    {
        rect.anchorMin = corner;
        rect.anchorMax = corner;
        rect.pivot = corner;

        // 모서리 안쪽으로 밀어 넣는 방향은 모서리마다 부호가 다르다.
        // 왼쪽(0)이면 +x, 오른쪽(1)이면 -x. 위아래도 같은 규칙.
        rect.anchoredPosition = new Vector2(
            Mathf.Lerp(margin.x, -margin.x, corner.x),
            Mathf.Lerp(margin.y, -margin.y, corner.y));
    }


    // ── 입력 ──

    /// <summary>
    /// 클릭을 받을 EventSystem 이 없으면 만들어 둔다.
    /// 프로젝트가 Input System 전용이므로 <see cref="InputSystemUIInputModule"/> 을 붙인다 —
    /// 레거시 StandaloneInputModule 을 붙이면 런타임에 예외가 난다.
    /// </summary>
    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        new GameObject("[EventSystem]", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }
}
