using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 천장(앵커)에 줄로 매달린 그림 한 장.
/// 시작하면 줄이 풀리듯 빠르게 아래로 떨어지고, 줄의 탄성 때문에 위아래로 몇 번 튕겼다가
/// 좌우로 흔들리며 잦아든다. 마우스가 그림에 닿으면 그 움직임을 따라 밀린다.
///
/// 스프링 진자(spring pendulum)를 직접 적분하므로 Rigidbody2D 는 필요 없다.
/// 마우스 판정에는 Collider2D 가 있으면 그것을, 없으면 <see cref="mouseRadius"/> 를 쓴다.
/// </summary>
[DisallowMultipleComponent]
public class HangingSprite : MonoBehaviour
{
    // 프레임 레이트가 흔들려도 반동이 똑같이 보이도록 고정 간격으로 적분한다.
    private const float Step = 1f / 120f;

    // 줄 길이가 0에 가까워지면 각가속도가 발산하므로 최소 길이를 둔다.
    private const float MinLength = 0.05f;

    [Header("줄")]
    [Tooltip("줄이 매달린 지점. 비워두면 시작 위치에서 줄 길이만큼 위를 앵커로 잡는다")]
    [SerializeField] private Transform anchor;

    [Tooltip("다 늘어났을 때의 줄 길이")]
    [SerializeField] private float ropeLength = 3f;

    [Tooltip("떨어지기 시작할 때의 줄 길이. 짧을수록 더 높은 곳에서 떨어진다")]
    [SerializeField] private float startRopeLength = 0.4f;

    [Tooltip("처음 기울어져 있는 각도(도). 0이 아니면 떨어지면서 같이 흔들린다")]
    [SerializeField] private float startAngle = 12f;

    [Tooltip("시작하고 이 시간이 지난 뒤에 떨어진다")]
    [SerializeField] private float dropDelay;

    [Tooltip("켜두면 Start 에서 바로 떨어진다. 끄면 Drop() 을 직접 불러야 한다")]
    [SerializeField] private bool dropOnStart = true;

    [Header("물리")]
    [Tooltip("중력. 클수록 빠르게 떨어진다")]
    [SerializeField] private float gravity = 30f;

    [Tooltip("줄의 탄성. 클수록 덜 늘어나고 짧고 날카롭게 튕긴다")]
    [SerializeField] private float stiffness = 220f;

    [Tooltip("위아래 반동이 잦아드는 정도. 0이면 계속 튕긴다")]
    [SerializeField] private float bounceDamping = 4f;

    [Tooltip("좌우 흔들림이 잦아드는 정도")]
    [SerializeField] private float swingDamping = 0.7f;

    [Tooltip("좌우로 벌어질 수 있는 최대 각도(도)")]
    [SerializeField] private float maxAngle = 70f;

    [Header("마우스")]
    [SerializeField] private bool reactToMouse = true;

    [Tooltip("비워두면 Camera.main 을 쓴다")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Collider2D 가 없을 때 쓰는 판정 반지름")]
    [SerializeField] private float mouseRadius = 0.8f;

    [Tooltip("마우스가 움직이는 속도를 얼마나 따라가는지. 클수록 마우스에 딱 붙어 끌려온다")]
    [SerializeField] private float mouseFollow = 10f;

    [Tooltip("마우스를 가만히 대고만 있어도 밀려나는 힘")]
    [SerializeField] private float mouseHoverPush = 8f;

    [Header("보기")]
    [Tooltip("줄이 기울어진 만큼 그림도 같이 기울인다")]
    [SerializeField] private bool tiltWithRope = true;

    [Tooltip("줄을 그릴 LineRenderer. 없어도 동작한다")]
    [SerializeField] private LineRenderer rope;

    private Collider2D hitCollider;
    private Quaternion restRotation;
    private Vector3 fixedAnchor;
    private float depth;

    // 앵커에서 똑바로 아래를 0으로 잰 각도(라디안)와 줄 길이. 이 둘이 상태의 전부다.
    private float angle;
    private float angleVelocity;
    private float length;
    private float lengthVelocity;

    private float accumulator;
    private float delayLeft;
    private bool dropping;

    private Vector2 lastMouseWorld;
    private bool hasLastMouse;

    /// <summary>줄이 매달린 지점(월드 좌표).</summary>
    public Vector3 AnchorPosition => anchor != null ? anchor.position : fixedAnchor;

    /// <summary>앵커에서 그림 쪽으로 향하는 줄 방향.</summary>
    public Vector2 RopeDirection => new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));

    /// <summary>떨어지는 중이거나 흔들리는 중이면 true.</summary>
    public bool IsDropping => dropping;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        hitCollider = GetComponent<Collider2D>();
        restRotation = transform.rotation;
        depth = transform.position.z;

        // 앵커를 따로 안 줬으면 배치해 둔 자리를 "다 내려온 자리"로 보고 그 위를 앵커로 잡는다.
        fixedAnchor = transform.position + Vector3.up * ropeLength;

        ResetPose();
    }

    private void Start()
    {
        if (dropOnStart)
            Drop();
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (!dropping)
        {
            if (delayLeft <= 0f)
                return;

            delayLeft -= dt;
            if (delayLeft > 0f)
                return;

            dropping = true;
        }

        if (reactToMouse)
            HandleMouse(dt);

        // 프레임이 크게 튀어도 한 번에 몰아서 계산하지 않도록 상한을 둔다.
        accumulator += Mathf.Min(dt, 0.1f);
        while (accumulator >= Step)
        {
            Simulate(Step);
            accumulator -= Step;
        }

        Apply();
    }

    /// <summary>줄을 풀어 떨어뜨린다. <see cref="dropDelay"/> 만큼 기다린 뒤 시작한다.</summary>
    public void Drop()
    {
        ResetPose();

        if (dropDelay > 0f)
        {
            delayLeft = dropDelay;
            return;
        }

        dropping = true;
    }

    /// <summary>떨어지기 전 자세로 되돌린다.</summary>
    public void ResetPose()
    {
        angle = startAngle * Mathf.Deg2Rad;
        angleVelocity = 0f;
        length = Mathf.Max(startRopeLength, MinLength);
        lengthVelocity = 0f;

        accumulator = 0f;
        delayLeft = 0f;
        dropping = false;
        hasLastMouse = false;

        Apply();
    }

    /// <summary>바깥에서 옆으로 툭 밀고 싶을 때. 양수면 오른쪽, 음수면 왼쪽.</summary>
    public void Push(float strength)
    {
        angleVelocity += strength / Mathf.Max(length, MinLength);
    }

    /// <summary>
    /// 런타임에 생성해서 붙일 때 쓰는 초기화. Awake 뒤에 부르면 된다.
    /// 떨어뜨리는 시점은 부른 쪽이 <see cref="Drop"/> 으로 정한다.
    /// </summary>
    public void Configure(Vector3 anchorPosition, float rope, float angleDegrees, float delay)
    {
        anchor = null;
        fixedAnchor = anchorPosition;
        ropeLength = rope;
        startAngle = angleDegrees;
        dropDelay = delay;
        dropOnStart = false;

        ResetPose();
    }

    /// <summary>줄을 그릴 LineRenderer 를 지정한다.</summary>
    public void SetRopeRenderer(LineRenderer renderer)
    {
        rope = renderer;
    }

    /// <summary>마우스 반응을 켜고 끈다.</summary>
    public void SetReactToMouse(bool value)
    {
        reactToMouse = value;
    }

    /// <summary>마우스 위치를 읽을 카메라를 지정한다.</summary>
    public void SetCamera(Camera camera)
    {
        targetCamera = camera;
    }

    /// <summary>스프링 진자 한 스텝. 극좌표(줄 길이 + 각도)로 푼다.</summary>
    private void Simulate(float dt)
    {
        float safeLength = Mathf.Max(length, MinLength);

        // 줄 방향: 중력의 세로 성분 + 원심력 - 스프링 복원력 - 감쇠.
        // 늘어난 줄이 되돌아오면서 위아래로 튕기는 반동이 여기서 나온다.
        float lengthAcceleration =
            gravity * Mathf.Cos(angle)
            + safeLength * angleVelocity * angleVelocity
            - stiffness * (length - ropeLength)
            - bounceDamping * lengthVelocity;

        // 접선 방향: 중력의 가로 성분 - 코리올리 항 - 감쇠.
        float angleAcceleration =
            (-gravity * Mathf.Sin(angle) - 2f * lengthVelocity * angleVelocity) / safeLength
            - swingDamping * angleVelocity;

        lengthVelocity += lengthAcceleration * dt;
        angleVelocity += angleAcceleration * dt;

        length += lengthVelocity * dt;
        angle += angleVelocity * dt;

        if (length < MinLength)
        {
            length = MinLength;
            if (lengthVelocity < 0f)
                lengthVelocity = 0f;
        }

        float limit = maxAngle * Mathf.Deg2Rad;
        if (Mathf.Abs(angle) > limit)
        {
            angle = Mathf.Sign(angle) * limit;
            angleVelocity = 0f;
        }
    }

    /// <summary>계산한 각도와 줄 길이를 실제 트랜스폼과 줄 그림에 반영한다.</summary>
    private void Apply()
    {
        Vector3 anchorPosition = AnchorPosition;
        Vector3 position = anchorPosition + (Vector3)(RopeDirection * length);
        position.z = depth;
        transform.position = position;

        if (tiltWithRope)
            transform.rotation = restRotation * Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);

        if (rope == null)
            return;

        if (rope.positionCount != 2)
            rope.positionCount = 2;

        rope.useWorldSpace = true;
        rope.SetPosition(0, anchorPosition);
        rope.SetPosition(1, position);
    }

    /// <summary>마우스가 그림에 닿아 있으면 마우스가 움직인 방향으로 끌어당긴다.</summary>
    private void HandleMouse(float dt)
    {
        if (targetCamera == null || dt <= 0f)
            return;

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        Vector3 screenPosition = mouse.position.ReadValue();
        screenPosition.z = Mathf.Abs(depth - targetCamera.transform.position.z);
        Vector2 world = targetCamera.ScreenToWorldPoint(screenPosition);

        Vector2 mouseVelocity = hasLastMouse ? (world - lastMouseWorld) / dt : Vector2.zero;
        lastMouseWorld = world;
        hasLastMouse = true;

        if (!IsTouching(world))
            return;

        float safeLength = Mathf.Max(length, MinLength);
        Vector2 direction = RopeDirection;
        Vector2 tangent = new Vector2(-direction.y, direction.x);

        // 마우스가 움직인 속도를 줄 방향/접선 방향으로 나눠서 그만큼 따라가게 한다.
        float follow = Mathf.Clamp01(mouseFollow * dt);
        angleVelocity = Mathf.Lerp(angleVelocity, Vector2.Dot(mouseVelocity, tangent) / safeLength, follow);
        lengthVelocity = Mathf.Lerp(lengthVelocity, Vector2.Dot(mouseVelocity, direction), follow);

        // 마우스를 가만히 대고만 있어도 반대쪽으로 슬슬 밀린다.
        Vector2 away = (Vector2)transform.position - world;
        if (away.sqrMagnitude > 0.0001f)
            angleVelocity += Vector2.Dot(away.normalized, tangent) * mouseHoverPush * dt / safeLength;
    }

    private bool IsTouching(Vector2 world)
    {
        if (hitCollider != null)
            return hitCollider.OverlapPoint(world);

        return ((Vector2)transform.position - world).sqrMagnitude <= mouseRadius * mouseRadius;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 anchorPosition = Application.isPlaying
            ? AnchorPosition
            : (anchor != null ? anchor.position : transform.position + Vector3.up * ropeLength);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(anchorPosition, transform.position);
        Gizmos.DrawWireSphere(anchorPosition, 0.1f);

        if (hitCollider == null)
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, mouseRadius);
        }
    }
}
