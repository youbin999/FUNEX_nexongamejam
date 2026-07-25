using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 4x4 그리드 한 칸. 스프라이트 표시, 커서 하이라이트, 표시(마킹) 결과 연출을 담당한다.
/// 마녀 찾기(<see cref="WitchFindMiniGame"/>)와 페트리 접시 고르기(<see cref="PenicillinFindMiniGame"/>)가
/// 같은 컴포넌트를 공유한다 — 조작이 같은 게임을 다른 시대에 배치하는 것이 이 게임의 주제 장치다.
/// 시대별 차이는 스프라이트와 <see cref="correctLabel"/> 로만 준다.
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

    [Header("표시 문구")]
    [Tooltip("정답 칸을 표시했을 때 뜨는 글자. 같은 그리드를 다른 시대에 재사용할 때 여기만 바꾸면 된다")]
    [SerializeField] private string correctLabel = "마녀!";

    [Tooltip("오답 칸을 표시했을 때 뜨는 글자")]
    [SerializeField] private string wrongLabel = "X";

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

    /// <summary>스페이스바로 표시됐을 때의 결과 연출. 정답 여부로 색과 문구만 바꾼다.</summary>
    public void SetMarked(bool isCorrect)
    {
        if (markedOverlayImage != null)
            markedOverlayImage.color = isCorrect ? correctColor : wrongColor;

        if (markedLabel != null)
            markedLabel.text = isCorrect ? correctLabel : wrongLabel;

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
