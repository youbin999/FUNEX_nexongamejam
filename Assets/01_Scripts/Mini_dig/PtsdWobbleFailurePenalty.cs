using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 참호 파기에 실패했을 때 붙는 "PTSD 화면 일렁임" 패널티.
/// 실패 직후에는 강한 일렁임과 셰이크로 크게 흔들고, 잠시 뒤 약한 일렁임으로 가라앉혀
/// 새 판을 시작할 때까지 계속 화면이 미묘하게 흔들리게 만든다.
/// 화면 가장자리 비네트도 함께 남는다.
/// </summary>
public sealed class PtsdWobbleFailurePenalty : FailurePenalty
{
    [Header("실패 직후 버스트")]
    [Tooltip("강한 일렁임을 유지하는 시간(초).")]
    [SerializeField] private float burstDuration = 1.5f;

    [Tooltip("버스트 일렁임의 위치 흔들림 크기(월드 단위).")]
    [SerializeField] private float burstPositionAmplitude = 0.35f;

    [Tooltip("버스트 일렁임의 Z축 회전 흔들림 크기(도).")]
    [SerializeField] private float burstRotationAmplitude = 3.5f;

    [Tooltip("버스트 일렁임의 직교 카메라 크기 맥동 폭.")]
    [SerializeField] private float burstSizeAmplitude = 0.35f;

    [Tooltip("버스트 일렁임의 기준 주파수(Hz).")]
    [SerializeField] private float burstFrequency = 1.6f;

    [Tooltip("실패 순간 한 번 터뜨릴 셰이크 시간(초). 0이면 셰이크하지 않는다.")]
    [SerializeField] private float shakeDuration = 0.4f;

    [Tooltip("실패 순간 셰이크 진폭.")]
    [SerializeField] private float shakeAmplitude = 0.25f;

    [Header("지속 일렁임")]
    [Tooltip("버스트 이후 계속 남는 일렁임의 위치 흔들림 크기.")]
    [SerializeField] private float lingerPositionAmplitude = 0.08f;

    [Tooltip("버스트 이후 계속 남는 일렁임의 Z축 회전 흔들림 크기(도).")]
    [SerializeField] private float lingerRotationAmplitude = 0.8f;

    [Tooltip("버스트 이후 계속 남는 일렁임의 직교 카메라 크기 맥동 폭.")]
    [SerializeField] private float lingerSizeAmplitude = 0.08f;

    [Tooltip("버스트 이후 계속 남는 일렁임의 기준 주파수(Hz).")]
    [SerializeField] private float lingerFrequency = 0.6f;

    [Header("비네트")]
    [Tooltip("화면 가장자리를 어둡게 덮는 비네트의 CanvasGroup. 비워두면 비네트 없이 진행한다.")]
    [SerializeField] private CanvasGroup vignetteGroup;

    [Tooltip("비네트가 나타나는 데 걸리는 시간(초).")]
    [SerializeField] private float vignetteFadeDuration = 0.6f;

    [Tooltip("비네트가 도달할 최종 알파.")]
    [Range(0f, 1f)]
    [SerializeField] private float vignetteTargetAlpha = 0.5f;

    private int burstWobbleId;
    private int lingerWobbleId;

    /// <summary>강한 일렁임과 셰이크로 시작해 비네트를 덮은 뒤, 오래 남을 약한 일렁임으로 갈아 끼운다.</summary>
    public override IEnumerator Apply()
    {
        CameraEffectManager effects = CameraEffectManager.Instance;

        burstWobbleId = effects.StartWobble(
            burstPositionAmplitude, burstRotationAmplitude, burstSizeAmplitude, burstFrequency);

        if (shakeDuration > 0f)
            effects.Shake(shakeDuration, shakeAmplitude);

        bool hasVignette = PrepareVignette();

        float burst = Mathf.Max(0f, burstDuration);
        if (hasVignette)
        {
            yield return FadeInVignette();

            float rest = burst - Mathf.Max(0f, vignetteFadeDuration);
            if (rest > 0f)
                yield return new WaitForSecondsRealtime(rest);
        }
        else if (burst > 0f)
        {
            yield return new WaitForSecondsRealtime(burst);
        }

        // 강한 일렁임을 걷어내고 오래 남을 약한 일렁임으로 갈아 끼운다.
        effects.StopWobble(burstWobbleId);
        burstWobbleId = 0;

        lingerWobbleId = effects.StartWobble(
            lingerPositionAmplitude, lingerRotationAmplitude, lingerSizeAmplitude, lingerFrequency);
    }

    /// <summary>남아 있던 일렁임을 걷어낸다. 안 지우면 다음 판까지 화면이 흔들린다.</summary>
    private void OnDestroy()
    {
        // 매니저가 이미 사라졌으면 정리할 것도 없다. 여기서 새로 만들지 않는다.
        CameraEffectManager effects = CameraEffectManager.TryGetInstance;
        if (effects == null)
            return;

        if (burstWobbleId != 0)
            effects.StopWobble(burstWobbleId);

        if (lingerWobbleId != 0)
            effects.StopWobble(lingerWobbleId);
    }

    /// <summary>비네트를 투명한 상태로 켜 둔다. 연결된 것이 없으면 false.</summary>
    private bool PrepareVignette()
    {
        if (vignetteGroup == null)
            return false;

        Image vignetteImage = vignetteGroup.GetComponent<Image>();
        if (vignetteImage != null)
            vignetteImage.raycastTarget = false;

        vignetteGroup.gameObject.SetActive(true);
        vignetteGroup.alpha = 0f;
        vignetteGroup.interactable = false;
        vignetteGroup.blocksRaycasts = false;
        return true;
    }

    /// <summary>비네트를 서서히 드러낸다.</summary>
    private IEnumerator FadeInVignette()
    {
        float target = Mathf.Clamp01(vignetteTargetAlpha);
        float duration = Mathf.Max(0f, vignetteFadeDuration);

        if (duration <= 0f)
        {
            vignetteGroup.alpha = target;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            vignetteGroup.alpha = Mathf.Lerp(0f, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        vignetteGroup.alpha = target;
    }
}
