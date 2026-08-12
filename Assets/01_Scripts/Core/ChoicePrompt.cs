using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 진행을 멈추고 두 갈래를 묻는 공용 선택창.
/// 게임 오버 갈림길(<see cref="GameOverPrompt"/>)과 엔딩의 API 안내가 이걸 함께 쓴다.
///
/// UI 는 인스펙터에 배선하면 그걸 쓰고, 비어 있으면 <see cref="SimpleUiBuilder"/> 로 만들어 붙인다 —
/// 이 창이 안 뜨면 진행이 막히므로 배선 누락으로 멈추는 일이 없어야 한다.
/// </summary>
public sealed class ChoicePrompt : MonoBehaviour
{
    [Header("참조 (비워두면 코드로 만든다)")]
    [Tooltip("실제로 켜고 끌 패널 루트")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("큰 글씨로 상황을 알리는 문구")]
    [SerializeField] private TMP_Text headlineText;

    [Tooltip("작은 글씨로 무엇을 고르는지 묻는 문구")]
    [SerializeField] private TMP_Text questionText;

    [Tooltip("긍정 선택 버튼(진행)")]
    [SerializeField] private Button primaryButton;

    [Tooltip("부정 선택 버튼(중단)")]
    [SerializeField] private Button secondaryButton;

    [Header("배치")]
    [Tooltip("오버레이 캔버스 정렬 순서. 실패 패널티(20~100) 위, 엔딩 암전(500) 아래에 두면 알맞다")]
    [SerializeField] private int canvasSortingOrder = 400;

    private TMP_Text primaryLabel;
    private TMP_Text secondaryLabel;

    /// <summary>선택 결과를 받는 쪽. true = 긍정(진행), false = 부정(중단).</summary>
    private Action<bool> onChosen;

    /// <summary>두 버튼이 겹쳐 두 번 통보되는 것을 막는다.</summary>
    private bool answered;

    /// <summary>UI 구성과 리스너 배선을 한 번만 한다.</summary>
    private bool built;

    /// <summary>창이 떠 있는지.</summary>
    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;


    // ── 열기와 선택 ──

    /// <summary>
    /// 선택창을 띄운다. <paramref name="onChosen"/> 은 정확히 한 번만 불린다.
    /// </summary>
    /// <param name="headline">큰 글씨로 보여 줄 상황 설명.</param>
    /// <param name="question">작은 글씨로 보여 줄 질문. 비워도 된다.</param>
    /// <param name="primaryLabelText">긍정 버튼 문구. 선택 시 콜백에 true 가 간다.</param>
    /// <param name="secondaryLabelText">부정 버튼 문구. 선택 시 콜백에 false 가 간다.</param>
    /// <param name="onChosen">선택 결과를 받을 콜백.</param>
    public void Show(
        string headline, string question,
        string primaryLabelText, string secondaryLabelText,
        Action<bool> onChosen)
    {
        Build();

        this.onChosen = onChosen;
        answered = false;

        if (headlineText != null)
            headlineText.text = headline ?? string.Empty;

        if (questionText != null)
            questionText.text = question ?? string.Empty;

        SetLabel(primaryLabel, primaryLabelText);
        SetLabel(secondaryLabel, secondaryLabelText);

        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    /// <summary>긍정 선택. 인스펙터 배선용으로 public 이다.</summary>
    public void ChoosePrimary() => Answer(true);

    /// <summary>부정 선택. 인스펙터 배선용으로 public 이다.</summary>
    public void ChooseSecondary() => Answer(false);

    /// <summary>선택을 확정하고 창을 닫는다. 두 번째 호출부터는 무시한다.</summary>
    private void Answer(bool primary)
    {
        if (answered)
            return;

        answered = true;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        Action<bool> callback = onChosen;
        onChosen = null;
        callback?.Invoke(primary);
    }

    /// <summary>버튼 문구를 갈아 끼운다. 인스펙터로 배선한 버튼이면 라벨을 찾아 쓴다.</summary>
    private static void SetLabel(TMP_Text label, string text)
    {
        if (label != null && !string.IsNullOrEmpty(text))
            label.text = text;
    }


    // ── 구성 ──

    /// <summary>인스펙터 배선을 확인하고, 빠진 부분을 코드로 채운다. 멱등.</summary>
    private void Build()
    {
        if (built)
            return;

        built = true;

        // 하나라도 배선이 없으면 통째로 만들어 쓴다 — 반쯤 배선된 상태를 섞으면 배치가 어긋난다.
        if (panelRoot == null || headlineText == null || primaryButton == null || secondaryButton == null)
            BuildRuntimeUi();
        else
            CacheInspectorLabels();

        if (primaryButton != null)
            primaryButton.onClick.AddListener(ChoosePrimary);

        if (secondaryButton != null)
            secondaryButton.onClick.AddListener(ChooseSecondary);

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    /// <summary>인스펙터로 배선한 버튼 안의 라벨을 찾아 둔다. 문구 교체에 쓴다.</summary>
    private void CacheInspectorLabels()
    {
        primaryLabel = primaryButton.GetComponentInChildren<TMP_Text>();
        secondaryLabel = secondaryButton.GetComponentInChildren<TMP_Text>();
    }

    /// <summary>선택창 UI 를 코드로 세운다. 화면을 덮는 어두운 판 + 문구 둘 + 버튼 둘.</summary>
    private void BuildRuntimeUi()
    {
        SimpleUiBuilder.EnsureEventSystem();

        Canvas canvas = SimpleUiBuilder.CreateOverlayCanvas("[Choice Prompt Canvas]", canvasSortingOrder);
        canvas.transform.SetParent(transform, false);

        // 화면을 덮는 판이 곧 패널 루트다. 뒤쪽 클릭도 여기서 막힌다.
        Image backdrop = SimpleUiBuilder.CreateFullScreenPanel(canvas.transform, "Backdrop", SimpleUiBuilder.Dim);
        panelRoot = backdrop.gameObject;

        headlineText = SimpleUiBuilder.CreateLabel(backdrop.transform, "Headline", string.Empty, 46f);
        SimpleUiBuilder.AnchorToCenter(headlineText.rectTransform, new Vector2(0f, 160f));
        headlineText.rectTransform.sizeDelta = new Vector2(1300f, 260f);

        questionText = SimpleUiBuilder.CreateLabel(backdrop.transform, "Question", string.Empty, 34f);
        SimpleUiBuilder.AnchorToCenter(questionText.rectTransform, new Vector2(0f, 10f));
        questionText.rectTransform.sizeDelta = new Vector2(1300f, 60f);

        var buttonSize = new Vector2(320f, 90f);

        primaryButton = SimpleUiBuilder.CreateButton(
            backdrop.transform, "PrimaryButton", string.Empty, buttonSize, 34f);
        SimpleUiBuilder.AnchorToCenter((RectTransform)primaryButton.transform, new Vector2(-180f, -120f));
        primaryLabel = primaryButton.GetComponentInChildren<TMP_Text>();

        secondaryButton = SimpleUiBuilder.CreateButton(
            backdrop.transform, "SecondaryButton", string.Empty, buttonSize, 34f);
        SimpleUiBuilder.AnchorToCenter((RectTransform)secondaryButton.transform, new Vector2(180f, -120f));
        secondaryLabel = secondaryButton.GetComponentInChildren<TMP_Text>();
    }
}
