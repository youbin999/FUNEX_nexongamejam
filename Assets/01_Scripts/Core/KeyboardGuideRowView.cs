using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>여러 키캡과 행동 설명으로 구성된 키 가이드 한 줄.</summary>
public sealed class KeyboardGuideRowView : MonoBehaviour
{
    [SerializeField] private Transform keyContainer;
    [SerializeField] private KeyboardKeycapView keycapPrefab;
    [SerializeField] private TMP_Text actionText;

    /// <summary>가이드 정보를 적용하고 유효한 키캡을 생성한다.</summary>
    public void Bind(KeyboardGuideEntry entry)
    {
        if (entry == null)
            return;

        if (actionText != null)
        {
            bool hasActionLabel = !string.IsNullOrWhiteSpace(entry.ActionLabel);
            actionText.gameObject.SetActive(hasActionLabel);
            if (hasActionLabel)
                actionText.text = entry.ActionLabel;
        }

        if (keyContainer == null || keycapPrefab == null)
            return;

        foreach (Key key in entry.Keys)
        {
            if (key == Key.None)
                continue;

            KeyboardKeycapView keycap = Instantiate(keycapPrefab, keyContainer);
            keycap.gameObject.SetActive(true);
            keycap.Bind(key);
        }
    }
}
