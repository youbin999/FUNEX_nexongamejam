using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 아르키메데스 목욕탕 미니게임.
/// D 키를 연타할 때마다(꾹 누르는 것은 무시) 아르키메데스가 한 걸음씩 오른쪽으로 이동하며
/// archimedes_0 / archimedes_1 스프라이트를 번갈아 표시해 걷는 애니메이션을 만든다.
/// 제한 시간 안에 <see cref="bathTrigger"/> 영역에 도달하면 공중제비를 돌고 '유레카!' 텍스트가 팝인되는
/// 연출 후 성공 처리되며, 도달하지 못하면 실패.
/// </summary>
public class ArchimedesBathMiniGame : MiniGame
{
    [Serializable] public class TimerEvent : UnityEvent<float> { }

    [Header("아르키메데스")]
    [SerializeField] private SpriteRenderer archimedesRenderer;
    [SerializeField] private Sprite archimedesFrame0;
    [SerializeField] private Sprite archimedesFrame1;

    [Header("목욕탕 (트리거)")]
    [Tooltip("배경의 목욕탕 비주얼 위치에 맞춰 씬/프리팹에서 자유롭게 옮겨 배치한다. 아르키메데스가 이 영역에 들어오면 성공")]
    [SerializeField] private Collider2D bathTrigger;

    [Header("규칙")]
    [Tooltip("제한 시간(초)")]
    [SerializeField] private float timeLimit = 6f;
    [Tooltip("D 키를 한 번 누를 때마다 이동하는 거리(월드 유닛). 시작 위치부터 목욕탕 트리거까지의 거리를 감안해 '미친듯이 연타'해야 겨우 도달하도록 작게 잡는다")]
    [SerializeField] private float stepDistance = 0.1f;

    [Header("유레카 연출 - 공중제비")]
    [Tooltip("목욕탕 도달 시 아르키메데스가 공중제비를 도는 데 걸리는 시간")]
    [SerializeField] private float somersaultDuration = 0.6f;
    [Tooltip("공중제비 도중 위로 뛰어오르는 높이(월드 유닛)")]
    [SerializeField] private float somersaultJumpHeight = 1.2f;
    [Tooltip("공중제비 회전 횟수 (1 = 360도 한 바퀴)")]
    [SerializeField] private int somersaultSpins = 1;

    [Header("유레카 연출 - 텍스트")]
    [Tooltip("'유레카!' 텍스트가 달린 오브젝트의 CanvasGroup. 알파(투명도) 애니메이션에 쓰며, 같은 오브젝트의 Transform 스케일도 함께 애니메이션한다")]
    [SerializeField] private CanvasGroup eurekaCanvasGroup;
    [Tooltip("텍스트가 스케일+알파로 팝인하는 데 걸리는 시간")]
    [SerializeField] private float eurekaAppearDuration = 0.35f;
    [Tooltip("팝인이 끝난 뒤 게임 종료까지 텍스트를 유지하는 시간")]
    [SerializeField] private float eurekaHoldDuration = 0.8f;
    [Tooltip("팝인 시작 시점의 스케일 배율")]
    [SerializeField] private float eurekaStartScale = 0.3f;

    [Header("이벤트")]
    [Tooltip("0에서 1로 차오르는 게이지 값. Image.fillAmount 에 그대로 연결하면 된다")]
    public TimerEvent onTimerChanged;
    public UnityEvent onClear;
    public UnityEvent onFail;

    private Vector3 startPosition;
    private float elapsed;
    private bool resultLocked;
    private bool useFrame0 = true;
    private Coroutine eurekaRoutine;

    private void Awake()
    {
        if (archimedesRenderer != null)
            startPosition = archimedesRenderer.transform.position;
    }

    private void Start()
    {
        onTimerChanged.Invoke(0f);
        SetEurekaVisible(false);
    }

    private void Update()
    {
        if (!IsPlaying || resultLocked)
            return;

        elapsed += Time.deltaTime;
        onTimerChanged.Invoke(timeLimit > 0f ? Mathf.Clamp01(elapsed / timeLimit) : 1f);

        if (elapsed >= timeLimit)
        {
            Fail();
            return;
        }

        if (Keyboard.current != null && Keyboard.current.dKey.wasPressedThisFrame)
            StepForward();
    }

    protected override void OnPlay()
    {
        ResetInternal();
    }

    protected override void OnStopAndReset()
    {
        if (eurekaRoutine != null)
        {
            StopCoroutine(eurekaRoutine);
            eurekaRoutine = null;
        }

        ResetInternal();
    }

    private void ResetInternal()
    {
        elapsed = 0f;
        resultLocked = false;
        useFrame0 = true;

        if (archimedesRenderer != null)
        {
            archimedesRenderer.transform.position = startPosition;
            archimedesRenderer.transform.rotation = Quaternion.identity;
            archimedesRenderer.sprite = archimedesFrame0;
        }

        SetEurekaVisible(false);
        onTimerChanged.Invoke(0f);
    }

    private void StepForward()
    {
        if (archimedesRenderer == null)
            return;

        archimedesRenderer.transform.position += Vector3.right * stepDistance;

        useFrame0 = !useFrame0;
        archimedesRenderer.sprite = useFrame0 ? archimedesFrame0 : archimedesFrame1;

        if (bathTrigger != null && bathTrigger.OverlapPoint(archimedesRenderer.transform.position))
            Clear();
    }

    private void Clear()
    {
        resultLocked = true;
        eurekaRoutine = StartCoroutine(EurekaRoutine());
    }

    private void Fail()
    {
        resultLocked = true;
        onFail.Invoke();
        ReportFinished(false);
    }

    /// <summary>목욕탕 도달 연출: 공중제비 → '유레카!' 텍스트 팝인 → 유지 후 게임 종료.</summary>
    private IEnumerator EurekaRoutine()
    {
        yield return StartCoroutine(SomersaultRoutine());
        yield return StartCoroutine(EurekaTextRoutine());

        yield return new WaitForSeconds(eurekaHoldDuration);

        eurekaRoutine = null;
        onClear.Invoke();
        ReportFinished(true);
    }

    private IEnumerator SomersaultRoutine()
    {
        if (archimedesRenderer == null)
            yield break;

        Transform t = archimedesRenderer.transform;
        Vector3 groundPosition = t.position;
        float totalSpinDegrees = 360f * Mathf.Max(1, somersaultSpins);

        float time = 0f;
        while (time < somersaultDuration)
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / somersaultDuration);

            // 포물선(sin)으로 뛰어오르는 높이를 만들고, 그 동안 Z축으로 회전시켜 공중제비처럼 보이게 한다.
            float jumpArc = Mathf.Sin(k * Mathf.PI) * somersaultJumpHeight;
            t.position = groundPosition + new Vector3(0f, jumpArc, 0f);
            t.rotation = Quaternion.Euler(0f, 0f, -totalSpinDegrees * k);

            yield return null;
        }

        t.position = groundPosition;
        t.rotation = Quaternion.identity;
    }

    private IEnumerator EurekaTextRoutine()
    {
        if (eurekaCanvasGroup == null)
            yield break;

        SetEurekaVisible(true);

        Transform t = eurekaCanvasGroup.transform;
        float time = 0f;
        while (time < eurekaAppearDuration)
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / eurekaAppearDuration);
            float scale = Mathf.LerpUnclamped(eurekaStartScale, 1f, EaseOutBack(k));

            t.localScale = new Vector3(scale, scale, 1f);
            eurekaCanvasGroup.alpha = k;

            yield return null;
        }

        t.localScale = Vector3.one;
        eurekaCanvasGroup.alpha = 1f;
    }

    private void SetEurekaVisible(bool visible)
    {
        if (eurekaCanvasGroup == null)
            return;

        if (!visible)
        {
            eurekaCanvasGroup.alpha = 0f;
            eurekaCanvasGroup.transform.localScale = Vector3.one * eurekaStartScale;
        }

        eurekaCanvasGroup.gameObject.SetActive(visible);
    }

    /// <summary>살짝 튀어나왔다가 정착하는 back-ease-out 곡선. 텍스트 팝인에 통통 튀는 느낌을 준다.</summary>
    private static float EaseOutBack(float k)
    {
        const float overshoot = 1.70158f;
        k -= 1f;
        return k * k * ((overshoot + 1f) * k + overshoot) + 1f;
    }
}
