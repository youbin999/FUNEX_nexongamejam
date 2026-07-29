using UnityEngine;

/// <summary>
/// 용광로 주변을 날아다니는 도구(Tool)들을 관리한다.
/// 도구끼리 부딪히는 처리는 Rigidbody2D 물리에 맡기고, 이 컴포넌트는
/// 시작할 때 무작위 속도를 주고 / 클리어되면 멈추고 / 다음 판을 위해 제자리로 되돌리는 일만 한다.
///
/// 각 도구에는 Rigidbody2D(Gravity Scale 0) + Collider2D + 튕기는 PhysicsMaterial2D 를 붙이고,
/// 화면 밖으로 나가지 않도록 프리팹 안에 벽 콜라이더를 둘러 둔다.
/// </summary>
public class BronzeToolSwarm : MonoBehaviour
{
    [Header("도구")]
    [Tooltip("날아다닐 도구들. 비워두면 자식에서 Rigidbody2D 를 모두 찾아 쓴다")]
    [SerializeField] private Rigidbody2D[] tools;

    [Header("움직임")]
    [Tooltip("시작할 때 주는 속도의 최소값 (유닛/초)")]
    [SerializeField] private float minSpeed = 2.5f;

    [Tooltip("시작할 때 주는 속도의 최대값 (유닛/초)")]
    [SerializeField] private float maxSpeed = 5f;

    [Tooltip("시작할 때 주는 회전 속도의 최대값 (도/초). 0이면 돌지 않는다")]
    [SerializeField] private float maxAngularSpeed = 240f;

    // 다음 판을 위해 되돌릴 처음 자세.
    private Vector3[] startPositions;
    private Quaternion[] startRotations;
    private bool cached;

    /// <summary>도구들이 배치된 자리와 크기를 기억해 둔다.</summary>
    private void Awake()
    {
        CacheStartPose();
    }

    /// <summary>
    /// 도구 목록과 처음 자세를 기억해 둔다.
    /// 비활성 상태로 프리로드되어 Awake 가 늦게 도는 경우가 있어서 값을 쓰기 전에 매번 부른다.
    /// </summary>
    private void CacheStartPose()
    {
        if (cached)
            return;

        cached = true;

        if (tools == null || tools.Length == 0)
            tools = GetComponentsInChildren<Rigidbody2D>(true);

        startPositions = new Vector3[tools.Length];
        startRotations = new Quaternion[tools.Length];

        for (int i = 0; i < tools.Length; i++)
        {
            if (tools[i] == null)
                continue;

            startPositions[i] = tools[i].transform.position;
            startRotations[i] = tools[i].transform.rotation;
        }
    }

    /// <summary>도구들을 무작위 방향으로 날려 보낸다.</summary>
    public void Begin()
    {
        CacheStartPose();

        for (int i = 0; i < tools.Length; i++)
        {
            Rigidbody2D body = tools[i];
            if (body == null)
                continue;

            body.simulated = true;

            float angle = Random.value * Mathf.PI * 2f;
            float speed = Random.Range(minSpeed, maxSpeed);

            body.linearVelocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            body.angularVelocity = Random.Range(-maxAngularSpeed, maxAngularSpeed);
        }
    }

    /// <summary>도구들을 그 자리에 멈춰 세운다. 클리어 연출에서 쓴다.</summary>
    public void Freeze()
    {
        CacheStartPose();

        for (int i = 0; i < tools.Length; i++)
        {
            Rigidbody2D body = tools[i];
            if (body == null)
                continue;

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;

            // 물리를 아예 꺼서 다른 도구가 밀고 지나가도 움직이지 않게 한다.
            body.simulated = false;
        }
    }

    /// <summary>도구들을 처음 자리·자세로 되돌리고 멈춘다. 몇 번 불러도 안전하다.</summary>
    public void ResetAll()
    {
        CacheStartPose();

        for (int i = 0; i < tools.Length; i++)
        {
            Rigidbody2D body = tools[i];
            if (body == null)
                continue;

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;

            body.transform.position = startPositions[i];
            body.transform.rotation = startRotations[i];
        }
    }
}
