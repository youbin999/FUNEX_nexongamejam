using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 미니게임 시작 전 설명과 키보드 조작 가이드를 표시한다.
/// <see cref="MiniGamePlayer"/> 는 이 Presenter 의 완료만 기다리고 실제 UI 구성은 알지 않는다.
/// </summary>
public sealed class MiniGameIntroPresenter : MonoBehaviour
{
    [Header("인트로 UI")]
    [Tooltip("설명과 키 가이드 전체를 표시하는 CanvasGroup")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("게임 설명을 표시할 텍스트")]
    [SerializeField] private TMP_Text descriptionText;

    [Tooltip("스케일 애니메이션을 적용할 RectTransform. 비워두면 CanvasGroup 의 RectTransform 을 사용한다")]
    [SerializeField] private RectTransform animatedRect;

    [Header("키 가이드")]
    [Tooltip("생성한 키 가이드 행을 배치할 부모. 유효한 가이드가 없으면 숨긴다")]
    [SerializeField] private RectTransform guideContainer;

    [Tooltip("키 목록과 행동 설명을 표시하는 행 프리팹")]
    [SerializeField] private KeyboardGuideRowView guideRowPrefab;

    [Header("연출")]
    [Tooltip("등장/퇴장 애니메이션 시간(초)")]
    [SerializeField] private float transitionDuration = 0.25f;

    [Tooltip("설명과 키 가이드를 화면에 유지하는 시간(초)")]
    [SerializeField] private float holdDuration = 0.75f;

    [Tooltip("등장 시작/퇴장 종료 시점의 스케일 값")]
    [SerializeField] private float startScale = 0.8f;

    [Header("설명 텍스트 가독성")]
    [Tooltip("설명 글자에 두를 테두리 두께(0~1). 밝은 배경(예: 주식 게임)에서 글자가 묻히는 걸 막는다. 0이면 테두리 없음")]
    [Range(0f, 1f)]
    [SerializeField] private float descriptionOutlineWidth = 0.15f;

    [Tooltip("설명 글자 테두리 색")]
    [SerializeField] private Color descriptionOutlineColor = Color.black;

    // ShaderUtilities 의 상수 이름은 TMP 버전마다 달라질 수 있어 프로퍼티를 직접 잡는다.
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

    private readonly List<KeyboardGuideRowView> spawnedRows = new List<KeyboardGuideRowView>();

    // ── 수명주기 ──

    /// <summary>UI를 감춘 상태로 시작하고 설명 글자 테두리를 적용한다.</summary>
    private void Awake()
    {
        HideImmediate();
        ApplyDescriptionOutline();
    }

    /// <summary>중간에 꺼져도 UI가 남지 않도록 즉시 되돌린다.</summary>
    private void OnDisable()
    {
        HideImmediate();
    }

    /// <summary>
    /// 설명 글자에 테두리를 두른다. 흰 배경 위에서도 글자가 읽히게 하는 게 목적이다.
    ///
    /// <c>fontMaterial</c> 은 이 텍스트 전용 머티리얼 인스턴스를 만들어 돌려준다.
    /// <c>fontSharedMaterial</c> 을 건드리면 같은 폰트를 쓰는 다른 텍스트는 물론
    /// 프로젝트의 머티리얼 에셋까지 바뀌므로 쓰지 않는다.
    /// </summary>
    private void ApplyDescriptionOutline()
    {
        if (descriptionText == null || descriptionOutlineWidth <= 0f)
            return;

        Material material = descriptionText.fontMaterial;
        if (material == null)
            return;

        material.EnableKeyword("OUTLINE_ON");
        material.SetFloat(OutlineWidthId, descriptionOutlineWidth);
        material.SetColor(OutlineColorId, descriptionOutlineColor);
    }


    // ── 표시와 숨김 ──

    /// <summary>미니게임 정보를 표시하고 퇴장 연출까지 마칠 때까지 기다린다.</summary>
    public IEnumerator Present(MiniGame game)
    {
        HideImmediate();

        if (game == null || canvasGroup == null)
            yield break;

        if (descriptionText != null)
            descriptionText.text = game.GameDescription;

        BuildGuideRows(game.KeyboardGuides);

        bool hasDescription = !string.IsNullOrWhiteSpace(game.GameDescription);
        bool hasGuides = spawnedRows.Count > 0;
        if (!hasDescription && !hasGuides)
            yield break;

        RectTransform rect = ResolveAnimatedRect();

        canvasGroup.gameObject.SetActive(true);
        yield return Animate(rect, 0f, 1f, startScale, 1f);

        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);

        yield return Animate(rect, 1f, 0f, 1f, startScale);
        HideImmediate();
    }

    /// <summary>진행 중인 연출과 관계없이 UI를 즉시 초기 상태로 되돌린다.</summary>
    public void HideImmediate()
    {
        ClearGuideRows();

        if (guideContainer != null)
            guideContainer.gameObject.SetActive(false);

        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 0f;

        RectTransform rect = ResolveAnimatedRect();
        if (rect != null)
            rect.localScale = Vector3.one * startScale;

        canvasGroup.gameObject.SetActive(false);
    }

    /// <summary>스케일 연출 대상 RectTransform. 지정이 없으면 CanvasGroup 의 것을 쓴다.</summary>
    private RectTransform ResolveAnimatedRect()
    {
        if (animatedRect != null)
            return animatedRect;

        return canvasGroup != null ? canvasGroup.GetComponent<RectTransform>() : null;
    }


    // ── 키 가이드 행 ──

    /// <summary>유효한 키가 하나라도 있는 가이드마다 행을 만들어 컨테이너에 붙인다.</summary>
    private void BuildGuideRows(IReadOnlyList<KeyboardGuideEntry> guides)
    {
        if (guideContainer == null || guideRowPrefab == null || guides == null)
            return;

        foreach (KeyboardGuideEntry guide in guides)
        {
            if (!HasValidKey(guide))
                continue;

            KeyboardGuideRowView row = Instantiate(guideRowPrefab, guideContainer);
            row.gameObject.SetActive(true);
            row.Bind(guide);
            spawnedRows.Add(row);
        }

        guideContainer.gameObject.SetActive(spawnedRows.Count > 0);
    }

    /// <summary>표시할 만한 키가 하나라도 있는지. 전부 None 이면 행을 만들지 않는다.</summary>
    private static bool HasValidKey(KeyboardGuideEntry guide)
    {
        if (guide == null)
            return false;

        foreach (UnityEngine.InputSystem.Key key in guide.Keys)
        {
            if (key != UnityEngine.InputSystem.Key.None)
                return true;
        }

        return false;
    }

    /// <summary>생성해 둔 가이드 행을 전부 파괴하고 목록을 비운다.</summary>
    private void ClearGuideRows()
    {
        foreach (KeyboardGuideRowView row in spawnedRows)
        {
            if (row != null)
            {
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
        }

        spawnedRows.Clear();
    }


    // ── 등장·퇴장 연출 ──

    /// <summary>투명도와 크기를 동시에 보간한다. 연출 시간이 0이면 목표값을 즉시 적용한다.</summary>
    private IEnumerator Animate(RectTransform rect, float fromAlpha, float toAlpha, float fromScale, float toScale)
    {
        if (transitionDuration <= 0f)
        {
            canvasGroup.alpha = toAlpha;
            if (rect != null)
                rect.localScale = Vector3.one * toScale;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / transitionDuration);
            canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, ratio);
            if (rect != null)
                rect.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, ratio);
            yield return null;
        }

        canvasGroup.alpha = toAlpha;
        if (rect != null)
            rect.localScale = Vector3.one * toScale;
    }
}
