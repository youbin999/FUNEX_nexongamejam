using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 고인돌을 구성하는 물리 돌 하나.
/// Rigidbody2D와 Collider2D를 함께 사용하며, 게임 시작 전에는 물리를 멈춰 프리로드 상태를 안전하게 유지한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class DolmenStone : MonoBehaviour
{
    [Header("안정 판정")]
    [Tooltip("이 속도보다 빨리 움직이면 아직 안정적으로 놓인 돌이 아니다.")]
    [SerializeField] private float stableLinearSpeed = 0.16f;

    [Tooltip("이 값보다 빨리 회전하면 아직 안정적으로 놓인 돌이 아니다.")]
    [SerializeField] private float stableAngularSpeed = 8f;

    [Header("충돌 소리")]
    [Tooltip("다른 돌과 이 속도 이상으로 부딪혀야 소리를 낸다. 살짝 스치는 접촉까지 울리지 않게 걸러준다.")]
    [SerializeField] private float impactMinSpeed = 1.2f;

    [Tooltip("돌끼리 부딪혔을 때 발화한다. SfxPlayer 의 Play 를 연결해서 쓴다.")]
    public UnityEvent onStoneImpact;

    public Collider2D HitCollider => hitCollider;

    private Rigidbody2D body;
    private Collider2D hitCollider;
    private Vector2 startPosition;
    private float startRotation;
    private float initialGravityScale;
    private RigidbodyConstraints2D initialConstraints;
    private bool gameplayPhysicsActive;

    /// <summary>물리 컴포넌트와 처음 자세를 잡아 두고, 프리로드 중에는 물리를 꺼 둔다.</summary>
    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        hitCollider = GetComponent<Collider2D>();
        startPosition = body.position;
        startRotation = body.rotation;
        initialGravityScale = body.gravityScale;
        initialConstraints = body.constraints;

        // MiniGamePlayer의 프리로드 중에는 돌이 굴러 떨어지지 않아야 한다.
        SetGameplayPhysicsActive(false);
    }

    /// <summary>재생 시작/종료에 맞춰 돌의 동적 물리를 켜거나 끈다.</summary>
    public void SetGameplayPhysicsActive(bool active)
    {
        gameplayPhysicsActive = active;
        if (body == null)
            return;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.gravityScale = initialGravityScale;
        body.constraints = initialConstraints;
        body.simulated = active;
        body.bodyType = RigidbodyType2D.Dynamic;
    }

    /// <summary>다음 플레이를 위해 시작 위치, 회전, 속도를 정확히 복원한다.</summary>
    public void ResetToStart()
    {
        if (body == null)
            return;

        body.position = startPosition;
        body.rotation = startRotation;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.gravityScale = initialGravityScale;
        body.constraints = initialConstraints;
        body.simulated = gameplayPhysicsActive;
        body.bodyType = RigidbodyType2D.Dynamic;
    }

    /// <summary>덮개돌에 키보드 방향의 힘을 가하고, 과도한 조작 속도는 제한한다.</summary>
    public void ApplyControlForce(Vector2 direction, float force, float maxSpeed)
    {
        if (!gameplayPhysicsActive || body == null)
            return;

        if (direction.sqrMagnitude > 0f)
            body.AddForce(direction * Mathf.Max(0f, force), ForceMode2D.Force);

        float clampedMaxSpeed = Mathf.Max(0f, maxSpeed);
        if (clampedMaxSpeed > 0f && body.linearVelocity.sqrMagnitude > clampedMaxSpeed * clampedMaxSpeed)
            body.linearVelocity = body.linearVelocity.normalized * clampedMaxSpeed;
    }

    /// <summary>
    /// 다른 돌과 부딪히면 소리 이벤트를 발화한다.
    /// 바닥·경계처럼 <see cref="DolmenStone"/> 이 아닌 것과의 충돌은 무시하고,
    /// 살짝 스치는 접촉은 <see cref="impactMinSpeed"/> 로 걸러낸다.
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!gameplayPhysicsActive)
            return;

        if (collision.collider == null || collision.collider.GetComponent<DolmenStone>() == null)
            return;

        if (collision.relativeVelocity.magnitude < impactMinSpeed)
            return;

        onStoneImpact.Invoke();
    }

    /// <summary>지정한 다른 돌과 실제 물리 접촉 중인지 반환한다.</summary>
    public bool IsRestingOn(DolmenStone otherStone)
    {
        return hitCollider != null && otherStone != null && otherStone.hitCollider != null
            && hitCollider.IsTouching(otherStone.hitCollider);
    }

    /// <summary>속도와 회전 속도가 안정 범위 안인지 반환한다.</summary>
    public bool IsStable()
    {
        if (body == null)
            return false;

        return body.linearVelocity.sqrMagnitude <= stableLinearSpeed * stableLinearSpeed
            && Mathf.Abs(body.angularVelocity) <= stableAngularSpeed;
    }

    /// <summary>시작 회전 기준으로 허용된 기울기 안에 있는지 반환한다.</summary>
    public bool IsAngleWithin(float maximumTilt)
    {
        if (body == null)
            return false;

        return Mathf.Abs(Mathf.DeltaAngle(startRotation, body.rotation)) <= Mathf.Max(0f, maximumTilt);
    }
}
