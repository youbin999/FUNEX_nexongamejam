using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 미니게임 시작 전에 표시할 키보드 조작 안내 한 줄.
/// 실제 입력을 자동 분석하지 않고, 미니게임 프리팹에서 명시적으로 설정한다.
/// </summary>
[Serializable]
public sealed class KeyboardGuideEntry
{
    [Tooltip("키 옆에 표시할 선택적 행동 설명. 비워두면 키캡만 표시한다")]
    [SerializeField] private string actionLabel;

    [Tooltip("안내할 키 목록. Key.None 은 표시하지 않는다")]
    [SerializeField] private List<Key> keys = new List<Key>();

    /// <summary>키를 눌렀을 때 수행하는 행동 설명.</summary>
    public string ActionLabel => actionLabel;

    /// <summary>키캡으로 표시할 키 목록.</summary>
    public IReadOnlyList<Key> Keys => keys;
}
