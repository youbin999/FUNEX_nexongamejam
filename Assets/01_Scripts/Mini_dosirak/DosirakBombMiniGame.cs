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
    [SerializeField] private LayerMask clickableLayers = ~0;

    [Header("배경")]
    [Tooltip("폭탄이 있는 배경. 성공 시 알파가 0으로 페이드되어 아래의 clear 배경이 드러난다")]
    [SerializeField] private SpriteRenderer backgroundRenderer;

    [Header("도시락")]
    [SerializeField] private SpriteRenderer dosirakRenderer;
    [SerializeField] private Collider2D dosirakCollider;

    [Header("폭발")]
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
    [SerializeField] private float explosionAppearDuration = 0.25f;
    [SerializeField] private float explosionHoldDuration = 0.4f;
    [SerializeField] private float explosionFadeDuration = 0.4f;
    [SerializeField] private float backgroundFadeDuration = 0.6f;
    [SerializeField] private float snapBackDuration = 0.25f;

    [Header("이벤트")]
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

    private void Start()
    {
        SetExplosionVisible(false);
    }

    protected override void OnTimedUpdate()
    {
        HandleDragInput();
    }

    protected override void OnTimedPlay()
    {
        ResetInternal();
    }

    protected override void OnTimedStopAndReset()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        ResetInternal();
    }

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

    protected override void OnTimeUp()
    {
        dragging = false;
        inputEnabled = false;
        onFail.Invoke();
        base.OnTimeUp();
    }

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

    private IEnumerator SuccessRoutine()
    {
        yield return StartCoroutine(ThrowAwayRoutine());
        yield return StartCoroutine(ExplosionRoutine());

        onClear.Invoke();
    }

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
            float ease = 1f - (1f - k) * (1f - k); // ease-out: 빠르게 튀어나갔다 감속
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
            float ease = k * k; // ease-in: 처음엔 천천히, 갈수록 빨려들듯 가속
            t.position = Vector3.Lerp(overshootPos, centerPos, ease);
            t.localScale = Vector3.Lerp(startScale, Vector3.zero, ease);
            SetSpriteAlpha(dosirakRenderer, Mathf.Lerp(startColor.a, 0f, ease));
            yield return null;
        }

        dosirakRenderer.gameObject.SetActive(false);
    }

    private IEnumerator ExplosionRoutine()
    {
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

    private void SetExplosionVisible(bool visible)
    {
        if (explosionRenderer == null)
            return;

        SetSpriteAlpha(explosionRenderer, visible ? 1f : 0f);
        explosionRenderer.transform.localScale = explosionStartScale;
        explosionRenderer.gameObject.SetActive(visible);
    }

    private static void SetSpriteAlpha(SpriteRenderer renderer, float alpha)
    {
        Color c = renderer.color;
        c.a = Mathf.Clamp01(alpha);
        renderer.color = c;
    }

    private Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        if (targetCamera == null)
            return screenPosition;

        return targetCamera.ScreenToWorldPoint(screenPosition);
    }

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
