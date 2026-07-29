using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 고대(청동기) 청동 제련 미니게임.
/// 오른쪽 세로 게이지의 눈금이 아래에서 위로 빠르게 차오르고, 상단의 좁은 은백색(주석) 영역에
/// 눈금이 들어왔을 때 스페이스바를 누르면 성공. 못 미친 채 누르거나 영역을 지나쳐 버리면 실패.
///
/// 성공하면 용광로가 타오르는 그림으로 바뀌고 청동검이 튀어나오는 연출이 재생되며,
/// 제한 시간이 다 차는 순간 성공으로 통지된다(와리오웨어 규칙 — <see cref="TimedMiniGame"/>).
/// 실패는 검은 연기 연출을 잠깐 보여준 뒤 통지한다.
/// </summary>
public class BronzeSmeltMiniGame : TimedMiniGame
{
    private enum State
    {
        Idle,
        Rising,
        Cleared,
        Failed,
    }

    [Header("게이지")]
    [SerializeField] private BronzeGauge gauge;

    [Tooltip("정답 구간(은백색 영역)의 아래 끝. 게이지 바닥이 0, 꼭대기가 1")]
    [Range(0f, 1f)]
    [SerializeField] private float bandMin = 0.88f;

    [Tooltip("정답 구간(은백색 영역)의 위 끝. 좁을수록 어렵다")]
    [Range(0f, 1f)]
    [SerializeField] private float bandMax = 0.96f;

    [Tooltip("눈금이 바닥에서 꼭대기까지 차오르는 데 걸리는 시간(초). 짧을수록 빠르고 어렵다")]
    [SerializeField] private float riseDuration = 1.2f;

    [Tooltip("켜면 눈금이 꼭대기에서 되돌아 내려오며 왕복해 기회가 여러 번 생긴다.\n" +
        "끄면 한 번만 올라가고, 영역을 지나치는 순간 실패한다(기본)")]
    [SerializeField] private bool pingPong = false;

    [Header("용광로")]
    [SerializeField] private SpriteRenderer furnaceRenderer;

    [Tooltip("식어 있는 용광로")]
    [SerializeField] private Sprite furnaceOffSprite;

    [Tooltip("활활 타오르는 용광로. 성공하는 순간 교체된다")]
    [SerializeField] private Sprite furnaceOnSprite;

    [Header("청동검")]
    [Tooltip("성공 시 용광로에서 튀어나오는 검. 시작 위치를 용광로 안쪽에 두면 된다")]
    [SerializeField] private Transform sword;

    [Tooltip("검이 튀어오르는 높이(월드 유닛)")]
    [SerializeField] private float swordRiseHeight = 2.2f;

    [Tooltip("검이 튀어오르는 데 걸리는 시간(초)")]
    [SerializeField] private float swordRiseDuration = 0.45f;

    [Tooltip("튀어오르는 동안 회전하는 각도(도)")]
    [SerializeField] private float swordSpinDegrees = 360f;

    [Header("도구 / 연기")]
    [Tooltip("용광로 주변을 날아다니는 도구들. 클리어되면 멈춘다")]
    [SerializeField] private BronzeToolSwarm toolSwarm;

    [Tooltip("실패 시 피어오르는 검은 연기. 스패너 미니게임의 증기 연출을 색만 검게 해서 재활용한다")]
    [SerializeField] private SteamBurst smokeBurst;

    [Header("결과 문구")]
    [SerializeField] private CanvasGroup resultCanvasGroup;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private string clearMessage = "SUCCESS! 청동기 시대 돌입!";
    [SerializeField] private string failMessage = "푸슉... 실패!";

    [Tooltip("문구가 스케일+알파로 팝인하는 데 걸리는 시간")]
    [SerializeField] private float resultAppearDuration = 0.3f;

    [Tooltip("팝인 시작 시점의 스케일 배율")]
    [SerializeField] private float resultStartScale = 0.3f;

    [Header("실패 연출")]
    [Tooltip("실패가 확정된 뒤 연기를 보여주고 종료를 통지하기까지 기다리는 시간(초). 이 동안 입력은 잠긴다")]
    [SerializeField] private float failPresentDuration = 0.35f;

    [Header("효과음")]
    [Tooltip("성공 — 챙강! 하는 맑은 금속음")]
    [SerializeField] private AudioClip clearSfx;

    [Tooltip("실패 — 푸슉- 하는 김 빠지는 소리")]
    [SerializeField] private AudioClip failSfx;

    [Header("이벤트")]
    public UnityEvent onClear;
    public UnityEvent onFail;

    private State state = State.Idle;

    // 눈금이 올라간 시간. riseDuration 으로 나눠 0~1 위치를 만든다.
    private float riseElapsed;
    private float marker;

    private Vector3 swordStartPosition;
    private Quaternion swordStartRotation;
    private Coroutine sequenceRoutine;

    /// <summary>지금 눈금 위치(0~1).</summary>
    public float Marker => marker;

    /// <summary>검이 놓인 처음 자세를 기억해 둔다.</summary>
    private void Awake()
    {
        if (sword != null)
        {
            swordStartPosition = sword.position;
            swordStartRotation = sword.rotation;
        }
    }

    /// <summary>초기 표시만 한다. 이미 재생 중이면 건드리지 않는다.</summary>
    private void Start()
    {
        // MiniGamePlayer 는 프리팹을 활성화한 그 프레임에 Play() 를 부를 수 있고,
        // 그 경우 Start 가 재생 시작보다 늦게 돈다. 이미 재생 중이면 건드리지 않는다.
        if (IsPlaying)
            return;

        // 계약 1.3 — 여기서는 초기 표시만 하고 게임을 시작하지 않는다.
        ResetInternal();
    }

    /// <summary>게임을 시작한다. 호출 시점에 타이머는 이미 0으로 초기화돼 있다.</summary>
    protected override void OnTimedPlay()
    {
        ResetInternal();

        state = State.Rising;

        if (toolSwarm != null)
            toolSwarm.Begin();
    }

    /// <summary>게임을 중단하고 초기 상태로 되돌린다. 멱등하다.</summary>
    protected override void OnTimedStopAndReset()
    {
        StopSequence();
        ResetInternal();
    }

    /// <summary>매 프레임 입력과 진행을 처리한다. 결과가 확정된 뒤에는 불리지 않는다.</summary>
    protected override void OnTimedUpdate()
    {
        if (state != State.Rising)
            return;

        AdvanceMarker();

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Judge();
            return;
        }

        // 한 번만 올라가는 모드에서는 영역을 지나쳐 버리면 그 순간 실패다.
        if (!pingPong && marker > bandMax)
            Fail();
    }

    /// <summary>제한 시간을 다 써서 실패로 확정될 때 호출된다.</summary>
    protected override void OnTimeUp()
    {
        // 아직 아무 판정도 나지 않은 채 시간이 다 됐으면(왕복 모드에서 안 누른 경우 등) 여기서 실패 연출을 낸다.
        // 이미 실패 연출이 돌고 있었다면 중복으로 내지 않는다.
        if (state == State.Rising)
            PresentFail();

        base.OnTimeUp();
    }

    /// <summary>눈금을 한 프레임만큼 진행시킨다.</summary>
    private void AdvanceMarker()
    {
        riseElapsed += Time.deltaTime;

        float ratio = riseDuration > 0f ? riseElapsed / riseDuration : 1f;
        marker = pingPong ? Mathf.PingPong(ratio, 1f) : Mathf.Clamp01(ratio);

        if (gauge != null)
            gauge.SetMarker(marker);
    }

    /// <summary>스페이스를 누른 순간의 눈금 위치로 성공/실패를 가른다.</summary>
    private void Judge()
    {
        if (marker >= bandMin && marker <= bandMax)
            Clear();
        else
            Fail();
    }

    /// <summary>클리어 처리. 입력만 잠근 채 화로를 켜고 검·결과 연출을 재생한다.</summary>
    private void Clear()
    {
        state = State.Cleared;

        // 즉시 끝내지 않는다. 입력만 잠근 채 제한 시간이 다 차면 성공으로 통지된다.
        SucceedWhenTimeUp();

        if (furnaceRenderer != null && furnaceOnSprite != null)
            furnaceRenderer.sprite = furnaceOnSprite;

        if (toolSwarm != null)
            toolSwarm.Freeze();

        PlaySfx(clearSfx);

        StopSequence();
        sequenceRoutine = StartCoroutine(ClearRoutine());
    }

    /// <summary>실패 처리. 진행 중인 연출을 끊고 실패 연출로 갈아탄다.</summary>
    private void Fail()
    {
        state = State.Failed;

        PresentFail();

        StopSequence();
        sequenceRoutine = StartCoroutine(FailRoutine());
    }

    /// <summary>실패 연출(소리 + 검은 연기 + 문구)만 낸다. 종료 통지와는 분리돼 있다.</summary>
    private void PresentFail()
    {
        PlaySfx(failSfx);

        if (smokeBurst != null)
            smokeBurst.Burst();

        ShowResult(failMessage);
        onFail.Invoke();
    }

    /// <summary>연기를 잠깐 보여준 뒤 실패로 통지한다. 그 사이 입력은 state 로 잠겨 있다.</summary>
    private IEnumerator FailRoutine()
    {
        if (failPresentDuration > 0f)
            yield return new WaitForSeconds(failPresentDuration);

        sequenceRoutine = null;

        // 이미 시간 초과로 끝났다면 아무 일도 하지 않는다(멱등).
        FailImmediately();
    }

    /// <summary>성공 연출: 청동검이 튀어오르고 문구가 팝인된다. 이후엔 그대로 유지된 채 제한 시간을 기다린다.</summary>
    private IEnumerator ClearRoutine()
    {
        yield return StartCoroutine(SwordPopRoutine());

        ShowResult(clearMessage);
        yield return StartCoroutine(ResultPopRoutine());

        sequenceRoutine = null;
        onClear.Invoke();
    }

    /// <summary>검이 화로에서 솟아올랐다가 자리를 잡는다.</summary>
    private IEnumerator SwordPopRoutine()
    {
        if (sword == null)
            yield break;

        sword.gameObject.SetActive(true);
        sword.position = swordStartPosition;
        sword.rotation = swordStartRotation;

        Vector3 topPosition = swordStartPosition + Vector3.up * swordRiseHeight;

        float time = 0f;
        while (time < swordRiseDuration)
        {
            time += Time.deltaTime;
            float k = swordRiseDuration > 0f ? Mathf.Clamp01(time / swordRiseDuration) : 1f;

            // 튀어나온 직후가 가장 빠르고 위로 갈수록 느려지도록 ease-out 을 쓴다.
            float ease = 1f - (1f - k) * (1f - k);

            sword.position = Vector3.Lerp(swordStartPosition, topPosition, ease);
            sword.rotation = swordStartRotation * Quaternion.Euler(0f, 0f, swordSpinDegrees * ease);

            yield return null;
        }

        sword.position = topPosition;
        sword.rotation = swordStartRotation * Quaternion.Euler(0f, 0f, swordSpinDegrees);
    }

    /// <summary>결과 문구가 작게 시작해 통통 튀며 제 크기까지 커진다.</summary>
    private IEnumerator ResultPopRoutine()
    {
        if (resultCanvasGroup == null)
            yield break;

        Transform t = resultCanvasGroup.transform;

        float time = 0f;
        while (time < resultAppearDuration)
        {
            time += Time.deltaTime;
            float k = resultAppearDuration > 0f ? Mathf.Clamp01(time / resultAppearDuration) : 1f;
            float scale = Mathf.LerpUnclamped(resultStartScale, 1f, Ease.OutBack(k));

            t.localScale = new Vector3(scale, scale, 1f);
            resultCanvasGroup.alpha = k;

            yield return null;
        }

        t.localScale = Vector3.one;
        resultCanvasGroup.alpha = 1f;
    }

    /// <summary>결과 문구를 띄운다. 실패 문구는 팝인 연출 없이 바로 보이게 한다.</summary>
    private void ShowResult(string message)
    {
        if (resultText != null)
            resultText.text = message;

        if (resultCanvasGroup == null)
            return;

        resultCanvasGroup.gameObject.SetActive(true);
        resultCanvasGroup.alpha = 1f;
        resultCanvasGroup.transform.localScale = Vector3.one;
    }

    /// <summary>결과 문구를 감추고 팝인 시작 상태로 되돌린다.</summary>
    private void HideResult()
    {
        if (resultCanvasGroup == null)
            return;

        resultCanvasGroup.alpha = 0f;
        resultCanvasGroup.transform.localScale = Vector3.one * resultStartScale;
        resultCanvasGroup.gameObject.SetActive(false);
    }

    /// <summary>모든 상태를 초기 표시로 되돌린다. 재생 중이 아닐 때 불러도 안전하다(계약 1.4).</summary>
    private void ResetInternal()
    {
        state = State.Idle;
        riseElapsed = 0f;
        marker = 0f;

        if (gauge != null)
        {
            gauge.SetBand(bandMin, bandMax);
            gauge.SetMarker(0f);
        }

        if (furnaceRenderer != null && furnaceOffSprite != null)
            furnaceRenderer.sprite = furnaceOffSprite;

        if (sword != null)
        {
            sword.position = swordStartPosition;
            sword.rotation = swordStartRotation;
            sword.gameObject.SetActive(false);
        }

        if (smokeBurst != null)
            smokeBurst.StopBurst();

        if (toolSwarm != null)
            toolSwarm.ResetAll();

        HideResult();
    }

    /// <summary>진행 중인 연출 코루틴을 끊는다.</summary>
    private void StopSequence()
    {
        if (sequenceRoutine == null)
            return;

        StopCoroutine(sequenceRoutine);
        sequenceRoutine = null;
    }

    /// <summary>AudioManager 를 거쳐 효과음을 한 번 낸다. 매니저가 없으면 조용히 넘어간다.</summary>
    private void PlaySfx(AudioClip clip)
    {
        if (clip == null || AudioManager.Instance == null)
            return;

        AudioManager.Instance.PlaySfx(clip);
    }

    /// <summary>판정 구간을 정리하고, 에디터에서 은백색 띠가 즉시 따라오도록 갱신한다.</summary>
    private void OnValidate()
    {
        bandMax = Mathf.Max(bandMin, bandMax);

#if UNITY_EDITOR
        // 인스펙터에서 구간을 조절하는 즉시 은백색 띠가 따라 움직이게 해서
        // 보이는 영역과 판정 영역이 어긋난 채로 작업하는 일이 없게 한다.
        // OnValidate 안에서 RectTransform 을 바로 건드리면 경고가 나므로 한 프레임 미룬다.
        BronzeGauge target = gauge;
        float min = bandMin;
        float max = bandMax;

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (target != null)
                target.SetBand(min, max);
        };
#endif
    }
}
