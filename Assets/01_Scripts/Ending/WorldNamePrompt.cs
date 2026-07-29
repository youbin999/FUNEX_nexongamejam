using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 엔딩 크레딧이 끝난 뒤 화면 가운데에 뜨는 이름 입력 탭.
/// 방금 만들어진 엔딩 이미지를 축소해 보여주고, 플레이어가 붙인 world 이름을 받아 넘긴다.
///
/// 확정 경로는 두 갈래(엔터 / 확정 버튼)지만 <see cref="confirmed"/> 로 한 번만 통과시킨다.
///
/// <b>이 컴포넌트는 항상 활성인 바깥 오브젝트에 붙이고, 실제로 켜고 끌 탭을
/// <see cref="panelRoot"/> 에 연결한다.</b> 자기 자신을 껐다 켜면 리스너를 묶는 Awake 가
/// 도로 패널을 닫아버린다.
/// </summary>
public sealed class WorldNamePrompt : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("실제로 켜고 끌 패널 루트. 비워두면 이 오브젝트를 쓴다")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("방금 만들어진 엔딩 이미지를 축소해 보여줄 RawImage")]
    [SerializeField] private RawImage preview;

    [Tooltip("미리보기 비율을 원본에 맞춰준다. 없으면 영역에 그대로 늘어난다")]
    [SerializeField] private AspectRatioFitter previewFitter;

    [Tooltip("이름을 받는 입력 칸")]
    [SerializeField] private TMP_InputField nameField;

    [Tooltip("확정 버튼. 이름이 비어 있는 동안은 눌리지 않는다")]
    [SerializeField] private Button confirmButton;

    /// <summary>확정된 이름을 받는 쪽. <see cref="Show"/> 로 넘어온다.</summary>
    private Action<string> onConfirmed;

    /// <summary>엔터와 버튼이 겹쳐 두 번 확정되는 것을 막는다.</summary>
    private bool confirmed;

    /// <summary>입력 칸·버튼 리스너를 한 번만 묶는다.</summary>
    private bool wired;

    /// <summary>패널이 떠 있는지.</summary>
    public bool IsOpen => Root.activeSelf;

    /// <summary>실제로 켜고 끌 오브젝트. 지정이 없으면 자기 자신.</summary>
    private GameObject Root => panelRoot != null ? panelRoot : gameObject;

    /// <summary>공백만 남은 이름은 이름이 아니다. 입력 칸 연결이 빠졌으면 막지 않는다 — 확정할 길이 없어지면 안 된다.</summary>
    private bool HasName => nameField == null || !string.IsNullOrWhiteSpace(nameField.text);


    // ── 수명주기와 배선 ──

    /// <summary>리스너를 묶고 탭을 닫아둔 상태로 시작한다.</summary>
    private void Awake()
    {
        EnsureWired();

        // 탭이 따로 있을 때만 닫아둔다. 자기 자신을 끄면 다시 켜질 때 Awake 가 또 돌아 닫아버린다.
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    /// <summary>입력 칸과 확정 버튼의 리스너를 한 번만 묶는다.</summary>
    private void EnsureWired()
    {
        if (wired)
            return;

        wired = true;

        if (nameField != null)
        {
            nameField.characterLimit = GalleryStore.MaxWorldNameLength;
            nameField.lineType = TMP_InputField.LineType.SingleLine;

            // ESC 를 눌러도 쳐둔 이름은 남긴다. TMP 는 기본값이 "원래 값으로 되돌리기"라
            // 그냥 두면 ESC 한 번에 이름이 통째로 날아간다. (포커스는 TMP 가 놓는다 —
            // 그동안 SettingsMenu 는 ESC 를 무시하므로 설정 창이 대신 열리지도 않는다)
            nameField.restoreOriginalTextOnEscape = false;

            nameField.onSubmit.AddListener(_ => Confirm());
            nameField.onValueChanged.AddListener(_ => RefreshConfirmButton());
        }

        if (confirmButton != null)
            confirmButton.onClick.AddListener(Confirm);
    }


    // ── 열기와 닫기 ──

    /// <summary>
    /// 탭을 띄운다. 이미지의 수명은 부른 쪽이 관리하므로 여기서는 참조만 건다.
    /// </summary>
    /// <param name="image">크레딧 배경에 떴던 엔딩 이미지. null 이면 미리보기 자리를 비운다.</param>
    /// <param name="onConfirmed">확정된 이름을 받을 콜백. 정확히 한 번만 불린다.</param>
    public void Show(Texture2D image, Action<string> onConfirmed)
    {
        EnsureWired();

        this.onConfirmed = onConfirmed;
        confirmed = false;

        if (preview != null)
        {
            preview.texture = image;
            preview.enabled = image != null;
        }

        if (previewFitter != null && image != null && image.height > 0)
            previewFitter.aspectRatio = (float)image.width / image.height;

        if (nameField != null)
            nameField.text = string.Empty;

        RefreshConfirmButton();

        Root.SetActive(true);

        if (nameField != null)
            StartCoroutine(FocusRoutine());
    }

    /// <summary>
    /// 입력 칸에 포커스를 준다. 켜진 그 프레임에 바로 부르면 TMP 가 포커스를 놓치는 일이 있어
    /// 한 프레임 넘긴 뒤에 활성화한다.
    /// </summary>
    private IEnumerator FocusRoutine()
    {
        yield return null;

        if (nameField == null || !IsOpen)
            yield break;

        nameField.Select();
        nameField.ActivateInputField();
    }


    // ── 확정 ──

    /// <summary>
    /// 이름을 확정한다. 확정 버튼의 OnClick 과 입력 칸의 엔터가 모두 여기로 들어온다.
    /// 이름이 비어 있으면 아무 일도 하지 않는다 — 엔터로 빈 이름이 통과하는 길을 막는다.
    /// </summary>
    public void Confirm()
    {
        if (confirmed || !HasName)
            return;

        confirmed = true;

        string worldName = nameField != null ? nameField.text.Trim() : string.Empty;

        if (nameField != null)
            nameField.DeactivateInputField();

        Close();

        Action<string> callback = onConfirmed;
        onConfirmed = null;
        callback?.Invoke(worldName);
    }

    /// <summary>패널을 닫는다. 미리보기 참조도 놓아 텍스처가 붙들리지 않게 한다.</summary>
    public void Close()
    {
        if (preview != null)
            preview.texture = null;

        Root.SetActive(false);
    }

    /// <summary>이름이 비어 있는 동안에는 확정 버튼을 잠가 둔다.</summary>
    private void RefreshConfirmButton()
    {
        if (confirmButton != null)
            confirmButton.interactable = HasName;
    }
}
