using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 먼지 바람을 먼저 재생한 뒤 화면 전체에 렌즈 먼지 이미지를 남기는 실패 패널티.
/// 시각 에셋은 프리팹에서 ParticleSystem, Animator, CanvasGroup에 연결한다.
/// </summary>
public sealed class ScreenDirtFailurePenalty : FailurePenalty
{
    [Header("먼지 바람")]
    [Tooltip("실패 직후 재생할 먼지 파티클. 비워두면 파티클 없이 진행한다.")]
    [SerializeField] private ParticleSystem dustWindParticles;

    [Tooltip("먼지 바람 애니메이터. 비워두면 애니메이터 없이 진행한다.")]
    [SerializeField] private Animator dustWindAnimator;

    [Tooltip("먼지 바람 애니메이터에서 재생할 Trigger 이름. 비어 있으면 Trigger를 호출하지 않는다.")]
    [SerializeField] private string dustWindTrigger = "Play";

    [Tooltip("먼지 바람을 보여준 뒤 렌즈 먼지를 표시하기까지 기다릴 시간(초).")]
    [SerializeField] private float dustWindDuration = 0.8f;

    [Header("영구 렌즈 먼지")]
    [Tooltip("렌즈 먼지 UI의 CanvasGroup. 알파가 1이 된 뒤 새 판을 시작할 때까지 유지된다.")]
    [SerializeField] private CanvasGroup lensDirtCanvasGroup;

    [Tooltip("렌즈 먼지가 나타나는 데 걸리는 시간(초).")]
    [SerializeField] private float lensDirtFadeDuration = 0.2f;

    private bool dustWindDetached;


    // ── 패널티 연출 ──

    /// <summary>먼지 바람을 재생하고, 지정 시간이 지나면 렌즈 먼지를 서서히 덮어씌운다.</summary>
    public override IEnumerator Apply()
    {
        bool hasLensDirtVisual = PrepareLensDirt();

        if (dustWindParticles != null)
        {
            PrepareDustWind();
            dustWindParticles.Play(true);
        }

        if (dustWindAnimator != null && !string.IsNullOrWhiteSpace(dustWindTrigger))
            dustWindAnimator.SetTrigger(dustWindTrigger);

        float windDuration = Mathf.Max(0f, dustWindDuration);
        if (windDuration > 0f)
            yield return new WaitForSeconds(windDuration);

        if (hasLensDirtVisual)
            yield return FadeInLensDirt();
    }

    /// <summary>월드로 분리해 둔 먼지 파티클은 부모와 함께 사라지지 않으므로 직접 정리한다.</summary>
    private void OnDestroy()
    {
        if (dustWindDetached && dustWindParticles != null)
            Destroy(dustWindParticles.gameObject);
    }


    // ── 준비 ──

    /// <summary>
    /// Screen Space Overlay Canvas 아래에서는 일반 ParticleSystemRenderer가 표시되지 않으므로
    /// 재생할 때 월드 루트로 분리하고 카메라 앞 원점 기준으로 배치한다.
    /// </summary>
    private void PrepareDustWind()
    {
        Transform dustTransform = dustWindParticles.transform;
        if (!dustTransform.IsChildOf(transform))
            return;

        dustTransform.SetParent(null, false);
        dustTransform.position = Vector3.zero;
        dustTransform.rotation = Quaternion.identity;
        dustTransform.localScale = Vector3.one;
        dustWindDetached = true;
    }

    /// <summary>렌즈 먼지를 투명한 상태로 켜 둔다. 스프라이트가 없는 빈 슬롯이면 false.</summary>
    private bool PrepareLensDirt()
    {
        if (lensDirtCanvasGroup == null)
            return false;

        Image lensDirtImage = lensDirtCanvasGroup.GetComponent<Image>();
        if (lensDirtImage != null && lensDirtImage.sprite == null)
        {
            // 연결용 빈 슬롯이 기본 흰 사각형으로 화면을 가리지 않게 한다.
            lensDirtCanvasGroup.gameObject.SetActive(false);
            return false;
        }

        lensDirtCanvasGroup.gameObject.SetActive(true);
        lensDirtCanvasGroup.alpha = 0f;
        lensDirtCanvasGroup.interactable = false;
        lensDirtCanvasGroup.blocksRaycasts = false;
        return true;
    }

    /// <summary>렌즈 먼지를 서서히 드러낸다. 이후 새 판이 시작될 때까지 그대로 남는다.</summary>
    private IEnumerator FadeInLensDirt()
    {
        if (lensDirtCanvasGroup == null)
            yield break;

        float duration = Mathf.Max(0f, lensDirtFadeDuration);
        if (duration <= 0f)
        {
            lensDirtCanvasGroup.alpha = 1f;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            lensDirtCanvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        lensDirtCanvasGroup.alpha = 1f;
    }
}
