using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 마녀 찾기 미니게임의 4x4 그리드 한 칸.
/// 얼굴 스프라이트 표시, 커서 하이라이트, 표시(마킹) 결과 연출을 담당한다.
/// </summary>
public class WitchGridCell : MonoBehaviour
{
    [SerializeField] private Image faceImage;
    [SerializeField] private GameObject cursorHighlight;
    [SerializeField] private GameObject markedOverlay;
    [SerializeField] private Image markedOverlayImage;
    [SerializeField] private TMP_Text markedLabel;

    [SerializeField] private Color correctColor = new Color(0.4f, 0.85f, 0.4f, 0.85f);
    [SerializeField] private Color wrongColor = new Color(0.85f, 0.3f, 0.3f, 0.85f);

    /// <summary>현재 표시 중인 얼굴 스프라이트. 실패 패널티가 띄울 얼굴을 가져갈 때 쓴다.</summary>
    public Sprite CurrentFace => faceImage != null ? faceImage.sprite : null;

    /// <summary>얼굴 스프라이트를 바꾼다. null 이면 바꾸지 않고 유지한다(플레이스홀더 대응).</summary>
    public void SetFace(Sprite sprite)
    {
        if (faceImage != null && sprite != null)
            faceImage.sprite = sprite;
    }

    /// <summary>현재 WASD 커서가 이 칸에 있는지 여부.</summary>
    public void SetSelected(bool selected)
    {
        if (cursorHighlight != null)
            cursorHighlight.SetActive(selected);
    }

    /// <summary>스페이스바로 표시됐을 때의 결과 연출. isWitch 여부로 색만 바꾼다.</summary>
    public void SetMarked(bool isWitch)
    {
        if (markedOverlayImage != null)
            markedOverlayImage.color = isWitch ? correctColor : wrongColor;

        if (markedLabel != null)
            markedLabel.text = isWitch ? "마녀!" : "X";

        if (markedOverlay != null)
            markedOverlay.SetActive(true);
    }

    /// <summary>다음 재생을 위해 초기 상태로 되돌린다.</summary>
    public void ResetVisual()
    {
        if (cursorHighlight != null)
            cursorHighlight.SetActive(false);

        if (markedOverlay != null)
            markedOverlay.SetActive(false);
    }
}
