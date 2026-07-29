using System.Collections;
using UnityEngine;

/// <summary>
/// 우주 쓰레기 한 조각. 평소에는 천천히 둥둥 떠다니고, 잡히면 포인터를 따라오며,
/// 버려지면 빙빙 돌면서 작아지다가 사라진다.
/// <see cref="TrashMiniGame"/> 이 배열로 들고 있으며, 상태는 스스로 관리한다.
/// </summary>
public class SpaceTrash : MonoBehaviour
{
    private enum State
    {
        Floating,
        Held,
        Disposed,
    }

    [Header("떠다니기")]
    [Tooltip("둥둥 떠다니는 속도(월드 유닛/초)")]
    [SerializeField] private float driftSpeed = 0.6f;

    [Tooltip("표류 방향이 서서히 바뀌는 정도(도/초). 0이면 직선으로만 간다")]
    [SerializeField] private float driftTurnSpeed = 25f;

    [Tooltip("떠다니는 동안 천천히 도는 속도(도/초)")]
    [SerializeField] private float spinSpeed = 12f;

    [Tooltip("화면 경계에서 이만큼 안쪽까지만 떠다닌다(월드 유닛)")]
    [SerializeField] private float screenMargin = 0.4f;

    [Header("버려질 때")]
    [Tooltip("빙빙 돌며 작아져 사라지는 데 걸리는 시간(초)")]
    [SerializeField] private float disposeDuration = 0.35f;

    [Tooltip("사라질 때 도는 속도(도/초)")]
    [SerializeField] private float disposeSpinSpeed = 720f;

    private State state = State.Floating;

    // 처음 배치된 자세. 재생을 반복해도 여기로 되돌아온다.
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 startScale;

    private Vector2 driftDirection;
    private Camera cachedCamera;
    private SpriteRenderer[] renderers;
    private Coroutine disposeRoutine;

    /// <summary>이미 버려졌는지 여부. 본체가 클리어 판정에 쓴다.</summary>
    public bool IsDisposed => state == State.Disposed;

    /// <summary>지금 잡혀 있는지 여부.</summary>
    public bool IsHeld => state == State.Held;

    /// <summary>처음 자세와 렌더러를 잡아 두고 표류 방향을 무작위로 정한다.</summary>
    private void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        startScale = transform.localScale;

        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        PickRandomDriftDirection();
    }

    /// <summary>표류 방향을 무작위로 새로 정한다.</summary>
    private void PickRandomDriftDirection()
    {
        float angle = Random.value * Mathf.PI * 2f;
        driftDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    /// <summary>본체가 카메라를 넘겨준다. 화면 경계에서 튕기는 데 쓴다.</summary>
    public void SetCamera(Camera camera)
    {
        cachedCamera = camera;
    }

    /// <summary>처음 자세·크기로 되돌리고 다시 떠다니게 한다. 멱등.</summary>
    public void ResetState()
    {
        if (disposeRoutine != null)
        {
            StopCoroutine(disposeRoutine);
            disposeRoutine = null;
        }

        transform.position = startPosition;
        transform.rotation = startRotation;
        transform.localScale = startScale;
        gameObject.SetActive(true);

        PickRandomDriftDirection();
        state = State.Floating;
    }

    /// <summary>잡는다. 잡혀 있는 동안은 표류하지 않는다.</summary>
    public void Grab()
    {
        if (state == State.Disposed)
            return;

        state = State.Held;
    }

    /// <summary>놓는다. 버려지지 않았다면 놓은 그 자리에서 다시 떠다니기 시작한다.</summary>
    public void Release()
    {
        if (state != State.Held)
            return;

        PickRandomDriftDirection();
        state = State.Floating;
    }

    /// <summary>버린다. 목표 지점으로 빨려들며 빙빙 돌고 작아지다가 사라진다.</summary>
    /// <param name="target">빨려 들어갈 지점(보통 Point 의 중심).</param>
    public void Dispose(Vector3 target)
    {
        SuckInto(target, disposeDuration);
    }

    /// <summary>
    /// 실패했을 때 화면 밖으로 슉 날아가게 한다. 잡혀 있든 떠다니든 상관없이 즉시 날아간다.
    /// </summary>
    /// <param name="direction">날아갈 방향.</param>
    /// <param name="speed">날아가는 속도(월드 유닛/초).</param>
    /// <param name="duration">날아가는 시간(초).</param>
    public void FlyAway(Vector2 direction, float speed, float duration)
    {
        if (state == State.Disposed)
            return;

        state = State.Disposed;
        disposeRoutine = StartCoroutine(FlyAwayRoutine(direction, speed, duration));
    }

    /// <summary>지정한 방향으로 날아가며 화면 밖으로 빠져나간다.</summary>
    private IEnumerator FlyAwayRoutine(Vector2 direction, float speed, float duration)
    {
        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;

        float time = 0f;
        while (time < duration)
        {
            float dt = Time.deltaTime;
            time += dt;

            transform.position += (Vector3)(dir * (speed * dt));

            yield return null;
        }

        gameObject.SetActive(false);
        disposeRoutine = null;
    }

    /// <summary>
    /// 목표 지점으로 빨려 들어가게 한다. 버릴 때와 실패 연출에서 같이 쓴다.
    /// </summary>
    /// <param name="target">빨려 들어갈 지점.</param>
    /// <param name="duration">빨려 들어가는 데 걸리는 시간(초).</param>
    public void SuckInto(Vector3 target, float duration)
    {
        if (state == State.Disposed)
            return;

        state = State.Disposed;
        disposeRoutine = StartCoroutine(DisposeRoutine(target, duration));
    }

    /// <summary>목표 지점으로 빨려 들어가며 작아져 사라진다.</summary>
    private IEnumerator DisposeRoutine(Vector3 target, float duration)
    {
        Vector3 from = transform.position;
        Vector3 fromScale = transform.localScale;
        target.z = from.z;

        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float k = duration > 0f ? Mathf.Clamp01(time / duration) : 1f;

            // 빨려 들어가면서 빙빙 돌고 점점 작아진다.
            transform.position = Vector3.Lerp(from, target, k * k);
            transform.localScale = Vector3.Lerp(fromScale, Vector3.zero, k);
            transform.Rotate(0f, 0f, disposeSpinSpeed * Time.deltaTime);

            yield return null;
        }

        transform.localScale = Vector3.zero;
        gameObject.SetActive(false);
        disposeRoutine = null;
    }

    /// <summary>떠다니는 처리. 잡혀 있거나 버려졌으면 아무것도 하지 않는다.</summary>
    public void Drift(float deltaTime)
    {
        if (state != State.Floating)
            return;

        // 방향이 서서히 틀어져 둥둥 떠다니는 느낌을 준다.
        if (!Mathf.Approximately(driftTurnSpeed, 0f))
        {
            float turn = Mathf.Sin(Time.time * 0.7f + startPosition.x) * driftTurnSpeed * deltaTime;
            driftDirection = (Quaternion.Euler(0f, 0f, turn) * driftDirection).normalized;
        }

        transform.position += (Vector3)(driftDirection * (driftSpeed * deltaTime));

        if (!Mathf.Approximately(spinSpeed, 0f))
            transform.Rotate(0f, 0f, spinSpeed * deltaTime);

        BounceOffScreenEdges();
    }

    /// <summary>화면 밖으로 나가지 않도록 경계에서 방향을 반사시킨다.</summary>
    private void BounceOffScreenEdges()
    {
        Camera cam = cachedCamera != null ? cachedCamera : Camera.main;
        if (cam == null)
            return;

        float depth = Mathf.Abs(transform.position.z - cam.transform.position.z);
        Vector3 min = cam.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 max = cam.ViewportToWorldPoint(new Vector3(1f, 1f, depth));

        Vector3 position = transform.position;
        Vector2 extents = GetExtents();

        float minX = min.x + screenMargin + extents.x;
        float maxX = max.x - screenMargin - extents.x;
        float minY = min.y + screenMargin + extents.y;
        float maxY = max.y - screenMargin - extents.y;

        if (position.x < minX && driftDirection.x < 0f)
        {
            driftDirection.x = -driftDirection.x;
            position.x = minX;
        }
        else if (position.x > maxX && driftDirection.x > 0f)
        {
            driftDirection.x = -driftDirection.x;
            position.x = maxX;
        }

        if (position.y < minY && driftDirection.y < 0f)
        {
            driftDirection.y = -driftDirection.y;
            position.y = minY;
        }
        else if (position.y > maxY && driftDirection.y > 0f)
        {
            driftDirection.y = -driftDirection.y;
            position.y = maxY;
        }

        transform.position = position;
    }

    /// <summary>이 쓰레기의 월드 영역(AABB)을 구한다. 잡기·버리기 판정에 쓴다.</summary>
    public bool TryGetBounds(out Bounds bounds)
    {
        bounds = default;

        if (renderers == null)
            return false;

        bool found = false;
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    /// <summary>영역의 반크기. 화면 경계 반사에 쓴다.</summary>
    private Vector2 GetExtents()
    {
        return TryGetBounds(out Bounds bounds)
            ? new Vector2(bounds.extents.x, bounds.extents.y)
            : Vector2.zero;
    }

    /// <summary>월드 좌표가 이 쓰레기 위에 있는지. 콜라이더 없이 스프라이트 영역으로 판정한다.</summary>
    public bool ContainsPoint(Vector2 worldPoint)
    {
        if (state == State.Disposed)
            return false;

        if (!TryGetBounds(out Bounds bounds))
            return false;

        return worldPoint.x >= bounds.min.x && worldPoint.x <= bounds.max.x
            && worldPoint.y >= bounds.min.y && worldPoint.y <= bounds.max.y;
    }

    /// <summary>정렬 순서. 겹쳐 있을 때 위에 있는 것부터 잡기 위해 쓴다.</summary>
    public int SortingOrder => renderers != null && renderers.Length > 0 && renderers[0] != null
        ? renderers[0].sortingOrder
        : 0;
}
