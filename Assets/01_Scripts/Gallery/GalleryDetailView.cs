using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 갤러리 상세 패널. 왼쪽에 확대된 엔딩 이미지, 오른쪽에 그때의 크레딧 본문을 보여준다.
///
/// 이 컴포넌트는 패널 루트에 붙는다. 패널은 평소 비활성 상태이지만,
/// <see cref="GalleryScreen"/> 이 참조를 들고 있으므로 비활성 상태에서도 <see cref="Show"/> 를 부를 수 있다.
/// </summary>
public sealed class GalleryDetailView : MonoBehaviour
{
    [Header("이미지")]
    [SerializeField] private RawImage image;

    [Tooltip("이미지 비율을 원본에 맞춰준다. 없으면 RawImage 영역에 그대로 늘어난다")]
    [SerializeField] private AspectRatioFitter imageFitter;

    [Header("텍스트")]
    [Tooltip("그때 흘러갔던 크레딧 본문")]
    [SerializeField] private TMP_Text creditText;

    [Tooltip("롤이 끝난 뒤 남았던 마무리 문장. 비어 있으면 자동으로 숨긴다")]
    [SerializeField] private TMP_Text epilogueText;

    [Tooltip("world 이름 전용 라벨. 비워두면 이름이 날짜 줄 앞에 붙는다")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("저장 시각 라벨")]
    [SerializeField] private TMP_Text dateText;

    [Tooltip("크레딧이 길 때 위에서부터 보이도록 되감을 스크롤. 없어도 동작한다")]
    [SerializeField] private ScrollRect creditScroll;

    /// <summary>패널이 열려 있는지. ESC 처리에서 쓴다.</summary>
    public bool IsOpen => gameObject.activeSelf;

    /// <summary>엔트리를 펼쳐 보여준다. 텍스처의 수명은 부른 쪽이 관리한다.</summary>
    public void Show(GalleryEntry entry, Texture2D texture)
    {
        if (entry == null)
            return;

        if (image != null)
        {
            image.texture = texture;
            image.enabled = texture != null;
        }

        if (imageFitter != null && texture != null && texture.height > 0)
            imageFitter.aspectRatio = (float)texture.width / texture.height;

        if (creditText != null)
            creditText.text = entry.CreditBody;

        if (epilogueText != null)
        {
            bool hasEpilogue = !string.IsNullOrWhiteSpace(entry.epilogue);
            epilogueText.gameObject.SetActive(hasEpilogue);
            if (hasEpilogue)
                epilogueText.text = entry.epilogue;
        }

        if (nameText != null)
            nameText.text = entry.DisplayName;

        // 이름 라벨이 따로 없는(옛 구성) 패널에서는 날짜 줄이 이름까지 떠안는다.
        if (dateText != null)
            dateText.text = nameText != null
                ? entry.DisplayDate
                : $"{entry.DisplayName}\n{entry.DisplayDate}";

        gameObject.SetActive(true);

        // 이전에 열었던 엔트리의 스크롤 위치가 남아 있으면 본문 중간부터 보인다.
        if (creditScroll != null)
            creditScroll.verticalNormalizedPosition = 1f;
    }

    /// <summary>패널을 닫는다. 닫기 버튼과 ESC 가 부른다.</summary>
    public void Close()
    {
        // 텍스처 참조를 놓아 목록에서 해제될 때 남지 않게 한다.
        if (image != null)
            image.texture = null;

        gameObject.SetActive(false);
    }
}
