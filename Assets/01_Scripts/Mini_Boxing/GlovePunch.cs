using System.Collections;
using UnityEngine;

/// <summary>
/// 글러브 한 짝의 펀치 모션. 키를 누를 때마다 외계인 쪽으로 아주 빠르게 뻗었다가 제자리로 돌아온다.
/// GloveLeft / GloveRight 에 하나씩 붙인다.
/// </summary>
public class GlovePunch : MonoBehaviour
{
    [Header("움직임")]
    [Tooltip("펀치를 뻗는 방향(로컬 기준). 화면 안쪽(위)으로 지르려면 기본값 그대로 둔다")]
    [SerializeField] private Vector2 punchDirection = Vector2.up;

    [Tooltip("기준 위치에서 뻗어 나가는 거리")]
    [SerializeField] private float punchDistance = 2.2f;

    [Tooltip("뻗는 데 걸리는 시간(초). 짧을수록 '슉' 하고 빠르게 나간다")]
    [SerializeField] private float punchOutTime = 0.05f;

    [Tooltip("돌아오는 데 걸리는 시간(초)")]
    [SerializeField] private float punchBackTime = 0.1f;

    [Tooltip("뻗었을 때 커지는 배율. 가까이 다가온 것처럼 보이게 한다")]
    [SerializeField] private float punchScale = 1.25f;

    [Tooltip("뻗을 때 같이 기울어지는 각도 (0이면 회전 없음)")]
    [SerializeField] private float tiltAngle = 6f;

    [SerializeField]
    private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Vector3 restPosition;
    private Quaternion restRotation;
    private Vector3 restScale;
    private Coroutine running;

    private void Awake()
    {
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        restScale = transform.localScale;
    }

    /// <summary>펀치를 한 번 지른다. 연타로 중간에 다시 불려도 현재 자세에서 이어서 재생한다.</summary>
    public void Punch()
    {
        if (running != null)
            StopCoroutine(running);

        running = StartCoroutine(PunchRoutine());
    }

    /// <summary>처음 자세로 되돌린다. (재시작용, 멱등)</summary>
    public void ResetPose()
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        transform.localPosition = restPosition;
        transform.localRotation = restRotation;
        transform.localScale = restScale;
    }

    private IEnumerator PunchRoutine()
    {
        Vector3 targetPos = restPosition + (Vector3)(punchDirection.normalized * punchDistance);
        Quaternion targetRot = restRotation * Quaternion.Euler(0f, 0f, tiltAngle);
        Vector3 targetScale = restScale * punchScale;

        // 1단계: 외계인 쪽으로 슉 뻗는다.
        yield return MoveRoutine(targetPos, targetRot, targetScale, punchOutTime);

        // 2단계: 제자리로 돌아온다.
        yield return MoveRoutine(restPosition, restRotation, restScale, punchBackTime);

        running = null;
    }

    /// <summary>현재 자세에서 목표 자세로 보간한다. 연타로 끊겨도 현재 위치에서 이어지도록 from 을 매번 읽는다.</summary>
    private IEnumerator MoveRoutine(Vector3 toPos, Quaternion toRot, Vector3 toScale, float duration)
    {
        Vector3 fromPos = transform.localPosition;
        Quaternion fromRot = transform.localRotation;
        Vector3 fromScale = transform.localScale;

        if (duration > 0f)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = ease.Evaluate(t / duration);
                transform.localPosition = Vector3.LerpUnclamped(fromPos, toPos, k);
                transform.localRotation = Quaternion.SlerpUnclamped(fromRot, toRot, k);
                transform.localScale = Vector3.LerpUnclamped(fromScale, toScale, k);
                yield return null;
            }
        }

        transform.localPosition = toPos;
        transform.localRotation = toRot;
        transform.localScale = toScale;
    }
}
