using System.Collections;
using UnityEngine;

/// <summary>
/// 삽 한 자루의 자세 연출. 기본 자세 / 흙에 박은 자세 / 퍼올린 자세 세 가지를 오간다.
/// 판정은 <see cref="DigMiniGame"/> 이 하고, 이 스크립트는 보이는 것만 담당한다.
/// 삽 스프라이트 오브젝트에 붙인다.
/// </summary>
public class ShovelMotion : MonoBehaviour
{
    [Header("흙에 박은 자세")]
    [Tooltip("기본 자세에서 얼마나 내려가는지")]
    [SerializeField] private Vector2 insertOffset = new Vector2(0f, -0.8f);

    [Tooltip("박을 때 기울어지는 각도")]
    [SerializeField] private float insertAngle = -15f;

    [Header("퍼올린 자세")]
    [Tooltip("기본 자세에서 얼마나 올라가는지")]
    [SerializeField] private Vector2 liftOffset = new Vector2(0.2f, 0.7f);

    [Tooltip("퍼올릴 때 기울어지는 각도")]
    [SerializeField] private float liftAngle = 35f;

    [Header("속도")]
    [Tooltip("흙에 박는 데 걸리는 시간")]
    [SerializeField] private float insertTime = 0.1f;

    [Tooltip("퍼올리는 데 걸리는 시간")]
    [SerializeField] private float liftTime = 0.16f;

    [Tooltip("기본 자세로 돌아오는 데 걸리는 시간")]
    [SerializeField] private float returnTime = 0.14f;

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

    /// <summary>기본 자세(흙 위)로 돌아온다.</summary>
    public void MoveToIdle()
    {
        Move(Vector2.zero, 0f, returnTime);
    }

    /// <summary>삽을 흙 밑으로 밀어 넣는다.</summary>
    public void MoveToInserted()
    {
        Move(insertOffset, insertAngle, insertTime);
    }

    /// <summary>흙을 퍼서 들어올린다.</summary>
    public void MoveToLifted()
    {
        Move(liftOffset, liftAngle, liftTime);
    }

    /// <summary>연출을 끊고 즉시 처음 자세로 되돌린다. (재시작용)</summary>
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

    private void Move(Vector2 offset, float angle, float duration)
    {
        if (running != null)
            StopCoroutine(running);

        Vector3 toPosition = restPosition + (Vector3)offset;
        Quaternion toRotation = restRotation * Quaternion.Euler(0f, 0f, angle);

        // 비활성 상태에서는 코루틴을 돌릴 수 없으니 값만 바로 넣는다.
        if (!isActiveAndEnabled)
        {
            transform.localPosition = toPosition;
            transform.localRotation = toRotation;
            return;
        }

        running = StartCoroutine(MoveRoutine(toPosition, toRotation, duration));
    }

    private IEnumerator MoveRoutine(Vector3 toPosition, Quaternion toRotation, float duration)
    {
        Vector3 fromPosition = transform.localPosition;
        Quaternion fromRotation = transform.localRotation;

        if (duration > 0f)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = ease.Evaluate(t / duration);
                transform.localPosition = Vector3.LerpUnclamped(fromPosition, toPosition, k);
                transform.localRotation = Quaternion.SlerpUnclamped(fromRotation, toRotation, k);
                yield return null;
            }
        }

        transform.localPosition = toPosition;
        transform.localRotation = toRotation;
        running = null;
    }
}
