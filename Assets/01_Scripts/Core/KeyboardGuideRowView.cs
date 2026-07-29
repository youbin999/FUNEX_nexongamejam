using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>여러 키캡과 행동 설명으로 구성된 키 가이드 한 줄.</summary>
public sealed class KeyboardGuideRowView : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("생성한 키캡을 배치할 부모")]
    [SerializeField] private Transform keyContainer;

    [Tooltip("키 하나를 표시하는 키캡 프리팹")]
    [SerializeField] private KeyboardKeycapView keycapPrefab;

    [Tooltip("키 옆에 표시할 행동 설명 텍스트. 설명이 비어 있으면 자동으로 숨긴다")]
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
