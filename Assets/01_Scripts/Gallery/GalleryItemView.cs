using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 갤러리 목록의 칸 하나. 엔딩 이미지를 썸네일로 보여주고, 누르면 상세로 넘긴다.
/// 텍스처의 수명은 <see cref="GalleryScreen"/> 이 관리하므로 여기서는 참조만 들고 있는다.
/// </summary>
public sealed class GalleryItemView : MonoBehaviour
{
    [SerializeField] private RawImage thumbnail;

    [Tooltip("플레이어가 붙인 world 이름. 비워두면 이름은 표시되지 않는다")]
    [SerializeField] private TMP_Text nameLabel;

    [SerializeField] private TMP_Text dateLabel;
    [SerializeField] private Button button;

    private GalleryEntry entry;
    private Texture2D texture;
    private Action<GalleryEntry, Texture2D> onSelected;

    /// <summary>
    /// 엔트리를 칸에 붙인다. 텍스처가 null 이면 썸네일 자리는 비워둔다.
    /// 상세 화면도 같은 텍스처를 쓰므로 선택될 때 그대로 돌려준다.
    /// </summary>
    public void Bind(GalleryEntry entry, Texture2D texture, Action<GalleryEntry, Texture2D> onSelected)
    {
        this.entry = entry;
        this.texture = texture;
        this.onSelected = onSelected;

        if (thumbnail != null)
        {
            thumbnail.texture = texture;
            thumbnail.enabled = texture != null;
        }

        if (nameLabel != null)
            nameLabel.text = entry != null ? entry.DisplayName : string.Empty;

        if (dateLabel != null)
            dateLabel.text = entry != null ? entry.DisplayDate : string.Empty;

        if (button == null)
            return;

        // 풀링 없이 매번 새로 만들지만, 재사용해도 리스너가 겹치지 않도록 비워두고 등록한다.
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(Select);
    }

    private void Select()
    {
        if (entry != null)
            onSelected?.Invoke(entry, texture);
    }
}
