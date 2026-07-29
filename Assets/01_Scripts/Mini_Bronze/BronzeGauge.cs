using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 청동 제련 미니게임의 세로 게이지 표시 전담 컴포넌트.
/// 판정에는 전혀 관여하지 않는다 — 정답 구간(은백색 띠)과 눈금 위치는
/// <see cref="BronzeSmeltMiniGame"/> 이 정규화 값(0~1)으로 넘겨주고, 여기서는 그리기만 한다.
/// 보이는 띠와 실제 판정 구간이 어긋나지 않도록 띠의 배치도 코드로 계산한다.
/// </summary>
public class BronzeGauge : MonoBehaviour
{
    [Header("게이지 구성")]
    [Tooltip("게이지 전체 영역(세로 막대). band / marker / fill 은 모두 이 안의 자식이어야 한다")]
    [SerializeField] private RectTransform track;

    [Tooltip("상단의 은백색(주석) 영역. 정답 구간에 맞춰 자동으로 배치된다")]
    [SerializeField] private RectTransform band;

    [Tooltip("위로 올라가는 눈금. 세로 위치만 갱신되고 높이는 배치해 둔 값을 그대로 쓴다")]
    [SerializeField] private RectTransform marker;

    [Tooltip("눈금 아래를 채우는 구리색 게이지. Image Type 을 Filled / Vertical / Bottom 으로 두면 된다. 없어도 동작한다")]
    [SerializeField] private Image fill;

    // 눈금의 원래 크기. 앵커를 옮겨도 높이가 변하지 않게 유지하는 데 쓴다.
    private Vector2 markerSize;
    private bool cached;

    /// <summary>게이지의 기준 크기를 기억해 둔다.</summary>
    private void Awake()
    {
        CacheMarkerSize();
    }

    /// <summary>
    /// 눈금의 원래 크기를 기억해 둔다.
    /// 비활성 상태로 프리로드되어 Awake 가 늦게 도는 경우가 있어서 값을 쓰기 전에 매번 부른다.
    /// </summary>
    private void CacheMarkerSize()
    {
        if (cached || marker == null)
            return;

        cached = true;
        markerSize = marker.sizeDelta;
    }

    /// <summary>은백색(주석) 영역을 정규화 구간에 맞춰 배치한다. 0 이 게이지 바닥, 1 이 꼭대기.</summary>
    public void SetBand(float min, float max)
    {
        if (band == null)
            return;

        min = Mathf.Clamp01(min);
        max = Mathf.Clamp01(Mathf.Max(min, max));

        band.anchorMin = new Vector2(0f, min);
        band.anchorMax = new Vector2(1f, max);
        band.offsetMin = Vector2.zero;
        band.offsetMax = Vector2.zero;
    }

    /// <summary>눈금을 정규화 위치로 옮긴다. 0 이 게이지 바닥, 1 이 꼭대기.</summary>
    public void SetMarker(float value)
    {
        value = Mathf.Clamp01(value);

        if (marker != null)
        {
            CacheMarkerSize();

            // 앵커를 한 점으로 모아 세로 위치만 잡고, 높이는 배치해 둔 값을 유지한다.
            marker.anchorMin = new Vector2(0f, value);
            marker.anchorMax = new Vector2(1f, value);
            marker.anchoredPosition = new Vector2(marker.anchoredPosition.x, 0f);
            marker.sizeDelta = new Vector2(marker.sizeDelta.x, markerSize.y);
        }

        if (fill != null)
            ApplyFill(value);
    }

    /// <summary>
    /// 눈금 아래를 채운다.
    /// Image 에 스프라이트가 없거나 타입이 Filled 가 아니면 Unity 가 fillAmount 를 통째로 무시하므로,
    /// 그 경우엔 앵커를 늘려 바닥부터 채운다 — 어떤 설정이든 게이지는 차오른다.
    /// </summary>
    private void ApplyFill(float value)
    {
        if (fill.sprite != null && fill.type == Image.Type.Filled)
        {
            fill.fillAmount = value;
            return;
        }

        RectTransform rect = fill.rectTransform;

        rect.anchorMin = new Vector2(rect.anchorMin.x, 0f);
        rect.anchorMax = new Vector2(rect.anchorMax.x, value);
        rect.offsetMin = new Vector2(rect.offsetMin.x, 0f);
        rect.offsetMax = new Vector2(rect.offsetMax.x, 0f);
    }

    /// <summary>게이지 영역. 인스펙터에서 비워두면 자기 자신을 쓴다.</summary>
    public RectTransform Track => track != null ? track : transform as RectTransform;
}
