using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// "누르지 마시오" 미니게임.
/// 제한 시간(기본 5초) 동안 버튼을 누르지 않고 버티면 성공, 한 번이라도 누르면 실패한다.
/// 성공이든 실패든 판정 즉시 끝나지 않고, 결과 연출(눌린 이미지 등)을 잠깐 보여준 뒤
/// <see cref="MiniGame.ReportFinished"/> 로 통지한다. 프리팹으로 만들어 <see cref="MiniGamePlayer"/> 가 재생한다.
/// </summary>
public class HackMiniGame : TimedMiniGame
{
    [Header("카메라")]
    [Tooltip("클릭 좌표 변환에 쓸 카메라. 비워두면 Camera.main 을 쓴다")]
    [SerializeField] private Camera targetCamera;

    [Header("버튼")]
    [Tooltip("누르면 실패하는 버튼의 스프라이트 렌더러")]
    [SerializeField] private SpriteRenderer buttonRenderer;

    [Header("버튼 스프라이트")]
    [Tooltip("평소(누르기 전) 스프라이트")]
    [SerializeField] private Sprite normalSprite;

    [Tooltip("눌렸을 때 교체할 스프라이트")]
    [SerializeField] private Sprite pressedSprite;

    [Header("결과 연출")]
    [Tooltip("성공/실패가 판정된 뒤 결과를 보여주고 다음으로 넘어가기까지 대기하는 시간(초)")]
    [SerializeField] private float resultHoldDuration = 1f;

    [Header("성공 연출 - 엄지")]
    [Tooltip("성공 시 왼쪽에서 튀어나올 엄지 스프라이트")]
    [SerializeField] private SpriteRenderer leftThumb;

    [Tooltip("성공 시 오른쪽에서 튀어나올 엄지 스프라이트")]
    [SerializeField] private SpriteRenderer rightThumb;

    [Tooltip("엄지가 숨어있을 때 각자 화면 바깥(좌/우)으로 밀어낼 거리(월드 유닛)")]
    [SerializeField] private float thumbHideDistance = 8f;

    [Tooltip("엄지가 제자리로 튀어나오는 데 걸리는 시간(초)")]
    [SerializeField] private float thumbPopDuration = 0.35f;

    [Header("실패 연출 - 경고")]
    [Tooltip("실패 시 튀어나올 경고 스프라이트들(4개). 각자 배치한 위치·크기 그대로 튀어나온다")]
    [SerializeField] private SpriteRenderer[] warnings;

    [Tooltip("경고 하나가 0에서 제 크기로 튀어나오는 데 걸리는 시간(초)")]
    [SerializeField] private float warningPopDuration = 0.3f;

    [Tooltip("경고들이 순차로 튀어나오는 간격(초). 0이면 동시에 튀어나온다")]
    [SerializeField] private float warningStagger = 0.06f;

    [Header("진행 중 화면 흔들림")]
    [Tooltip("게임이 시작되는 순간부터 성공/실패가 확정될 때까지 계속 이어지는 진동의 세기(월드 유닛).\n" +
        "0이면 진동하지 않는다. 실패 순간의 강한 흔들림은 아래 '실패 연출' 쪽이 따로 담당한다")]
    [SerializeField] private float tensionShakeMagnitude = 0.1f;

    [Tooltip("진동의 빠르기(Hz). 높을수록 잘게 떤다")]
    [SerializeField] private float tensionShakeFrequency = 12f;

    [Tooltip("진동에 같이 실릴 화면 기울기(도). 0이면 위치만 흔들린다")]
    [SerializeField] private float tensionShakeRotation = 0.4f;

    [Header("실패 연출 - 화면 흔들림")]
    [Tooltip("실패 시 카메라가 흔들리는 시간(초). 0이면 흔들지 않는다")]
    [SerializeField] private float shakeDuration = 0.3f;

    [Tooltip("흔들림의 최대 세기(월드 유닛). 시간이 지날수록 점점 잦아든다")]
    [SerializeField] private float shakeMagnitude = 0.25f;

    [Header("배경음")]
    [Tooltip("게임이 시작되는 순간부터 판정이 날 때까지 루프로 깔리는 소리. 비워두면 재생하지 않는다")]
    [SerializeField] private AudioClip bgmClip;

    [Tooltip("배경음 원본 크기. 설정 창의 BGM 게이지 값에 이 값을 곱한다")]
    [Range(0f, 1f)]
    [SerializeField] private float bgmVolume = 0.6f;

    [Tooltip("이 배경음이 도는 동안 전역 BGM 을 낮출 비율. 0.15 면 원래 크기의 15%. 1이면 낮추지 않는다.\n" +
        "전역 BGM 을 멈췄다 다시 트는 게 아니라 볼륨만 낮추므로 곡이 처음부터 다시 시작하지 않는다")]
    [Range(0f, 1f)]
    [SerializeField] private float globalBgmDuck = 0.15f;

    [Header("이벤트")]
    public UnityEvent onClear;
    public UnityEvent onFail;

    // 배경음 전용 AudioSource. 프리팹에 심지 않고 필요할 때 만들어 쓴다(SfxPlayer 의 폴백과 같은 방식).
    private AudioSource bgmSource;

    // 판정이 끝나 결과 연출 중인지 여부. true 인 동안은 추가 입력/재판정을 받지 않는다.
    private bool resolving;
    private Coroutine resolveRoutine;

    // 엄지가 튀어나와 멈출 제자리 위치(프리팹에 배치된 로컬 좌표를 그대로 캐시한다).
    private Vector3 leftThumbShownPos;
    private Vector3 rightThumbShownPos;

    // 경고가 튀어나와 멈출 제 크기(프리팹에 배치된 로컬 스케일을 그대로 캐시한다).
    private Vector3[] warningShownScales;

    // 화면 흔들림 처리용. 흔들기 시작 시점의 카메라 원위치를 저장해 두었다가 끝나면 복원한다.
    private Coroutine shakeRoutine;
    private Vector3 cameraRestorePos;
    private bool cameraShaking;

    // 진행 중 지속 진동의 핸들. 0이면 걸려 있지 않다(StartWobble 은 1부터 발급한다).
    private int tensionShakeId;

    /// <summary>카메라를 확보하고 엄지·경고 표시가 배치된 자리를 기억해 둔다.</summary>
    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (leftThumb != null)
            leftThumbShownPos = leftThumb.transform.localPosition;
        if (rightThumb != null)
            rightThumbShownPos = rightThumb.transform.localPosition;

        if (warnings != null)
        {
            warningShownScales = new Vector3[warnings.Length];
            for (int i = 0; i < warnings.Length; i++)
                if (warnings[i] != null)
                    warningShownScales[i] = warnings[i].transform.localScale;
        }
    }

    /// <summary>게임을 시작한다. 호출 시점에 타이머는 이미 0으로 초기화돼 있다.</summary>
    protected override void OnTimedPlay()
    {
        ResetInternal();
        CacheCameraRestorePos();
        StartTensionShake();
        StartBgm();
    }

    /// <summary>
    /// 버튼을 참고 있는 동안 화면을 계속 떨게 한다. 결과가 확정되면 <see cref="StopTensionShake"/> 가 멈춘다.
    ///
    /// 직접 카메라 transform 을 만지지 않고 <see cref="CameraEffectManager"/> 를 거치는 이유는,
    /// 앞선 게임에서 넘어온 실패 패널티(예: PtsdWobble)가 이미 카메라를 잡고 있을 수 있어서다.
    /// 매니저는 걸려 있는 효과의 오프셋을 전부 합산하지만, transform 을 직접 쓰면 서로 덮어써서
    /// 둘 중 하나가 통째로 사라진다. 매니저는 unscaledTime 을 쓰므로 설정 창(ESC)으로 멈춰도 진동이 이어진다.
    /// </summary>
    private void StartTensionShake()
    {
        if (tensionShakeMagnitude <= 0f)
            return;

        // 이미 걸려 있으면 겹쳐 걸지 않는다 — 진폭이 두 배가 된다.
        StopTensionShake();

        tensionShakeId = CameraEffectManager.Instance.StartWobble(
            tensionShakeMagnitude, tensionShakeRotation, 0f, tensionShakeFrequency);
    }

    /// <summary>지속 진동을 멈춘다. 걸려 있지 않아도 안전하다(멱등).</summary>
    private void StopTensionShake()
    {
        if (tensionShakeId == 0)
            return;

        // 정리 경로에서도 불리므로 매니저를 새로 만들지 않는다.
        CameraEffectManager manager = CameraEffectManager.TryGetInstance;
        if (manager != null)
            manager.StopWobble(tensionShakeId);

        tensionShakeId = 0;
    }

    /// <summary>
    /// 실패 흔들림이 되돌아올 기준 위치를 재생 시작 시점에 잡아 둔다.
    /// 지속 진동이 도는 중에 읽으면 흔들린 좌표가 기준이 되어 카메라가 제자리로 돌아오지 못한다.
    /// </summary>
    private void CacheCameraRestorePos()
    {
        if (targetCamera != null)
            cameraRestorePos = targetCamera.transform.localPosition;
    }

    /// <summary>배경음을 처음부터 재생한다. 클립이 없으면 아무것도 하지 않는다.</summary>
    private void StartBgm()
    {
        if (bgmClip == null)
            return;

        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;

            // 2D 로 재생한다. 3D 로 두면 카메라와의 거리에 따라 소리가 죽는다.
            bgmSource.spatialBlend = 0f;
            bgmSource.clip = bgmClip;
        }

        bgmSource.volume = GameSettings.BgmVolume * bgmVolume;
        bgmSource.time = 0f;
        bgmSource.Play();

        // 전역 BGM 이 그대로 깔려 있으면 서로 잡아먹는다. 끊지 않고 볼륨만 낮춘다.
        if (globalBgmDuck < 1f && AudioManager.Instance != null)
            AudioManager.Instance.DuckBgm(globalBgmDuck);
    }

    /// <summary>배경음을 멈추고 전역 BGM 을 되돌린다. 재생 중이 아닐 때 불러도 안전하다(멱등).</summary>
    private void StopBgm()
    {
        if (bgmSource != null)
            bgmSource.Stop();

        if (AudioManager.Instance != null)
            AudioManager.Instance.RestoreBgm();
    }

    /// <summary>
    /// 어떤 경로로 꺼지든 전역 BGM 과 지속 진동은 반드시 되돌린다.
    /// 둘 다 이 게임 바깥(AudioManager / CameraEffectManager)에 걸어 둔 상태라,
    /// 걸어 놓은 채 사라지면 남은 판 내내 BGM 이 작고 화면이 떨린다.
    /// </summary>
    private void OnDisable()
    {
        StopBgm();
        StopTensionShake();
    }

    /// <summary>게임을 중단하고 초기 상태로 되돌린다. 멱등하다.</summary>
    protected override void OnTimedStopAndReset()
    {
        StopBgm();
        StopTensionShake();

        if (resolveRoutine != null)
        {
            StopCoroutine(resolveRoutine);
            resolveRoutine = null;
        }

        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        // 흔들리던 중에 중단됐다면 카메라를 원위치로 되돌린다.
        if (cameraShaking && targetCamera != null)
            targetCamera.transform.localPosition = cameraRestorePos;
        cameraShaking = false;

        ResetInternal();
    }

    /// <summary>버튼을 다시 누를 수 있는 초기 상태(평소 스프라이트)로 되돌린다. 멱등.</summary>
    private void ResetInternal()
    {
        resolving = false;

        if (buttonRenderer != null && normalSprite != null)
            buttonRenderer.sprite = normalSprite;

        HideThumb(leftThumb);
        HideThumb(rightThumb);

        if (warnings != null)
        {
            for (int i = 0; i < warnings.Length; i++)
            {
                if (warnings[i] == null)
                    continue;

                if (warningShownScales != null && i < warningShownScales.Length)
                    warnings[i].transform.localScale = warningShownScales[i];
                warnings[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>매 프레임 버튼 클릭 여부만 감시한다. 결과가 확정되기 전까지만 호출된다.</summary>
    protected override void OnTimedUpdate()
    {
        if (resolving)
            return;

        if (TryGetPressPosition(out Vector2 screenPos) && IsPointerOnButton(screenPos))
            BeginResolve(false);
    }

    /// <summary>
    /// 제한 시간을 다 버텨냈다 — 이 게임에서는 시간 초과가 곧 성공이다.
    /// 이미 실패 연출이 진행 중이면 그대로 두고, 아니면 성공 연출을 시작한다.
    /// </summary>
    protected override void OnTimeUp()
    {
        if (resolving)
            return;

        BeginResolve(true);
    }

    /// <summary>
    /// 성공/실패를 확정하고 결과 연출을 시작한다. 즉시 통지하지 않고 <see cref="resultHoldDuration"/>
    /// 만큼 결과를 보여준 뒤 <see cref="ResolveRoutine"/> 에서 통지한다.
    /// </summary>
    private void BeginResolve(bool success)
    {
        resolving = true;

        // 긴장 진동은 여기서 끝난다 — 결과 연출은 자기 흔들림을 따로 쓴다.
        StopTensionShake();

        // 실패(버튼 누름)일 때만 눌린 이미지로 바꾸고 화면을 흔든다. 성공은 버튼을 안 눌렀으므로 그대로 둔다.
        if (!success)
        {
            if (buttonRenderer != null && pressedSprite != null)
                buttonRenderer.sprite = pressedSprite;

            if (targetCamera != null && shakeDuration > 0f)
                shakeRoutine = StartCoroutine(ShakeCameraRoutine());
        }

        // 결과음(Aha / Hack_Fail)이 묻히지 않도록 배경음을 먼저 끊는다.
        StopBgm();

        if (success)
            onClear.Invoke();
        else
            onFail.Invoke();

        resolveRoutine = StartCoroutine(ResolveRoutine(success));
    }

    /// <summary>결과를 잠깐 보여준 뒤 종료를 통지한다. 성공이면 엄지, 실패면 경고 연출을 먼저 재생한다.</summary>
    private IEnumerator ResolveRoutine(bool success)
    {
        if (success)
            yield return PopThumbsRoutine();
        else
            yield return PopWarningsRoutine();

        if (resultHoldDuration > 0f)
            yield return new WaitForSeconds(resultHoldDuration);

        resolveRoutine = null;
        ReportFinished(success);
    }

    /// <summary>경고 스프라이트 여러 개를 스케일 0에서 제 크기로 통통 튀어나오게 한다(순차 stagger 지원).</summary>
    private IEnumerator PopWarningsRoutine()
    {
        if (warnings == null || warnings.Length == 0)
            yield break;

        // 모두 스케일 0으로 두고 활성화한다.
        for (int i = 0; i < warnings.Length; i++)
        {
            if (warnings[i] == null)
                continue;

            warnings[i].transform.localScale = Vector3.zero;
            warnings[i].gameObject.SetActive(true);
        }

        float total = warningPopDuration + warningStagger * Mathf.Max(0, warnings.Length - 1);
        float time = 0f;
        while (time < total)
        {
            time += Time.deltaTime;
            for (int i = 0; i < warnings.Length; i++)
            {
                if (warnings[i] == null)
                    continue;

                Vector3 shown = warningShownScales != null && i < warningShownScales.Length
                    ? warningShownScales[i]
                    : Vector3.one;
                float progress = warningPopDuration > 0f
                    ? (time - warningStagger * i) / warningPopDuration
                    : 1f;
                float e = Ease.OutBack(Mathf.Clamp01(progress));
                warnings[i].transform.localScale = Vector3.LerpUnclamped(Vector3.zero, shown, e);
            }

            yield return null;
        }

        for (int i = 0; i < warnings.Length; i++)
        {
            if (warnings[i] == null)
                continue;

            warnings[i].transform.localScale = warningShownScales != null && i < warningShownScales.Length
                ? warningShownScales[i]
                : Vector3.one;
        }
    }

    /// <summary>왼쪽·오른쪽 엄지를 화면 밖에서 제자리로 통통 튀어나오게 한다(ease-out-back 오버슈트).</summary>
    private IEnumerator PopThumbsRoutine()
    {
        Vector3 leftHidden = leftThumbShownPos + Vector3.left * thumbHideDistance;
        Vector3 rightHidden = rightThumbShownPos + Vector3.right * thumbHideDistance;

        ShowThumbAt(leftThumb, leftHidden);
        ShowThumbAt(rightThumb, rightHidden);

        float time = 0f;
        while (time < thumbPopDuration)
        {
            time += Time.deltaTime;
            float e = Ease.OutBack(Mathf.Clamp01(time / thumbPopDuration));

            if (leftThumb != null)
                leftThumb.transform.localPosition = Vector3.LerpUnclamped(leftHidden, leftThumbShownPos, e);
            if (rightThumb != null)
                rightThumb.transform.localPosition = Vector3.LerpUnclamped(rightHidden, rightThumbShownPos, e);

            yield return null;
        }

        if (leftThumb != null)
            leftThumb.transform.localPosition = leftThumbShownPos;
        if (rightThumb != null)
            rightThumb.transform.localPosition = rightThumbShownPos;
    }

    /// <summary>
    /// 카메라를 무작위로 흔들었다가 원위치로 되돌린다. 시간이 지날수록 세기가 잦아든다.
    /// 기준 위치는 재생 시작 시점에 잡아 둔 <see cref="cameraRestorePos"/> 다 —
    /// 여기서 현재 좌표를 읽으면 직전까지 돌던 긴장 진동의 오프셋이 기준에 섞여 카메라가 밀린 채 남는다.
    /// </summary>
    private IEnumerator ShakeCameraRoutine()
    {
        Transform cam = targetCamera.transform;
        cameraShaking = true;

        float time = 0f;
        while (time < shakeDuration)
        {
            time += Time.deltaTime;
            // 뒤로 갈수록 세기가 잦아든다(선형). 끝까지 세게 유지하다 마지막에만 사그라든다.
            float damper = 1f - Mathf.Clamp01(time / shakeDuration);

            // 매 프레임 '최대 세기'로 방향만 무작위로 튄다(반지름 랜덤이 아니라 각도 랜덤).
            float angle = Random.value * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (shakeMagnitude * damper);
            cam.localPosition = cameraRestorePos + offset;
            yield return null;
        }

        cam.localPosition = cameraRestorePos;
        cameraShaking = false;
        shakeRoutine = null;
    }

    /// <summary>엄지를 지정 위치에 놓고 활성화한다.</summary>
    private static void ShowThumbAt(SpriteRenderer thumb, Vector3 localPosition)
    {
        if (thumb == null)
            return;

        thumb.transform.localPosition = localPosition;
        thumb.gameObject.SetActive(true);
    }

    /// <summary>엄지를 비활성화한다.</summary>
    private static void HideThumb(SpriteRenderer thumb)
    {
        if (thumb != null)
            thumb.gameObject.SetActive(false);
    }

    /// <summary>이번 프레임에 눌림(마우스 좌클릭/터치 시작)이 있었으면 화면 좌표를 반환한다.</summary>
    private static bool TryGetPressPosition(out Vector2 screenPosition)
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPosition = mouse.position.ReadValue();
            return true;
        }

        Touchscreen touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = touch.primaryTouch.position.ReadValue();
            return true;
        }

        screenPosition = default;
        return false;
    }

    /// <summary>화면 좌표가 버튼 스프라이트의 영역(월드 AABB) 안에 있는지 검사한다.</summary>
    private bool IsPointerOnButton(Vector2 screenPosition)
    {
        if (buttonRenderer == null || targetCamera == null)
            return false;

        Vector3 world = targetCamera.ScreenToWorldPoint(screenPosition);
        Bounds bounds = buttonRenderer.bounds;
        return world.x >= bounds.min.x && world.x <= bounds.max.x
            && world.y >= bounds.min.y && world.y <= bounds.max.y;
    }
}
