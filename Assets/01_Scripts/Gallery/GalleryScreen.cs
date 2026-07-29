using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 갤러리 씬 총괄. 저장된 엔딩들을 격자에 깔고, 고르면 상세 패널을 연다.
///
/// 이미지는 씬에 들어올 때 한 번에 읽어 <see cref="textures"/> 에 모아두고,
/// 씬을 떠날 때 한꺼번에 해제한다. 목록과 상세가 같은 텍스처를 공유하므로 메모리가 두 배로 들지 않는다.
/// </summary>
public sealed class GalleryScreen : MonoBehaviour
{
    [Header("목록")]
    [Tooltip("GridLayoutGroup 이 붙은 스크롤 Content. 여기에 칸이 생성된다")]
    [SerializeField] private RectTransform content;

    [SerializeField] private GalleryItemView itemPrefab;

    [Tooltip("저장된 엔딩이 하나도 없을 때 대신 보여줄 오브젝트")]
    [SerializeField] private GameObject emptyMessage;

    [Tooltip("체크하면 최신 엔딩이 앞에 온다. 기본은 오래된 것부터")]
    [SerializeField] private bool newestFirst;

    [Header("상세")]
    [SerializeField] private GalleryDetailView detail;

    [Header("씬")]
    [Tooltip("Back 으로 돌아갈 씬 이름")]
    [SerializeField] private string titleSceneName = "00000_Title";

    /// <summary>이 씬에서 읽어 들인 텍스처들. 씬을 떠날 때 전부 해제한다.</summary>
    private readonly List<Texture2D> textures = new List<Texture2D>();


    // ── 수명주기 ──

    /// <summary>상세를 닫아둔 상태로 목록을 채운다.</summary>
    private void Start()
    {
        if (detail != null)
            detail.Close();

        Build();
    }

    /// <summary>ESC 를 한 겹씩 벗긴다. 상세가 열려 있으면 상세만 닫고, 아니면 타이틀로 돌아간다.</summary>
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            return;

        // 설정 창이 ESC 를 먼저 먹는다 — 타이틀에서 따라온 설정 창이 여기서도 살아 있다.
        if (SettingsMenu.IsAnyOpen || SettingsMenu.EscapeConsumedThisFrame)
            return;

        // ESC 는 한 겹씩 벗긴다 — 상세가 열려 있으면 상세만 닫고, 아니면 타이틀로.
        if (detail != null && detail.IsOpen)
            detail.Close();
        else
            BackToTitle();
    }

    /// <summary>씬을 떠나며 읽어 들인 텍스처를 한꺼번에 해제한다.</summary>
    private void OnDestroy()
    {
        foreach (Texture2D texture in textures)
        {
            if (texture != null)
                Destroy(texture);
        }

        textures.Clear();
    }


    // ── 목록과 상세 ──

    /// <summary>Back 버튼이 부른다. 타이틀 씬으로 돌아간다.</summary>
    public void BackToTitle()
    {
        SceneManager.LoadScene(titleSceneName);
    }

    /// <summary>저장본을 읽어 격자를 채운다.</summary>
    private void Build()
    {
        IReadOnlyList<GalleryEntry> entries = GalleryStore.LoadAll();

        if (emptyMessage != null)
            emptyMessage.SetActive(entries.Count == 0);

        if (content == null || itemPrefab == null)
        {
            Debug.LogWarning("GalleryScreen: content 나 itemPrefab 이 비어 있어 목록을 만들 수 없습니다.");
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            // newestFirst 면 뒤에서부터 꺼낸다. 저장 순서가 곧 시간 순서다.
            GalleryEntry entry = entries[newestFirst ? entries.Count - 1 - i : i];

            Texture2D texture = GalleryStore.LoadTexture(entry);
            if (texture != null)
                textures.Add(texture);

            GalleryItemView item = Instantiate(itemPrefab, content);
            item.gameObject.SetActive(true);
            item.Bind(entry, texture, Select);
        }
    }

    /// <summary>칸을 눌렀을 때. 목록이 쓰던 텍스처를 그대로 상세에 넘긴다.</summary>
    private void Select(GalleryEntry entry, Texture2D texture)
    {
        if (detail != null)
            detail.Show(entry, texture);
    }
}
