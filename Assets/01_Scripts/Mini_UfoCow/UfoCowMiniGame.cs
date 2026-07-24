using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// UFO를 좌우로 움직여 목장의 소를 모두 납치하는 제한시간 미니게임.
/// 소를 전부 흡입하면 UFO가 화면 위로 날아간 뒤 제한시간 종료 시 성공한다.
/// </summary>
public sealed class UfoCowMiniGame : TimedMiniGame
{
    private enum State { Idle, Playing, Escaping, SuccessPending, Failed }

    [Header("구성")]
    [SerializeField] private Transform ufo;
    [SerializeField] private Transform beam;
    [SerializeField] private Transform[] cows;

    [Header("조작")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float horizontalLimit = 7.2f;
    [SerializeField] private float suctionHalfWidth = 1.15f;
    [SerializeField] private float suctionDuration = 0.45f;

    [Header("탈출 연출")]
    [SerializeField] private float escapeAcceleration = 24f;
    [SerializeField] private float escapeDuration = 0.75f;

    [Header("이벤트")]
    public UnityEvent onCowCaptured;
    public UnityEvent onAllCowsCaptured;
    public UnityEvent onFail;

    private Vector3 ufoStart;
    private Vector3[] cowStarts;
    private bool[] captured;
    private bool suctionBusy;
    private State state = State.Idle;

    private void Awake()
    {
        if (ufo == null) ufo = transform.Find("UFO");
        if (beam == null) beam = transform.Find("UFO/Beam");
        if (cows == null || cows.Length == 0)
        {
            Transform herd = transform.Find("Cows");
            if (herd != null)
            {
                cows = new Transform[herd.childCount];
                for (int i = 0; i < cows.Length; i++) cows[i] = herd.GetChild(i);
            }
        }
        CacheStarts();
    }

    protected override void OnTimedPlay()
    {
        ResetObjects();
        state = State.Playing;
    }

    protected override void OnTimedStopAndReset()
    {
        StopAllCoroutines();
        ResetObjects();
        state = State.Idle;
    }

    protected override void OnTimedUpdate()
    {
        if (state != State.Playing || ufo == null) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        float direction = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) direction -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) direction += 1f;
        Vector3 position = ufo.localPosition;
        position.x = Mathf.Clamp(position.x + direction * moveSpeed * Time.deltaTime, -horizontalLimit, horizontalLimit);
        ufo.localPosition = position;

        if (!suctionBusy && keyboard.spaceKey.wasPressedThisFrame)
            TryCaptureCow();
    }

    protected override void OnTimeUp()
    {
        state = State.Failed;
        onFail.Invoke();
        base.OnTimeUp();
    }

    private void TryCaptureCow()
    {
        int target = -1;
        float closest = float.MaxValue;
        for (int i = 0; i < cows.Length; i++)
        {
            if (captured[i] || cows[i] == null) continue;
            float distance = Mathf.Abs(cows[i].position.x - ufo.position.x);
            if (distance <= suctionHalfWidth && distance < closest)
            {
                target = i;
                closest = distance;
            }
        }
        if (target >= 0) StartCoroutine(SuckCow(target));
    }

    private IEnumerator SuckCow(int index)
    {
        suctionBusy = true;
        if (beam != null) beam.gameObject.SetActive(true);
        Transform cow = cows[index];
        Vector3 start = cow.position;
        Vector3 end = ufo.position + Vector3.down * 0.15f;
        float elapsedTime = 0f;
        while (elapsedTime < suctionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / suctionDuration);
            cow.position = Vector3.Lerp(start, end, t);
            cow.localScale = Vector3.one * Mathf.Lerp(1f, 0.12f, t);
            cow.Rotate(0f, 0f, 720f * Time.deltaTime);
            yield return null;
        }
        cow.gameObject.SetActive(false);
        captured[index] = true;
        onCowCaptured.Invoke();
        if (beam != null) beam.gameObject.SetActive(false);
        suctionBusy = false;

        for (int i = 0; i < captured.Length; i++)
            if (!captured[i]) yield break;
        StartCoroutine(Escape());
    }

    private IEnumerator Escape()
    {
        state = State.Escaping;
        onAllCowsCaptured.Invoke();
        float speed = 0f;
        float elapsedTime = 0f;
        while (elapsedTime < escapeDuration)
        {
            elapsedTime += Time.deltaTime;
            speed += escapeAcceleration * Time.deltaTime;
            ufo.localPosition += Vector3.up * speed * Time.deltaTime;
            yield return null;
        }
        state = State.SuccessPending;
        SucceedWhenTimeUp();
    }

    private void CacheStarts()
    {
        if (ufo != null) ufoStart = ufo.localPosition;
        int count = cows == null ? 0 : cows.Length;
        cowStarts = new Vector3[count];
        captured = new bool[count];
        for (int i = 0; i < count; i++)
            if (cows[i] != null) cowStarts[i] = cows[i].localPosition;
    }

    private void ResetObjects()
    {
        suctionBusy = false;
        if (ufo != null) ufo.localPosition = ufoStart;
        if (beam != null) beam.gameObject.SetActive(false);
        if (cows == null || cowStarts == null || cowStarts.Length != cows.Length) CacheStarts();
        for (int i = 0; i < cows.Length; i++)
        {
            captured[i] = false;
            if (cows[i] == null) continue;
            cows[i].gameObject.SetActive(true);
            cows[i].localPosition = cowStarts[i];
            cows[i].localRotation = Quaternion.identity;
            cows[i].localScale = Vector3.one;
        }
    }
}
