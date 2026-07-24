using UnityEngine;

/// <summary>
/// 축 하나를 기준으로 돌아가는 오브젝트. (돌아가는 쪽)
/// <see cref="SpannerMiniGame.onAngleChanged"/> 에 이 컴포넌트의 <see cref="SetAngle"/> 를 연결해서 쓴다.
/// 스패너처럼 축에서 떨어져 있는 물체는 축 둘레를 돌면서 같이 기울어져야 렌치를 돌리는 것처럼 보인다.
/// </summary>
public class ScrewRotator : MonoBehaviour
{
    [Header("축")]
    [Tooltip("이 점을 중심으로 돈다. 볼트 한가운데에 빈 오브젝트를 두고 지정한다. 비워두면 제자리에서 회전만 한다")]
    [SerializeField] private Transform pivot;

    [Header("회전")]
    [Tooltip("각도에 곱하는 배수. -1 이면 입력과 반대로 돈다")]
    [SerializeField] private float angleScale = 1f;

    [Tooltip("0보다 크면 목표 각도를 이 속도로 부드럽게 따라간다. 0이면 즉시 맞춰서 드래그와 1:1로 움직인다")]
    [SerializeField] private float followSpeed = 0f;

    private Vector3 restPosition;
    private Quaternion restRotation;
    private bool cached;

    private float targetAngle;
    private float currentAngle;

    private void Awake()
    {
        CacheRest();
    }

    /// <summary>
    /// 처음 위치/자세를 기억해 둔다.
    /// 각도는 항상 이 기준에서 다시 계산하므로 오래 돌려도 오차가 쌓이지 않는다.
    /// Awake 순서가 보장되지 않아 값을 쓰기 전에 매번 부른다.
    /// </summary>
    private void CacheRest()
    {
        if (cached)
            return;

        cached = true;
        restPosition = transform.position;
        restRotation = transform.rotation;
    }

    /// <summary>돌아간 각도(도)를 넘긴다. 누적값이라 720 처럼 한 바퀴가 넘는 값이 들어와도 된다.</summary>
    public void SetAngle(float degrees)
    {
        CacheRest();
        targetAngle = degrees * angleScale;

        if (followSpeed <= 0f)
        {
            currentAngle = targetAngle;
            Apply();
        }
    }

    /// <summary>처음 자세로 되돌린다. (재시작용)</summary>
    public void ResetPose()
    {
        CacheRest();
        targetAngle = 0f;
        currentAngle = 0f;
        Apply();
    }

    private void Update()
    {
        if (followSpeed <= 0f)
            return;

        currentAngle = Mathf.Lerp(currentAngle, targetAngle, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        Apply();
    }

    private void Apply()
    {
        Quaternion spin = Quaternion.Euler(0f, 0f, currentAngle);

        // 축이 있으면 그 둘레를 돌고(공전), 없으면 제자리에서 돌기만 한다(자전).
        if (pivot != null)
        {
            Vector3 origin = pivot.position;
            transform.position = origin + spin * (restPosition - origin);
        }

        transform.rotation = restRotation * spin;
    }
}
