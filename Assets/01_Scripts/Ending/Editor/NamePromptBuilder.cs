using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 엔딩 씬의 "world 이름 입력 탭" UI 를 한 번에 세워주는 에디터 도구.
///
/// TMP_InputField 는 손으로 만들면 Text Area / Text / Placeholder 세 곳의 폰트를 따로 갈아줘야 해서
/// 한글이 깨지기 쉽다. 뼈대를 코드로 세우고 색·여백만 에디터에서 다듬는 편이 빠르다.
/// 만들어진 결과는 평범한 UGUI 오브젝트라 이 스크립트 없이도 그대로 굴러간다.
///
/// 사용법: 엔딩 씬(99_Ending)을 연 상태에서 [Tools > Ending > 이름 입력 탭 구성] 실행.
/// 되돌리기(Ctrl+Z) 한 번으로 전부 취소된다.
/// </summary>
public static class NamePromptBuilder
{
    private const string RootName = "Name Prompt";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/KMU80TTFSungkokSerif SDF.asset";

    private static readonly Color Dim = new Color(0f, 0f, 0f, 0.85f);
    private static readonly Color Panel = new Color(0.11f, 0.11f, 0.14f, 1f);
    private static readonly Color Field = new Color(0.18f, 0.18f, 0.22f, 1f);
    private static readonly Color Accent = new Color(0.95f, 0.78f, 0.35f, 1f);
    private static readonly Color Ink = new Color(0.93f, 0.93f, 0.9f, 1f);
    private static readonly Color Faint = new Color(0.55f, 0.55f, 0.6f, 1f);

    [MenuItem("Tools/Ending/이름 입력 탭 구성")]
    public static void Build()
    {
        if (GameObject.Find(RootName) != null)
        {
            EditorUtility.DisplayDialog(
                "이름 입력 탭 구성",
                $"이 씬에 이미 '{RootName}' 가 있습니다.\n다시 만들려면 기존 것을 지우고 실행하세요.",
                "확인");
            return;
        }

        // 루트는 항상 활성이어야 한다 — WorldNamePrompt 의 Awake 가 여기서 리스너를 묶는다.
        RectTransform root = BuildCanvas();

        RectTransform panel = NewUI("Panel", root);
        Stretch(panel, 0f, 0f, 0f, 0f);

        // 뒤쪽 UI 로 클릭이 새지 않게 전면을 막는다.
        AddImage(panel, Dim, raycastTarget: true);

        RectTransform tab = BuildTab(panel);
        BuildTitle(tab);

        RawImage preview = BuildPreview(tab, out AspectRatioFitter previewFitter);
        TMP_InputField nameField = BuildNameField(tab);
        Button confirmButton = BuildConfirmButton(tab);

        var prompt = root.gameObject.AddComponent<WorldNamePrompt>();
        Wire(prompt,
            ("panelRoot", panel.gameObject),
            ("preview", preview),
            ("previewFitter", previewFitter),
            ("nameField", nameField),
            ("confirmButton", confirmButton));

        // 확정 버튼의 OnClick 은 WorldNamePrompt.Awake 가 코드로 묶는다 —
        // 여기서 또 걸면 확정이 두 번 들어간다(중복 확정은 막혀 있지만 인스펙터가 헷갈린다).
        panel.gameObject.SetActive(false);

        LinkToController(prompt);
        EnsureEventSystem();

        Undo.RegisterCreatedObjectUndo(root.gameObject, "이름 입력 탭 구성");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = root.gameObject;

        Debug.Log(
            "NamePromptBuilder: 이름 입력 탭을 구성했습니다.\n" +
            "EndingCreditController 의 namePrompt / gallerySceneName 을 확인하고 씬을 저장하세요.");
    }

    /// <summary>
    /// 탭 전용 오버레이 캔버스.
    ///
    /// 엔딩 씬의 크레딧은 월드 스페이스 캔버스(CrawlCanvas)에서 굴러가고 EndingCanvas 는
    /// Screen Space - Camera 라, 기존 캔버스에 얹으면 크레딧 뒤로 깔릴 수 있다.
    /// Overlay 캔버스는 무엇보다 앞에 그려지므로 탭이 항상 맨 위에 뜬다.
    /// </summary>
    private static RectTransform BuildCanvas()
    {
        var go = new GameObject(RootName, typeof(RectTransform));

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();

        return (RectTransform)go.transform;
    }

    /// <summary>화면 가운데 박스.</summary>
    private static RectTransform BuildTab(RectTransform panel)
    {
        RectTransform tab = NewUI("Tab", panel);
        tab.anchorMin = new Vector2(0.5f, 0.5f);
        tab.anchorMax = new Vector2(0.5f, 0.5f);
        tab.pivot = new Vector2(0.5f, 0.5f);
        tab.anchoredPosition = Vector2.zero;
        tab.sizeDelta = new Vector2(820f, 820f);

        AddImage(tab, Panel, raycastTarget: true);

        return tab;
    }

    /// <summary>탭 상단의 안내 문구를 만든다.</summary>
    private static void BuildTitle(RectTransform tab)
    {
        TextMeshProUGUI title = AddText(
            "Title", tab, "당신의 world의 이름을 정해주세요", 42f, TextAlignmentOptions.Center);
        title.color = Accent;
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -36f);
        title.rectTransform.sizeDelta = new Vector2(-64f, 100f);
    }

    /// <summary>
    /// 방금 만들어진 엔딩 이미지의 축소본.
    /// AspectRatioFitter(FitInParent)는 자기 RectTransform 을 부모에 꽉 맞추므로,
    /// 자리를 잡아주는 빈 컨테이너를 한 겹 두고 그 안에서만 비율을 맞춘다.
    /// </summary>
    private static RawImage BuildPreview(RectTransform tab, out AspectRatioFitter fitter)
    {
        RectTransform area = NewUI("Preview Area", tab);
        area.anchorMin = new Vector2(0f, 1f);
        area.anchorMax = new Vector2(1f, 1f);
        area.pivot = new Vector2(0.5f, 1f);
        area.anchoredPosition = new Vector2(0f, -150f);
        area.sizeDelta = new Vector2(-100f, 400f);

        RectTransform imageRect = NewUI("Preview", area);
        var image = imageRect.gameObject.AddComponent<RawImage>();

        fitter = imageRect.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 16f / 9f;

        return image;
    }

    /// <summary>이름을 받는 입력 칸. 폰트는 Text/Placeholder 양쪽 모두에 물린다.</summary>
    private static TMP_InputField BuildNameField(RectTransform tab)
    {
        RectTransform fieldRect = NewUI("Name Field", tab);
        fieldRect.anchorMin = new Vector2(0.5f, 0f);
        fieldRect.anchorMax = new Vector2(0.5f, 0f);
        fieldRect.pivot = new Vector2(0.5f, 0f);
        fieldRect.anchoredPosition = new Vector2(0f, 150f);
        fieldRect.sizeDelta = new Vector2(640f, 88f);

        Image background = AddImage(fieldRect, Field, raycastTarget: true);

        RectTransform textArea = NewUI("Text Area", fieldRect);
        Stretch(textArea, 24f, 12f, 24f, 12f);
        textArea.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = AddText(
            "Placeholder", textArea, "이름을 입력하세요", 34f, TextAlignmentOptions.MidlineLeft);
        placeholder.color = Faint;
        placeholder.fontStyle = FontStyles.Italic;
        Stretch(placeholder.rectTransform, 0f, 0f, 0f, 0f);

        TextMeshProUGUI text = AddText(
            "Text", textArea, string.Empty, 34f, TextAlignmentOptions.MidlineLeft);
        text.color = Ink;
        text.richText = false;
        Stretch(text.rectTransform, 0f, 0f, 0f, 0f);

        var input = fieldRect.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = textArea;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.targetGraphic = background;
        input.fontAsset = LoadFont();
        input.pointSize = 34f;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = GalleryStore.MaxWorldNameLength;
        input.caretColor = Accent;
        input.customCaretColor = true;

        return input;
    }

    /// <summary>이름을 확정하는 버튼을 만든다.</summary>
    private static Button BuildConfirmButton(RectTransform tab)
    {
        RectTransform rect = NewUI("Confirm Button", tab);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 40f);
        rect.sizeDelta = new Vector2(260f, 84f);

        Image image = AddImage(rect, Field, raycastTarget: true);

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI label = AddText("Label", rect, "확정", 36f, TextAlignmentOptions.Center);
        label.color = Accent;
        Stretch(label.rectTransform, 0f, 0f, 0f, 0f);

        return button;
    }

    /// <summary>씬에 EndingCreditController 가 있으면 namePrompt 를 대신 꽂아준다.</summary>
    private static void LinkToController(WorldNamePrompt prompt)
    {
        var controller = Object.FindFirstObjectByType<EndingCreditController>();
        if (controller == null)
        {
            Debug.LogWarning("NamePromptBuilder: EndingCreditController 를 찾지 못해 namePrompt 는 직접 연결해야 합니다.");
            return;
        }

        Wire(controller, ("namePrompt", prompt));
        EditorUtility.SetDirty(controller);
    }

    /// <summary>한글이 깨지지 않도록 프로젝트 폰트를 쓴다. 없으면 TMP 기본 폰트로 둔다.</summary>
    private static TMP_FontAsset LoadFont()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            Debug.LogWarning($"NamePromptBuilder: 폰트를 찾지 못했습니다 — {FontPath}");

        return font;
    }

    /// <summary>씬에 EventSystem 이 없으면 하나 만든다. 없으면 UI 입력이 먹지 않는다.</summary>
    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Undo.RegisterCreatedObjectUndo(go, "이름 입력 탭 구성");
    }

    // ── 잔손 도구들 ─────────────────────────────────────────────

    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>부모 영역에 맞춰 늘리고 사방 여백을 준다.</summary>
    private static RectTransform Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    /// <summary>단색 Image 를 붙인다.</summary>
    private static Image AddImage(RectTransform rect, Color color, bool raycastTarget)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    /// <summary>기본 서식을 적용한 TMP 텍스트를 붙인다.</summary>
    private static TextMeshProUGUI AddText(
        string name, Transform parent, string content, float size, TextAlignmentOptions alignment)
    {
        RectTransform rect = NewUI(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();

        TMP_FontAsset font = LoadFont();
        if (font != null)
            text.font = font;

        text.text = content;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Ink;
        return text;
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
                Debug.LogError($"NamePromptBuilder: {target.GetType().Name}.{field} 를 찾지 못했습니다.");
                continue;
            }

            property.objectReferenceValue = value;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
