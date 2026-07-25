using System.Collections;
using UnityEngine;

/// <summary>
/// 투명도만으로 나타났다 사라지는 연출. 튀어나오는 움직임 없이 그 자리에서 스윽 떴다 사라진다.
/// 실패 연출용으로 <see cref="StockMiniGame.onFail"/> 에 <see cref="Show"/> 를,
/// <see cref="StockMiniGame.onResetEffects"/> 에 <see cref="HideImmediate"/> 를 연결해서 쓴다.
///
/// 오브젝트 자체를 껐다 켜지 않고 알파로만 숨긴다. 꺼 두면 코루틴이 돌지 않아 다시 띄울 수가 없다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class FadePopup : MonoBehaviour
{
    [Header("시간")]
    [Tooltip("나타나는 데 걸리는 시간(초)")]
    [SerializeField] private float fadeInDuration = 0.15f;

    [Tooltip("다 나타난 채로 머무는 시간(초)")]
    [SerializeField] private float holdDuration = 0.35f;

    [Tooltip("사라지는 데 걸리는 시간(초). 0이면 사라지지 않고 그대로 남는다")]
    [SerializeField] private float fadeOutDuration = 0.2f;

    [Header("모양")]
    [Tooltip("가장 진할 때의 불투명도")]
    [Range(0f, 1f)]
    [SerializeField] private float peakAlpha = 1f;

    private CanvasGroup group;
    private Coroutine running;

    private void Awake()
    {
        Cache();

        // 재생 전에는 보이지 않아야 한다. 게임 진행에 해당하는 동작은 하지 않는다.
        group.alpha = 0f;
    }

    private void Cache()
    {
        if (group == null)
            group = GetComponent<CanvasGroup>();
    }

    /// <summary>나타났다가 다시 사라진다. 이미 떠 있었다면 처음부터 다시 재생한다.</summary>
    public void Show()
    {
        Cache();

        if (!isActiveAndEnabled)
        {
            // 코루틴을 돌릴 수 없는 상태라면 최소한 보이기라도 하게 둔다.
            group.alpha = peakAlpha;
            return;
        }

        if (running != null)
            StopCoroutine(running);

        running = StartCoroutine(ShowRoutine());
    }

    /// <summary>즉시 감춘다. 재생 중이 아닐 때 호출돼도 안전하다.</summary>
    public void HideImmediate()
    {
        Cache();

        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        group.alpha = 0f;
    }

    private IEnumerator ShowRoutine()
    {
        yield return FadeTo(peakAlpha, fadeInDuration);

        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);

        if (fadeOutDuration > 0f)
            yield return FadeTo(0f, fadeOutDuration);

        running = null;
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        float from = group.alpha;

        if (duration <= 0f)
        {
            group.alpha = target;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        group.alpha = target;
    }
}
