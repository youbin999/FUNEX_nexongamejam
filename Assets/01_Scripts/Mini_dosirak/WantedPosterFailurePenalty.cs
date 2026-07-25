using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도시락 폭탄 투척 실패 시 화면 구석에 수배 포스터를 붙이는 패널티.
/// 등장 연출이 끝난 포스터는 새 판을 시작할 때까지 화면 일부를 가린다.
/// </summary>
public sealed class WantedPosterFailurePenalty : FailurePenalty
{
    [Header("포스터")]
    [Tooltip("화면 구석에 표시할 포스터 이미지.")]
    [SerializeField] private Image posterImage;

    [Tooltip("포스터의 투명도와 입력 차단 여부를 제어한다.")]
    [SerializeField] private CanvasGroup posterCanvasGroup;

    [Header("등장 연출")]
    [Tooltip("포스터가 화면 안으로 미끄러져 들어오는 시간(초).")]
    [SerializeField] private float introDuration = 0.35f;

    [Tooltip("등장 시작 위치에 더할 화면 좌표 오프셋.")]
    [SerializeField] private Vector2 introOffset = new Vector2(180f, 120f);

    [Tooltip("등장 시작 시 포스터의 회전 각도.")]
    [SerializeField] private float introRotation = -12f;

    public override IEnumerator Apply()
    {
        if (!TryPreparePoster(out RectTransform posterTransform))
            yield break;

        Vector2 endPosition = posterTransform.anchoredPosition;
        Vector2 startPosition = endPosition + introOffset;
        float duration = Mathf.Max(0f, introDuration);

        posterTransform.anchoredPosition = startPosition;
        posterTransform.localRotation = Quaternion.Euler(0f, 0f, introRotation);
        posterCanvasGroup.alpha = 0f;

        if (duration <= 0f)
        {
            SetFinalState(posterTransform, endPosition);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            posterTransform.anchoredPosition = Vector2.LerpUnclamped(startPosition, endPosition, eased);
            posterTransform.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.LerpUnclamped(introRotation, 0f, eased));
            posterCanvasGroup.alpha = eased;
            yield return null;
        }

        SetFinalState(posterTransform, endPosition);
    }

    private bool TryPreparePoster(out RectTransform posterTransform)
    {
        posterTransform = null;
        if (posterImage == null || posterCanvasGroup == null || posterImage.sprite == null)
            return false;

        posterImage.raycastTarget = false;
        posterCanvasGroup.interactable = false;
        posterCanvasGroup.blocksRaycasts = false;
        posterCanvasGroup.gameObject.SetActive(true);
        posterTransform = posterImage.rectTransform;
        return true;
    }

    private void SetFinalState(RectTransform posterTransform, Vector2 endPosition)
    {
        posterTransform.anchoredPosition = endPosition;
        posterTransform.localRotation = Quaternion.identity;
        posterCanvasGroup.alpha = 1f;
    }
}
