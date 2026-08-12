using System;
using UnityEngine;

/// <summary>
/// 핵심(Critical) 미니게임 실패 직후 띄우는 갈림길 선택창.
///
/// 플레이어에게 두 갈래를 묻는다:
///   - <b>세계 만들기</b> — 이 실패까지 포함한 역사를 엔딩 크레딧으로 만들고 갤러리에 남긴다
///   - <b>다시 시작</b>   — 엔딩을 만들지 않고 곧바로 처음부터 다시 굴린다
///
/// 실패한 판마다 매번 엔딩 생성(LLM 호출 + 이미지 생성)을 기다리게 하면 반복 플레이가 무거워진다.
/// "지금 판을 작품으로 남길지, 그냥 다시 할지"를 플레이어가 고르게 해서 그 무게를 선택으로 바꾼다.
///
/// 창 자체는 <see cref="ChoicePrompt"/> 가 그린다. 여기는 문구를 만드는 일만 맡는다.
/// </summary>
public sealed class GameOverPrompt : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("실제로 창을 그리는 컴포넌트. 비워두면 이 오브젝트에 붙여 쓴다")]
    [SerializeField] private ChoicePrompt prompt;

    [Header("문구")]
    [Tooltip("{0} = 시대 이름, {1} = 사건 이름")]
    [SerializeField] private string headlineFormat = "인류는 {0}의 '{1}'에서 멈췄다.";

    [Tooltip("사건 이름이 비어 있을 때 쓰는 문구. {0} = 시대 이름")]
    [SerializeField] private string headlineFormatWithoutEvent = "인류는 {0}에서 멈췄다.";

    [SerializeField] private string questionLabel = "이 세계를 기록하시겠습니까?";
    [SerializeField] private string createWorldLabel = "기록하기";
    [SerializeField] private string restartLabel = "새로운 세계만들기";

    /// <summary>창이 떠 있는지.</summary>
    public bool IsOpen => prompt != null && prompt.IsOpen;


    /// <summary>
    /// 선택창을 띄운다. <paramref name="onChosen"/> 은 정확히 한 번만 불린다(true = 세계 만들기).
    /// </summary>
    /// <param name="era">멈춘 시대의 한국어 이름.</param>
    /// <param name="eventLabel">멈춤을 유발한 사건 이름. 비어 있어도 된다.</param>
    public void Show(string era, string eventLabel, Action<bool> onChosen)
    {
        if (prompt == null)
            prompt = gameObject.AddComponent<ChoicePrompt>();

        string headline = string.IsNullOrWhiteSpace(eventLabel)
            ? string.Format(headlineFormatWithoutEvent, era)
            : string.Format(headlineFormat, era, eventLabel);

        prompt.Show(headline, questionLabel, createWorldLabel, restartLabel, onChosen);
    }
}
