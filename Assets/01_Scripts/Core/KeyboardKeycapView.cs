using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>키 하나를 키캡 모양으로 표시하는 UI.</summary>
public sealed class KeyboardKeycapView : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private LayoutElement layoutElement;
    [SerializeField] private float regularWidth = 52f;
    [SerializeField] private float wideWidth = 108f;

    /// <summary>표시할 키를 적용한다.</summary>
    public void Bind(Key key)
    {
        string displayName = GetDisplayName(key);

        if (label != null)
            label.text = displayName;

        if (layoutElement != null)
            layoutElement.preferredWidth = displayName.Length > 2 ? wideWidth : regularWidth;
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
