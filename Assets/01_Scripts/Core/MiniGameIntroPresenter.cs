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

    private readonly List<KeyboardGuideRowView> spawnedRows = new List<KeyboardGuideRowView>();

    private void Awake()
    {
        HideImmediate();
    }

    private void OnDisable()
    {
        HideImmediate();
    }

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

        RectTransform rect = animatedRect != null
            ? animatedRect
            : canvasGroup.GetComponent<RectTransform>();

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
        RectTransform rect = animatedRect != null
            ? animatedRect
            : canvasGroup.GetComponent<RectTransform>();
        if (rect != null)
            rect.localScale = Vector3.one * startScale;

        canvasGroup.gameObject.SetActive(false);
    }

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
