using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>키 하나를 키캡 모양으로 표시하는 UI.</summary>
public sealed class KeyboardKeycapView : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private LayoutElement layoutElement;
    [Tooltip("W, ↑ 처럼 1~2글자 키의 폭")]
    [SerializeField] private float regularWidth = 52f;
    [Tooltip("SHIFT, ENTER 처럼 3글자 이상 키의 폭")]
    [SerializeField] private float wideWidth = 108f;
    [Tooltip("스페이스바 전용 폭")]
    [SerializeField] private float spaceWidth = 168f;

    /// <summary>표시할 키를 적용한다.</summary>
    public void Bind(Key key)
    {
        string displayName = GetDisplayName(key);

        if (label != null)
            label.text = displayName;

        ApplyWidth(GetWidth(key, displayName));
    }

    /// <summary>라벨 길이에 따라 키캡 폭을 3단계로 고른다.</summary>
    private float GetWidth(Key key, string displayName)
    {
        if (key == Key.Space)
            return spaceWidth;

        return displayName.Length > 2 ? wideWidth : regularWidth;
    }

    /// <summary>
    /// 키캡 폭을 적용한다. 부모 레이아웃 그룹이 Child Control Width 를 끈 상태여도
    /// 폭이 반영되도록 RectTransform 크기까지 직접 세팅한다.
    /// </summary>
    private void ApplyWidth(float width)
    {
        if (layoutElement != null)
        {
            layoutElement.preferredWidth = width;
            layoutElement.minWidth = width;
        }

        RectTransform rect = transform as RectTransform;
        if (rect != null)
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }

    /// <summary>Input System 키 값을 플레이어가 읽기 쉬운 짧은 이름으로 변환한다.</summary>
    public static string GetDisplayName(Key key)
    {
        switch (key)
        {
            case Key.Space:
                return "SPACE";
            case Key.UpArrow:
                return "↑";
            case Key.DownArrow:
                return "↓";
            case Key.LeftArrow:
                return "←";
            case Key.RightArrow:
                return "→";
            case Key.Enter:
            case Key.NumpadEnter:
                return "ENTER";
            case Key.LeftShift:
            case Key.RightShift:
                return "SHIFT";
            case Key.LeftCtrl:
            case Key.RightCtrl:
                return "CTRL";
            case Key.LeftAlt:
            case Key.RightAlt:
                return "ALT";
            case Key.Escape:
                return "ESC";
            default:
                return key.ToString().ToUpperInvariant();
        }
    }
}
