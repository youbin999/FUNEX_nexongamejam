using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 미니게임의 공통 수명주기를 정의하는 추상 베이스 클래스.
/// 프리팹 단위로 만들어 <see cref="MiniGamePlayer"/> 가 프리로드해 두었다가
/// 명령이 오면 <see cref="Play"/> 로 재생하고, 끝나면 <see cref="Finished"/> 로 통지받는다.
/// </summary>
public abstract class MiniGame : MonoBehaviour
{
    /// <summary>
    /// 게임 종료 통지. (자기 자신, 성공 여부).
    /// 플레이어 코드 전용이라 인스펙터에 노출하지 않고 C# event 로 선언한다.
    /// </summary>
    public event Action<MiniGame, bool> Finished;

    [Header("설명 프롬프트")]
    [Tooltip("MiniGamePlayer 가 Play() 호출 전에 표시하는 게임 설명 문구")]
    [SerializeField] private string gameDescription;

    [Header("키보드 가이드")]
    [Tooltip("게임 시작 전에 표시할 키보드 조작 안내. 마우스 전용 게임은 비워 둔다")]
    [SerializeField] private List<KeyboardGuideEntry> keyboardGuides = new List<KeyboardGuideEntry>();

    /// <summary>게임 시작 전 표시할 설명 문구. <see cref="MiniGamePlayer"/> 가 Play() 호출 전 연출에 사용한다.</summary>
    public string GameDescription => gameDescription;

    /// <summary>게임 시작 전에 표시할 키보드 조작 안내.</summary>
    public IReadOnlyList<KeyboardGuideEntry> KeyboardGuides => keyboardGuides;

    /// <summary>현재 재생 중인지 여부.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>
    /// 게임을 시작한다. 이미 플레이 중이면 무시한다.
    /// 내부적으로 상태를 초기화한 뒤 시작한다.
    /// </summary>
    public void Play()
    {
        if (IsPlaying)
            return;

        IsPlaying = true;
        OnPlay();
    }

    /// <summary>
    /// 게임을 강제로 중단하고 초기 상태로 되돌린다.
    /// 플레이 중이 아니어도 호출 가능하다(멱등).
    /// </summary>
    public void StopAndReset()
    {
        IsPlaying = false;
        OnStopAndReset();
    }

    /// <summary>파생 클래스에서 실제 게임 시작 처리를 구현한다.</summary>
    protected abstract void OnPlay();

    /// <summary>파생 클래스에서 상태를 초기 값으로 복원하는 처리를 구현한다.</summary>
    protected abstract void OnStopAndReset();

    /// <summary>
    /// 파생 클래스가 게임 종료 시 호출한다.
    /// 플레이 중이 아니면 무시하여 중복 통지를 막고,
    /// <see cref="IsPlaying"/> 를 false 로 만든 뒤 <see cref="Finished"/> 를 1회만 발화한다.
    /// </summary>
    /// <param name="success">클리어(성공) 여부.</param>
    protected void ReportFinished(bool success)
    {
        if (!IsPlaying)
            return;

        IsPlaying = false;
        Finished?.Invoke(this, success);
    }
}
