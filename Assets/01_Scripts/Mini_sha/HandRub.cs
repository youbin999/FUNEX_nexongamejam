using System.Collections;
using UnityEngine;

/// <summary>
/// 손 한 짝의 비비기 모션. 클릭할 때마다 위/아래 두 지점을 오간다.
/// Hand_sha_0(왼손), Hand_sha_1(오른손)에 하나씩 붙인다.
/// </summary>
public class HandRub : MonoBehaviour
{
    [Header("움직임")]
    [Tooltip("기준 위치에서 위/아래로 벌어지는 거리")]
    [SerializeField] private float distance = 0.45f;

    [Tooltip("한 번 움직이는 데 걸리는 시간")]
    [SerializeField] private float moveTime = 0.12f;

    [Tooltip("움직일 때 같이 기울어지는 각도 (0이면 회전 없음)")]
    [SerializeField] private float tiltAngle = 4f;

    [SerializeField]
    private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Vector3 restPosition;
    private Quaternion restRotation;
    private Coroutine running;

    private void Awake()
    {
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
    }

    /// <summary>direction: +1 이면 위로, -1 이면 아래로.</summary>
    public void MoveTo(int direction)
    {
        if (running != null)
            StopCoroutine(running);

        running = StartCoroutine(MoveRoutine(direction));
    }

    /// <summary>처음 자세로 되돌린다. (재시작용)</summary>
    public void ResetPose()
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        transform.localPosition = restPosition;
        transform.localRotation = restRotation;
    }

    private IEnumerator MoveRoutine(int direction)
    {
        Vector3 fromPos = transform.localPosition;
        Quaternion fromRot = transform.localRotation;

        Vector3 toPos = restPosition + Vector3.up * (distance * direction);
        Quaternion toRot = restRotation * Quaternion.Euler(0f, 0f, tiltAngle * direction);

        // 연타로 중간에 끊겨도 현재 위치에서 이어지도록 남은 거리에 비례해 시간을 줄인다.
        float duration = moveTime;
        if (duration > 0f)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = ease.Evaluate(t / duration);
                transform.localPosition = Vector3.LerpUnclamped(fromPos, toPos, k);
                transform.localRotation = Quaternion.SlerpUnclamped(fromRot, toRot, k);
                yield return null;
            }
        }

        transform.localPosition = toPos;
        transform.localRotation = toRot;
        running = null;
    }
}
