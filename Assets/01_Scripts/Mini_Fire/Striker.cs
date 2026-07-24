using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 상대에게 부딪히러 갔다가 제자리로 돌아오는 오브젝트. (때리는 쪽)
/// 나갈 때는 빠르게, 돌아올 때는 조금 느리게 움직여야 타격감이 산다.
/// </summary>
public class Striker : MonoBehaviour
{
    [Header("움직임")]
    [Tooltip("부딪히러 가는 방향. 오른쪽 오브젝트를 친다면 (1, 0)")]
    [SerializeField] private Vector2 direction = Vector2.right;

    [Tooltip("기준 위치에서 얼마나 파고들지")]
    [SerializeField] private float distance = 1.2f;

    [Tooltip("부딪히러 가는 시간 (전체 거리 기준)")]
    [SerializeField] private float hitTime = 0.035f;

    [Tooltip("제자리로 돌아오는 시간 (전체 거리 기준)")]
    [SerializeField] private float returnTime = 0.08f;

    [Tooltip("칠 때 기울어지는 각도 (0이면 회전 없음)")]
    [SerializeField] private float tiltAngle = -12f;

    [Header("자연스럽게")]
    [Tooltip("매번 파고드는 거리를 이 비율만큼 무작위로 흔든다. 0이면 항상 똑같이 움직인다")]
    [Range(0f, 0.5f)]
    [SerializeField] private float distanceJitter = 0.12f;

    [Tooltip("매번 기울기를 이 각도만큼 무작위로 흔든다")]
    [SerializeField] private float angleJitter = 5f;

    [SerializeField]
    private AnimationCurve hitEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField]
    private AnimationCurve returnEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("이벤트")]
    [Tooltip("상대에 닿는 순간. 스파크 이펙트나 사운드, 맞는 쪽의 Hit() 을 연결한다")]
    public UnityEvent onImpact;

    private Vector3 restPosition;
    private Quaternion restRotation;
    private Coroutine running;

    // 연타로 끊겼을 때 접촉을 보정하기 위한 정보
    private Vector3 pendingContactPosition;
    private Quaternion pendingContactRotation;
    private bool reachedContact = true;

    private void Awake()
    {
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;

        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.right;

        direction.Normalize();
    }

    /// <summary>한 번 부딪히고 돌아온다. 연타하면 현재 위치에서 다시 시작한다.</summary>
    public void Strike()
    {
        if (running != null)
        {
            StopCoroutine(running);

            // 접촉 전에 끊겼다면 그 타격은 없던 일이 돼 버린다.
            // 누른 만큼은 반드시 부딪히도록 접촉 처리를 하고 넘어간다.
            if (!reachedContact)
            {
                transform.localPosition = pendingContactPosition;
                transform.localRotation = pendingContactRotation;
                reachedContact = true;
                onImpact.Invoke();
            }
        }

        running = StartCoroutine(StrikeRoutine());
    }

    /// <summary>처음 자세로 되돌린다. (재시작용)</summary>
    public void ResetPose()
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        reachedContact = true;
        transform.localPosition = restPosition;
        transform.localRotation = restRotation;
    }

    private IEnumerator StrikeRoutine()
    {
        float hitDistance = distance * (1f + Random.Range(-distanceJitter, distanceJitter));
        float angle = tiltAngle + Random.Range(-angleJitter, angleJitter);

        Vector3 contactPosition = restPosition + (Vector3)(direction * hitDistance);
        Quaternion contactRotation = restRotation * Quaternion.Euler(0f, 0f, angle);

        // 도중에 끊겼을 때 어디까지 갔어야 했는지 남겨 둔다.
        pendingContactPosition = contactPosition;
        pendingContactRotation = contactRotation;
        reachedContact = false;

        yield return MoveRoutine(contactPosition, contactRotation, hitTime, hitEase);

        reachedContact = true;
        onImpact.Invoke();

        yield return MoveRoutine(restPosition, restRotation, returnTime, returnEase);

        running = null;
    }

    private IEnumerator MoveRoutine(Vector3 toPos, Quaternion toRot, float fullDuration, AnimationCurve ease)
    {
        Vector3 fromPos = transform.localPosition;
        Quaternion fromRot = transform.localRotation;

        // 연타로 중간에 끊기면 남은 거리가 짧아진다.
        // 시간을 그대로 쓰면 그만큼 느려 보이므로 거리에 비례해 줄여서 속도를 일정하게 유지한다.
        float duration = fullDuration;
        if (distance > 0.0001f)
            duration *= Mathf.Clamp01(Vector3.Distance(fromPos, toPos) / distance);

        if (duration > 0.0001f)
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

        // 접촉 자세가 최소 한 프레임은 화면에 남아야 부딪힌 게 보인다.
        yield return null;
    }
}
