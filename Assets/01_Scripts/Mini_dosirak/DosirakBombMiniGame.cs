using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 도시락 폭탄 던지기 미니게임.
/// 도시락을 드래그하다가 위로 빠르게 스냅해서 던지면(속도 임계값 이상) 성공 연출(폭발 + 배경 클리어 페이드)이 재생된다.
/// 제한 시간 안에 던지지 못하면 실패. 프리팹으로 만들어 <see cref="MiniGamePlayer"/> 가 재생한다.
/// </summary>
public class DosirakBombMiniGame : TimedMiniGame
{
    [Header("카메라 / 입력")]
    [Tooltip("비워두면 Camera.main 을 쓴다")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("드래그 판정에 쓸 레이어")]
    [SerializeField] private LayerMask clickableLayers = ~0;

    [Header("배경")]
    [Tooltip("폭탄이 있는 배경. 성공 시 알파가 0으로 페이드되어 아래의 clear 배경이 드러난다")]
    [SerializeField] private SpriteRenderer backgroundRenderer;

    [Header("도시락")]
    [Tooltip("드래그해서 던질 도시락")]
    [SerializeField] private SpriteRenderer dosirakRenderer;

    [Tooltip("도시락을 집었는지 판정할 콜라이더")]
    [SerializeField] private Collider2D dosirakCollider;

    [Header("폭발")]
    [Tooltip("성공 시 화면 중앙에서 터지는 폭발 스프라이트")]
    [SerializeField] private SpriteRenderer explosionRenderer;

    [Header("규칙")]
    [Tooltip("이 속도(월드 유닛/초, 위 방향) 이상으로 놓아야 제대로 던진 것으로 인정한다")]
    [SerializeField] private float throwUpwardSpeedThreshold = 6f;

    [Header("연출 타이밍")]
    [Tooltip("던진 방향(주로 위쪽)으로 화면 밖까지 오버슈트하는 데 걸리는 시간")]
    [SerializeField] private float overshootDuration = 0.18f;

    [Tooltip("오버슈트 거리(월드 유닛). 실제 드래그 속도가 이보다 더 멀리 보내면 그 값을 쓴다")]
    [SerializeField] private float minOvershootDistance = 9f;

    [Tooltip("오버슈트 지점에서 화면 중앙(폭발 위치)으로 빨려 들어가며 작아지는 데 걸리는 시간")]
    [SerializeField] private float suckInDuration = 0.3f;

    [Tooltip("폭발이 작게 시작해 제 크기까지 커지는 시간")]
    [SerializeField] private float explosionAppearDuration = 0.25f;

    [Tooltip("폭발을 제 크기로 유지하는 시간")]
    [SerializeField] private float explosionHoldDuration = 0.4f;

    [Tooltip("폭발이 사라지는 시간")]
    [SerializeField] private float explosionFadeDuration = 0.4f;

    [Tooltip("배경이 걷혀 clear 배경이 드러나는 시간")]
    [SerializeField] private float backgroundFadeDuration = 0.6f;

    [Tooltip("약하게 던져 실패했을 때 도시락이 제자리로 돌아오는 시간")]
    [SerializeField] private float snapBackDuration = 0.25f;

    [Header("이벤트")]
    [Tooltip("폭발 스프라이트가 터져 나오는 순간 발화한다. 폭발음처럼 연출과 붙어야 하는 소리를 연결한다.\n" +
        "onClear 는 페이드까지 다 끝난 뒤라 소리를 걸기엔 너무 늦다")]
    public UnityEvent onExplode;

    public UnityEvent onClear;
    public UnityEvent onFail;

    private readonly List<Collider2D> hitBuffer = new List<Collider2D>();
    private ContactFilter2D filter;

    private Vector3 dosirakStartPosition;
    private Vector3 dosirakStartScale;
    private Color dosirakStartColor;
    private Vector3 explosionStartScale;

    private bool inputEnabled;
    private bool dragging;
    private Vector2 lastWorldPos;
    private Vector2 dragVelocity;
    private Coroutine sequenceRoutine;


    // ── 수명주기 ──

    /// <summary>카메라와 판정 필터를 준비하고, 도시락·폭발의 초기 자세를 기억해 둔다.</summary>
    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(clickableLayers);

        if (dosirakRenderer != null)
        {
            dosirakStartPosition = dosirakRenderer.transform.position;
            dosirakStartScale = dosirakRenderer.transform.localScale;
            dosirakStartColor = dosirakRenderer.color;
        }

        if (explosionRenderer != null)
            explosionStartScale = explosionRenderer.transform.localScale;
    }

    /// <summary>폭발을 숨긴 상태로 시작한다.</summary>
    private void Start()
    {
        SetExplosionVisible(false);
    }

    /// <summary>매 프레임 드래그 입력을 처리한다.</summary>
    protected override void OnTimedUpdate()
    {
        HandleDragInput();
    }

    /// <summary>게임을 시작한다. 도시락과 배경을 초기 상태로 되돌린다.</summary>
    protected override void OnTimedPlay()
    {
        ResetInternal();
    }

    /// <summary>진행 중인 연출을 끊고 초기 상태로 되돌린다.</summary>
    protected override void OnTimedStopAndReset()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        ResetInternal();
    }

    /// <summary>도시락 자세·색·콜라이더, 폭발, 배경 알파를 처음으로 되돌린다.</summary>
    private void ResetInternal()
    {
        dragging = false;
        inputEnabled = true;
        dragVelocity = Vector2.zero;

        if (dosirakRenderer != null)
        {
            dosirakRenderer.transform.position = dosirakStartPosition;
            dosirakRenderer.transform.localScale = dosirakStartScale;
            dosirakRenderer.color = dosirakStartColor;
            dosirakRenderer.gameObject.SetActive(true);
        }

        if (dosirakCollider != null)
            dosirakCollider.enabled = true;

        SetExplosionVisible(false);

        if (backgroundRenderer != null)
            SetSpriteAlpha(backgroundRenderer, 1f);
    }

    /// <summary>제한 시간 안에 던지지 못했다. 입력을 잠그고 실패로 통지한다.</summary>
    protected override void OnTimeUp()
    {
        dragging = false;
        inputEnabled = false;
        onFail.Invoke();
        base.OnTimeUp();
    }


    // ── 드래그 입력 ──

    /// <summary>
    /// 도시락을 집어 끌고 다니다가 놓는 흐름을 처리한다.
    /// - 도시락 위에서 눌러야 드래그가 시작된다
    /// - 드래그 속도는 프레임 속도를 섞어 부드럽게 유지한다
    /// - 놓는 순간 위 방향 속도가 임계값을 넘으면 성공, 아니면 제자리로 돌아온다
    /// </summary>
    private void HandleDragInput()
    {
        if (!inputEnabled || dosirakRenderer == null)
            return;

        if (!TryGetPointer(out Vector2 screenPos, out bool pressed, out bool released, out bool isPressed))
            return;

        if (!dragging)
        {
            if (pressed && IsOnDosirak(screenPos))
            {
                dragging = true;
                lastWorldPos = ScreenToWorld(screenPos);
                dragVelocity = Vector2.zero;
            }
            return;
        }

        Vector2 worldPos = ScreenToWorld(screenPos);
        if (Time.deltaTime > 0f)
        {
            Vector2 frameVelocity = (worldPos - lastWorldPos) / Time.deltaTime;
            dragVelocity = Vector2.Lerp(dragVelocity, frameVelocity, 0.6f);
        }

        dosirakRenderer.transform.position = new Vector3(worldPos.x, worldPos.y, dosirakStartPosition.z);
        lastWorldPos = worldPos;

        if (released || !isPressed)
        {
            dragging = false;
            inputEnabled = false;

            bool goodThrow = dragVelocity.y >= throwUpwardSpeedThreshold;
            if (goodThrow)
            {
                SucceedWhenTimeUp();
                sequenceRoutine = StartCoroutine(SuccessRoutine());
            }
            else
            {
                sequenceRoutine = StartCoroutine(SnapBackRoutine());
            }
        }
    }


    // ── 성공·실패 연출 ──

    /// <summary>던져 보낸 뒤 폭발까지 이어서 재생하고 클리어를 알린다.</summary>
    private IEnumerator SuccessRoutine()
    {
        yield return StartCoroutine(ThrowAwayRoutine());
        yield return StartCoroutine(ExplosionRoutine());

        onClear.Invoke();
    }

    /// <summary>
    /// 던진 도시락이 화면 밖으로 나갔다가 중앙으로 빨려 들어간다.
    /// 1단계는 감속(ease-out)으로 튀어나가고, 2단계는 가속(ease-in)으로 빨려들며 작아진다.
    /// </summary>
    private IEnumerator ThrowAwayRoutine()
    {
        if (dosirakRenderer == null)
            yield break;

        if (dosirakCollider != null)
            dosirakCollider.enabled = false;

        Transform t = dosirakRenderer.transform;
        Vector3 startPos = t.position;
        Vector3 startScale = t.localScale;
        Color startColor = dosirakRenderer.color;
        Vector3 centerPos = explosionRenderer != null ? explosionRenderer.transform.position : new Vector3(0f, 0f, startPos.z);

        // 1단계: 던진 방향(주로 위쪽)으로 화면 밖까지 오버슈트한다.
        Vector2 dir = dragVelocity.sqrMagnitude > 0.0001f ? dragVelocity.normalized : Vector2.up;
        float dist = Mathf.Max(dragVelocity.magnitude * overshootDuration, minOvershootDistance);
        Vector3 overshootPos = startPos + (Vector3)(dir * dist);

        float time = 0f;
        while (time < overshootDuration)
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / overshootDuration);
            float ease = Ease.OutQuad(k); // 빠르게 튀어나갔다 감속
            t.position = Vector3.Lerp(startPos, overshootPos, ease);
            yield return null;
        }

        t.position = overshootPos;

        // 2단계: 화면 중앙(폭발 위치)으로 빨려 들어가며 작아지고 사라진다.
        time = 0f;
        while (time < suckInDuration)
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / suckInDuration);
            float ease = Ease.InQuad(k); // 처음엔 천천히, 갈수록 빨려들듯 가속
            t.position = Vector3.Lerp(overshootPos, centerPos, ease);
            t.localScale = Vector3.Lerp(startScale, Vector3.zero, ease);
            SetSpriteAlpha(dosirakRenderer, Mathf.Lerp(startColor.a, 0f, ease));
            yield return null;
        }

        dosirakRenderer.gameObject.SetActive(false);
    }

    /// <summary>폭발이 커지며 나타났다가, 배경과 함께 걷히며 clear 배경을 드러낸다.</summary>
    private IEnumerator ExplosionRoutine()
    {
        // 터지는 순간에 맞춰 소리를 낸다. 연출이 다 끝난 뒤(onClear)면 너무 늦다.
        onExplode.Invoke();

        if (explosionRenderer != null)
        {
            explosionRenderer.gameObject.SetActive(true);
            explosionRenderer.transform.localScale = explosionStartScale * 0.3f;
            SetSpriteAlpha(explosionRenderer, 0f);

            float time = 0f;
            while (time < explosionAppearDuration)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / explosionAppearDuration);
                explosionRenderer.transform.localScale = Vector3.Lerp(explosionStartScale * 0.3f, explosionStartScale, k);
                SetSpriteAlpha(explosionRenderer, k);
                yield return null;
            }

            explosionRenderer.transform.localScale = explosionStartScale;
            SetSpriteAlpha(explosionRenderer, 1f);
        }

        yield return new WaitForSeconds(explosionHoldDuration);

        float fadeTime = 0f;
        float duration = Mathf.Max(explosionFadeDuration, backgroundFadeDuration, 0.0001f);
        while (fadeTime < duration)
        {
            fadeTime += Time.deltaTime;

            if (explosionRenderer != null)
                SetSpriteAlpha(explosionRenderer, 1f - Mathf.Clamp01(fadeTime / explosionFadeDuration));

            if (backgroundRenderer != null)
                SetSpriteAlpha(backgroundRenderer, 1f - Mathf.Clamp01(fadeTime / backgroundFadeDuration));

            yield return null;
        }

        SetExplosionVisible(false);

        if (backgroundRenderer != null)
            SetSpriteAlpha(backgroundRenderer, 0f);
    }

    /// <summary>약하게 던졌다. 도시락이 제자리로 돌아오고, 시간이 남았으면 다시 잡을 수 있다.</summary>
    private IEnumerator SnapBackRoutine()
    {
        if (dosirakRenderer != null)
        {
            Transform t = dosirakRenderer.transform;
            Vector3 startPos = t.position;
            Vector3 startScale = t.localScale;

            float time = 0f;
            while (time < snapBackDuration)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / snapBackDuration);
                t.position = Vector3.Lerp(startPos, dosirakStartPosition, k);
                t.localScale = Vector3.Lerp(startScale, dosirakStartScale, k);
                yield return null;
            }

            t.position = dosirakStartPosition;
            t.localScale = dosirakStartScale;
        }

        if (!ResultLocked)
            inputEnabled = true;

        sequenceRoutine = null;
    }


    // ── 보조 ──

    /// <summary>폭발 스프라이트를 제 크기로 세워 켜거나 끈다.</summary>
    private void SetExplosionVisible(bool visible)
    {
        if (explosionRenderer == null)
            return;

        SetSpriteAlpha(explosionRenderer, visible ? 1f : 0f);
        explosionRenderer.transform.localScale = explosionStartScale;
        explosionRenderer.gameObject.SetActive(visible);
    }

    /// <summary>RGB 는 유지하고 알파만 바꾼다.</summary>
    private static void SetSpriteAlpha(SpriteRenderer renderer, float alpha)
    {
        Color c = renderer.color;
        c.a = Mathf.Clamp01(alpha);
        renderer.color = c;
    }

    /// <summary>화면 좌표를 월드 좌표로 바꾼다. 카메라가 없으면 그대로 돌려준다.</summary>
    private Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        if (targetCamera == null)
            return screenPosition;

        return targetCamera.ScreenToWorldPoint(screenPosition);
    }

    /// <summary>그 화면 좌표가 도시락 콜라이더 위인지.</summary>
    private bool IsOnDosirak(Vector2 screenPosition)
    {
        if (targetCamera == null)
            return false;

        Vector2 worldPoint = ScreenToWorld(screenPosition);
        int count = Physics2D.OverlapPoint(worldPoint, filter, hitBuffer);
        for (int i = 0; i < count; i++)
        {
            if (hitBuffer[i] == dosirakCollider)
                return true;
        }

        return false;
    }

    /// <summary>마우스를 우선 보고, 없으면 터치를 본다. 둘 다 없으면 false.</summary>
    private bool TryGetPointer(out Vector2 screenPosition, out bool pressedThisFrame, out bool releasedThisFrame, out bool isPressed)
    {
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            screenPosition = mouse.position.ReadValue();
            pressedThisFrame = mouse.leftButton.wasPressedThisFrame;
            releasedThisFrame = mouse.leftButton.wasReleasedThisFrame;
            isPressed = mouse.leftButton.isPressed;
            return true;
        }

        Touchscreen touch = Touchscreen.current;
        if (touch != null)
        {
            screenPosition = touch.primaryTouch.position.ReadValue();
            pressedThisFrame = touch.primaryTouch.press.wasPressedThisFrame;
            releasedThisFrame = touch.primaryTouch.press.wasReleasedThisFrame;
            isPressed = touch.primaryTouch.press.isPressed;
            return true;
        }

        screenPosition = default;
        pressedThisFrame = false;
        releasedThisFrame = false;
        isPressed = false;
        return false;
    }
}
