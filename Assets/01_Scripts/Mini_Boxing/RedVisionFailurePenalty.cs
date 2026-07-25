using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 복싱 패배 시 맞아서 눈앞이 붉어진 듯한 전체 화면 틴트를 남기는 패널티.
/// 강한 붉은색으로 페이드인한 뒤 약한 틴트로 가라앉으며, 새 판 시작 전까지 유지된다.
/// </summary>
public sealed class RedVisionFailurePenalty : FailurePenalty
{
    [Header("붉은 틴트")]
    [Tooltip("화면 전체를 덮는 붉은색 이미지.")]
    [SerializeField] private Image tintImage;

    [Tooltip("붉은 틴트의 투명도를 제어한다.")]
    [SerializeField] private CanvasGroup tintCanvasGroup;

    [Header("등장 연출")]
    [Tooltip("맞은 직후 가장 진한 투명도까지 올라가는 시간(초).")]
    [SerializeField] private float fadeInDuration = 0.16f;

    [Tooltip("가장 진한 틴트에서 지속 틴트로 가라앉는 시간(초).")]
    [SerializeField] private float settleDuration = 0.3f;

    [Range(0f, 1f)]
    [Tooltip("맞은 직후 순간적으로 도달할 최대 투명도.")]
    [SerializeField] private float peakAlpha = 0.55f;

    [Range(0f, 1f)]
    [Tooltip("다음 미니게임들에서도 계속 유지할 투명도.")]
    [SerializeField] private float persistentAlpha = 0.22f;

    public override IEnumerator Apply()
    {
        if (!TryPrepareTint())
            yield break;

        tintCanvasGroup.alpha = 0f;
        yield return AnimateAlpha(0f, peakAlpha, fadeInDuration);
        yield return AnimateAlpha(peakAlpha, persistentAlpha, settleDuration);
        tintCanvasGroup.alpha = Mathf.Clamp01(persistentAlpha);
    }

    private bool TryPrepareTint()
    {
        if (tintImage == null || tintCanvasGroup == null || tintImage.sprite == null)
            return false;

        tintImage.raycastTarget = false;
        tintCanvasGroup.interactable = false;
        tintCanvasGroup.blocksRaycasts = false;
        tintCanvasGroup.gameObject.SetActive(true);
        return true;
    }

    private IEnumerator AnimateAlpha(float from, float to, float duration)
    {
        duration = Mathf.Max(0f, duration);
        if (duration <= 0f)
        {
            tintCanvasGroup.alpha = Mathf.Clamp01(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            tintCanvasGroup.alpha = Mathf.LerpUnclamped(from, to, eased);
            yield return null;
        }

        tintCanvasGroup.alpha = Mathf.Clamp01(to);
    }
}
