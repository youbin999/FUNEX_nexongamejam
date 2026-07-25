using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 갤러리 씬의 UI 를 한 번에 만들어주는 에디터 도구.
///
/// 스크롤 목록 + 상세 패널은 손으로 배치하면 잔손이 많이 가는 구조라, 뼈대를 코드로 세우고
/// 색·여백 같은 마감은 에디터에서 다듬는 편이 빠르다. 만든 결과는 평범한 UGUI 오브젝트라
/// 이 스크립트 없이도 그대로 굴러간다.
///
/// 사용법: 갤러리 씬을 연 상태에서 [Tools > Gallery > 갤러리 씬 구성] 실행.
/// 한 번 실행하면 되돌리기(Ctrl+Z) 한 번으로 전부 취소된다.
/// </summary>
public static class GallerySceneBuilder
{
    private const string CanvasName = "Gallery Canvas";
    private const string PrefabFolder = "Assets/03_Prefabs/Gallery";
    private const string PrefabPath = PrefabFolder + "/GalleryItem.prefab";

    // 1920x1080 기준으로 배치한다.
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    private static readonly Color Background = new Color(0.07f, 0.07f, 0.09f, 1f);
    private static readonly Color Panel = new Color(0.13f, 0.13f, 0.16f, 1f);
    private static readonly Color Accent = new Color(0.95f, 0.78f, 0.35f, 1f);
    private static readonly Color Ink = new Color(0.93f, 0.93f, 0.9f, 1f);
    private static readonly Color Dim = new Color(0f, 0f, 0f, 0.88f);

    [MenuItem("Tools/Gallery/갤러리 씬 구성")]
    public static void Build()
    {
        if (GameObject.Find(CanvasName) != null)
        {
            EditorUtility.DisplayDialog(
                "갤러리 씬 구성",
                $"이 씬에 이미 '{CanvasName}' 가 있습니다.\n다시 만들려면 기존 것을 지우고 실행하세요.",
                "확인");
            return;
        }

        GalleryItemView itemPrefab = BuildItemPrefab();

        RectTransform canvas = BuildCanvas();
        RectTransform listContent = BuildList(canvas);
        GameObject emptyMessage = BuildEmptyMessage(canvas);
        GalleryDetailView detail = BuildDetail(canvas);

        var screen = canvas.gameObject.AddComponent<GalleryScreen>();
        Wire(screen,
            ("content", listContent),
            ("itemPrefab", itemPrefab),
            ("emptyMessage", emptyMessage),
            ("detail", detail));

        BuildBackButton(canvas, screen);
        EnsureEventSystem();

        Undo.RegisterCreatedObjectUndo(canvas.gameObject, "갤러리 씬 구성");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = canvas.gameObject;

        RegisterSceneInBuildSettings();

        Debug.Log(
            "GallerySceneBuilder: 갤러리 UI 를 구성했습니다.\n" +
            $"아이템 프리팹: {PrefabPath}\n" +
            "씬을 저장하고, 타이틀 씬의 갤러리 버튼에 SceneNavigator 를 연결하면 끝입니다.");
    }

    /// <summary>화면 전체를 덮는 캔버스와 배경.</summary>
    private static RectTransform BuildCanvas()
    {
        var go = new GameObject(CanvasName, typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();

        var root = (RectTransform)go.transform;

        RectTransform background = NewUI("Background", root);
        Stretch(background, 0f, 0f, 0f, 0f);
        AddImage(background, Background, raycastTarget: false);

        TextMeshProUGUI title = AddText("Title", root, "GALLERY", 56f, TextAlignmentOptions.Center);
        title.color = Accent;
        title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -40f);
        title.rectTransform.sizeDelta = new Vector2(600f, 80f);

        return root;
    }

    /// <summary>스크롤 목록. 반환값은 칸이 생성될 Content.</summary>
    private static RectTransform BuildList(RectTransform canvas)
    {
        RectTransform scrollArea = NewUI("Scroll View", canvas);
        Stretch(scrollArea, 60f, 150f, 60f, 60f);
        var scroll = scrollArea.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 40f;

        // 뷰포트는 스크롤바 폭(40)만큼 왼쪽에 자리를 남긴다.
        RectTransform viewport = NewUI("Viewport", scrollArea);
        Stretch(viewport, 0f, 0f, 40f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();

        // 빈 곳을 끌어도 스크롤되도록 투명한 그래픽을 깔아둔다.
        AddImage(viewport, new Color(1f, 1f, 1f, 0.01f), raycastTarget: true);

        RectTransform content = NewUI("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(0f, 0f);
        content.offsetMax = new Vector2(0f, 0f);

        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(520f, 340f);
        grid.spacing = new Vector2(40f, 40f);
        grid.padding = new RectOffset(20, 20, 20, 20);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform scrollbar = BuildScrollbar(scrollArea);

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.verticalScrollbar = scrollbar.GetComponent<Scrollbar>();
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        return content;
    }

    private static RectTransform BuildScrollbar(RectTransform parent)
    {
        RectTransform bar = NewUI("Scrollbar Vertical", parent);
        bar.anchorMin = new Vector2(1f, 0f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(1f, 0.5f);
        bar.sizeDelta = new Vector2(24f, 0f);
        bar.anchoredPosition = Vector2.zero;
        AddImage(bar, Panel, raycastTarget: true);

        RectTransform slidingArea = NewUI("Sliding Area", bar);
        Stretch(slidingArea, 2f, 2f, 2f, 2f);

        RectTransform handle = NewUI("Handle", slidingArea);
        handle.sizeDelta = new Vector2(0f, 0f);
        Image handleImage = AddImage(handle, new Color(0.45f, 0.45f, 0.5f, 1f), raycastTarget: true);

        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;

        return bar;
    }

    private static GameObject BuildEmptyMessage(RectTransform canvas)
    {
        TextMeshProUGUI text = AddText(
            "Empty Message", canvas,
            "아직 남긴 엔딩이 없다.\n미래까지 도달해 첫 번째 역사를 남겨보자.",
            36f, TextAlignmentOptions.Center);

        text.color = new Color(0.6f, 0.6f, 0.65f, 1f);
        Stretch(text.rectTransform, 200f, 300f, 200f, 300f);
        text.gameObject.SetActive(false);

        return text.gameObject;
    }

    /// <summary>왼쪽 확대 이미지 + 오른쪽 크레딧 본문으로 나뉜 상세 패널.</summary>
    private static GalleryDetailView BuildDetail(RectTransform canvas)
    {
        RectTransform panel = NewUI("Detail Panel", canvas);
        Stretch(panel, 0f, 0f, 0f, 0f);

        // 배경을 눌러도 닫히게 한다.
        Image dim = AddImage(panel, Dim, raycastTarget: true);
        var dimButton = panel.gameObject.AddComponent<Button>();
        dimButton.targetGraphic = dim;
        dimButton.transition = Selectable.Transition.None;

        // 왼쪽: 확대 이미지.
        RectTransform imageArea = NewUI("Image", panel);
        imageArea.anchorMin = new Vector2(0f, 0f);
        imageArea.anchorMax = new Vector2(0.52f, 1f);
        imageArea.offsetMin = new Vector2(60f, 80f);
        imageArea.offsetMax = new Vector2(-20f, -80f);

        var image = imageArea.gameObject.AddComponent<RawImage>();
        var fitter = imageArea.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 16f / 9f;

        // 오른쪽: 크레딧 본문(길면 스크롤).
        RectTransform textArea = NewUI("Credit Scroll", panel);
        textArea.anchorMin = new Vector2(0.52f, 0f);
        textArea.anchorMax = new Vector2(1f, 1f);
        textArea.offsetMin = new Vector2(20f, 80f);
        textArea.offsetMax = new Vector2(-60f, -80f);

        var creditScroll = textArea.gameObject.AddComponent<ScrollRect>();
        creditScroll.horizontal = false;
        creditScroll.vertical = true;
        creditScroll.scrollSensitivity = 30f;

        RectTransform viewport = NewUI("Viewport", textArea);
        Stretch(viewport, 0f, 0f, 0f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        AddImage(viewport, new Color(1f, 1f, 1f, 0.01f), raycastTarget: true);

        RectTransform textContent = NewUI("Content", viewport);
        textContent.anchorMin = new Vector2(0f, 1f);
        textContent.anchorMax = new Vector2(1f, 1f);
        textContent.pivot = new Vector2(0.5f, 1f);
        textContent.offsetMin = Vector2.zero;
        textContent.offsetMax = Vector2.zero;

        var layout = textContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.spacing = 28f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        var contentFitter = textContent.gameObject.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI worldName = AddText("Name", textContent, string.Empty, 40f, TextAlignmentOptions.TopLeft);
        worldName.color = Ink;

        TextMeshProUGUI date = AddText("Date", textContent, string.Empty, 26f, TextAlignmentOptions.TopLeft);
        date.color = Accent;

        TextMeshProUGUI credit = AddText("Credit", textContent, string.Empty, 32f, TextAlignmentOptions.TopLeft);
        credit.color = Ink;
        credit.lineSpacing = 12f;

        TextMeshProUGUI epilogue = AddText("Epilogue", textContent, string.Empty, 34f, TextAlignmentOptions.TopLeft);
        epilogue.color = Accent;
        epilogue.fontStyle = FontStyles.Italic;

        creditScroll.viewport = viewport;
        creditScroll.content = textContent;

        var detail = panel.gameObject.AddComponent<GalleryDetailView>();
        Wire(detail,
            ("image", image),
            ("imageFitter", fitter),
            ("creditText", credit),
            ("epilogueText", epilogue),
            ("nameText", worldName),
            ("dateText", date),
            ("creditScroll", creditScroll));

        // 닫기 버튼과 배경 클릭 모두 Close 로 보낸다.
        RectTransform close = BuildButton("Close Button", panel, "닫기", detail, nameof(GalleryDetailView.Close));
        close.anchorMin = new Vector2(1f, 1f);
        close.anchorMax = new Vector2(1f, 1f);
        close.pivot = new Vector2(1f, 1f);
        close.anchoredPosition = new Vector2(-40f, -20f);

        AddPersistentClick(dimButton, detail, nameof(GalleryDetailView.Close));

        panel.gameObject.SetActive(false);

        return detail;
    }

    private static void BuildBackButton(RectTransform canvas, GalleryScreen screen)
    {
        RectTransform back = BuildButton("Back Button", canvas, "Back", screen, nameof(GalleryScreen.BackToTitle));
        back.anchorMin = new Vector2(0f, 1f);
        back.anchorMax = new Vector2(0f, 1f);
        back.pivot = new Vector2(0f, 1f);
        back.anchoredPosition = new Vector2(48f, -40f);
    }

    /// <summary>텍스트가 들어간 버튼 하나. 클릭은 대상 컴포넌트의 메서드로 바로 연결한다.</summary>
    private static RectTransform BuildButton(
        string name, RectTransform parent, string label, Object target, string methodName)
    {
        RectTransform rect = NewUI(name, parent);
        rect.sizeDelta = new Vector2(180f, 72f);

        Image image = AddImage(rect, Panel, raycastTarget: true);

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        AddPersistentClick(button, target, methodName);

        TextMeshProUGUI text = AddText("Label", rect, label, 30f, TextAlignmentOptions.Center);
        text.color = Ink;
        Stretch(text.rectTransform, 0f, 0f, 0f, 0f);

        return rect;
    }

    /// <summary>목록 칸 프리팹을 만들어 저장한다. 이미 있으면 그것을 쓴다.</summary>
    private static GalleryItemView BuildItemPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null)
            return existing.GetComponent<GalleryItemView>();

        EnsureFolder(PrefabFolder);

        var go = new GameObject("GalleryItem", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(520f, 340f);

        Image frame = AddImage(rect, Panel, raycastTarget: true);

        var button = go.AddComponent<Button>();
        button.targetGraphic = frame;

        RectTransform thumbRect = NewUI("Thumbnail", rect);
        thumbRect.anchorMin = new Vector2(0f, 0f);
        thumbRect.anchorMax = new Vector2(1f, 1f);
        // 아래쪽 78px 은 이름 + 날짜 두 줄이 쓴다.
        thumbRect.offsetMin = new Vector2(10f, 78f);
        thumbRect.offsetMax = new Vector2(-10f, -10f);
        var thumbnail = thumbRect.gameObject.AddComponent<RawImage>();

        TextMeshProUGUI worldName = AddText("Name", rect, string.Empty, 26f, TextAlignmentOptions.Center);
        worldName.color = Ink;
        worldName.textWrappingMode = TextWrappingModes.NoWrap;
        worldName.overflowMode = TextOverflowModes.Ellipsis;
        worldName.rectTransform.anchorMin = new Vector2(0f, 0f);
        worldName.rectTransform.anchorMax = new Vector2(1f, 0f);
        worldName.rectTransform.pivot = new Vector2(0.5f, 0f);
        worldName.rectTransform.offsetMin = new Vector2(10f, 42f);
        worldName.rectTransform.offsetMax = new Vector2(-10f, 76f);

        TextMeshProUGUI date = AddText("Date", rect, string.Empty, 18f, TextAlignmentOptions.Center);
        date.color = Accent;
        date.rectTransform.anchorMin = new Vector2(0f, 0f);
        date.rectTransform.anchorMax = new Vector2(1f, 0f);
        date.rectTransform.pivot = new Vector2(0.5f, 0f);
        date.rectTransform.offsetMin = new Vector2(10f, 12f);
        date.rectTransform.offsetMax = new Vector2(-10f, 38f);

        var view = go.AddComponent<GalleryItemView>();
        Wire(view,
            ("thumbnail", thumbnail),
            ("nameLabel", worldName),
            ("dateLabel", date),
            ("button", button));

        GameObject asset = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);

        return asset != null ? asset.GetComponent<GalleryItemView>() : null;
    }

    /// <summary>Input System 을 쓰는 프로젝트이므로 InputSystemUIInputModule 로 만든다.</summary>
    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Undo.RegisterCreatedObjectUndo(go, "갤러리 씬 구성");
    }

    /// <summary>갤러리 씬이 빌드에 빠져 있으면 끝에 추가한다.</summary>
    private static void RegisterSceneInBuildSettings()
    {
        string path = SceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("GallerySceneBuilder: 저장되지 않은 씬이라 빌드 설정에 등록하지 못했습니다.");
            return;
        }

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path == path)
                return;
        }

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
        {
            new EditorBuildSettingsScene(path, true),
        };

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"GallerySceneBuilder: 빌드 설정에 {path} 를 추가했습니다.");
    }

    // ── 잔손 도구들 ─────────────────────────────────────────────

    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static RectTransform Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    private static Image AddImage(RectTransform rect, Color color, bool raycastTarget)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private static TextMeshProUGUI AddText(
        string name, Transform parent, string content, float size, TextAlignmentOptions alignment)
    {
        RectTransform rect = NewUI(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Ink;
        return text;
    }

    /// <summary>인스펙터에 남는(직렬화되는) 클릭 연결을 건다. 런타임 AddListener 와 달리 씬에 저장된다.</summary>
    private static void AddPersistentClick(Button button, Object target, string methodName)
    {
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
            button.onClick, (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), target, methodName));
    }

    /// <summary>private [SerializeField] 를 이름으로 찾아 연결한다. 필드 이름이 바뀌면 여기도 같이 고쳐야 한다.</summary>
    private static void Wire(Object target, params (string field, Object value)[] pairs)
    {
        var serialized = new SerializedObject(target);

        foreach ((string field, Object value) in pairs)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"GallerySceneBuilder: {target.GetType().Name}.{field} 를 찾지 못했습니다.");
                continue;
            }

            property.objectReferenceValue = value;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}
