using System.Collections;
using UnityEngine;

/// <summary>
/// 맞으면 뒤로 밀렸다가 제자리로 돌아오는 오브젝트. (맞는 쪽)
/// Striker 의 onImpact 에 이 컴포넌트의 Hit() 을 연결해서 쓴다.
/// 맞는 쪽이 반응해야 부딪힌 것처럼 보인다.
/// </summary>
public class StrikeTarget : MonoBehaviour
{
    [Header("반응")]
    [Tooltip("밀려나는 방향. 왼쪽에서 맞는다면 (1, 0)")]
    [SerializeField] private Vector2 knockDirection = Vector2.right;

    [Tooltip("밀려나는 거리")]
    [SerializeField] private float knockDistance = 0.25f;

    [Tooltip("제자리로 돌아오는 시간")]
    [SerializeField] private float recoverTime = 0.1f;

    [Tooltip("맞을 때 흔들리는 각도 (0이면 회전 없음)")]
    [SerializeField] private float shakeAngle = 6f;

    [Tooltip("맞는 순간 눌리는 정도. 빠른 연타에서는 이게 타격감을 대부분 만든다")]
    [Range(0f, 0.4f)]
    [SerializeField] private float squash = 0.12f;

    [SerializeField]
    private AnimationCurve recoverEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Vector3 restPosition;
    private Quaternion restRotation;
    private Vector3 restScale;
    private Coroutine running;

    /// <summary>기준 자세를 기억하고 튕겨나갈 방향을 정규화해 둔다.</summary>
    private void Awake()
    {
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        restScale = transform.localScale;

        if (knockDirection.sqrMagnitude < 0.0001f)
            knockDirection = Vector2.right;

        knockDirection.Normalize();
    }

    /// <summary>한 대 맞은 반응을 재생한다.</summary>
    public void Hit()
    {
        if (running != null)
            StopCoroutine(running);

        running = StartCoroutine(HitRoutine());
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
        transform.localScale = restScale;
    }

    /// <summary>맞은 자세(밀림·눌림·기울기)를 즉시 잡았다가 원래 자세로 돌아온다.</summary>
    private IEnumerator HitRoutine()
    {
        Vector3 knockedPosition = restPosition + (Vector3)(knockDirection * knockDistance);

        // 매번 같은 방향으로만 흔들리면 기계적으로 보여서 부호를 뒤집어 준다.
        float angle = Random.value < 0.5f ? shakeAngle : -shakeAngle;
        Quaternion knockedRotation = restRotation * Quaternion.Euler(0f, 0f, angle);

        // 맞는 방향으로 눌리고 직각 방향으로 부푼다.
        Vector3 knockedScale = new Vector3(
            restScale.x * (1f - squash),
            restScale.y * (1f + squash),
            restScale.z);

        // 밀려나는 자세는 즉시 잡는다.
        // 연타로 끊겨도 눌린 모습을 건너뛰지 않고, 히트 리액션은 튀는 편이 타격감도 좋다.
        transform.localPosition = knockedPosition;
        transform.localRotation = knockedRotation;
        transform.localScale = knockedScale;
        yield return null;

        yield return MoveRoutine(restPosition, restRotation, restScale, recoverTime, recoverEase);

        running = null;
    }

    /// <summary>현재 자세에서 목표 자세까지 위치·회전·크기를 함께 보간한다.</summary>
    private IEnumerator MoveRoutine(Vector3 toPos, Quaternion toRot, Vector3 toScale, float duration, AnimationCurve ease)
    {
        Vector3 fromPos = transform.localPosition;
        Quaternion fromRot = transform.localRotation;
        Vector3 fromScale = transform.localScale;

        if (duration > 0f)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float raw = t / duration;
                float k = ease != null ? ease.Evaluate(raw) : raw;
                transform.localPosition = Vector3.LerpUnclamped(fromPos, toPos, k);
                transform.localRotation = Quaternion.SlerpUnclamped(fromRot, toRot, k);
                transform.localScale = Vector3.LerpUnclamped(fromScale, toScale, k);
                yield return null;
            }
        }

        transform.localPosition = toPos;
        transform.localRotation = toRot;
        transform.localScale = toScale;

        // 눌린 자세가 최소 한 프레임은 화면에 남아야 맞은 게 보인다.
        yield return null;
    }
}
