using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 밑에서 튀어나왔다가 다시 내려가는 오브젝트. (등장하는 쪽)
/// 에디슨처럼 성공 순간에만 얼굴을 내미는 연출에 쓴다.
/// 오브젝트는 화면 밖(또는 가려지는 자리)에 숨겨 두고, 거기서 <c>riseDistance</c> 만큼 올라온다.
/// </summary>
public class PopUpActor : MonoBehaviour
{
    [Header("움직임")]
    [Tooltip("불린 뒤 올라오기까지 기다리는 시간(초). 여러 개를 조금씩 어긋나게 튀어나오게 할 때 쓴다")]
    [SerializeField] private float delay = 0f;

    [Tooltip("숨은 자리에서 위로 얼마나 올라올지")]
    [SerializeField] private float riseDistance = 3f;

    [Tooltip("올라오는 시간")]
    [SerializeField] private float riseTime = 0.22f;

    [Tooltip("올라온 채로 머무는 시간")]
    [SerializeField] private float holdTime = 0.8f;

    [Tooltip("다시 내려가는 시간")]
    [SerializeField] private float sinkTime = 0.3f;

    [Tooltip("올라올 때의 곡선. 1을 넘겼다 돌아오게 만들면 통통 튀며 튀어나온다")]
    [SerializeField]
    private AnimationCurve riseEase = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.7f, 1.12f),
        new Keyframe(1f, 1f));

    [Tooltip("내려갈 때의 곡선")]
    [SerializeField]
    private AnimationCurve sinkEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("이벤트")]
    [Tooltip("다 올라와서 멈춘 순간. 대사나 효과음을 연결한다")]
    public UnityEvent onArrived;

    private Vector3 restPosition;
    private bool cached;
    private Coroutine running;

    private void Awake()
    {
        CacheRest();
    }

    /// <summary>
    /// 숨어 있는 자리를 기억해 둔다.
    /// Awake 순서가 보장되지 않아 값을 쓰기 전에 매번 부른다.
    /// </summary>
    private void CacheRest()
    {
        if (cached)
            return;

        cached = true;
        restPosition = transform.localPosition;
    }

    /// <summary>튀어나왔다가 잠시 머문 뒤 다시 내려간다.</summary>
    public void Pop()
    {
        CacheRest();

        if (!gameObject.activeInHierarchy)
            return;

        if (running != null)
            StopCoroutine(running);

        running = StartCoroutine(PopRoutine());
    }

    /// <summary>숨은 자리로 되돌린다. (재시작용)</summary>
    public void ResetPose()
    {
        CacheRest();

        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        transform.localPosition = restPosition;
    }

    private IEnumerator PopRoutine()
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        Vector3 topPosition = restPosition + Vector3.up * riseDistance;

        yield return MoveRoutine(restPosition, topPosition, riseTime, riseEase);

        onArrived.Invoke();

        if (holdTime > 0f)
            yield return new WaitForSeconds(holdTime);

        yield return MoveRoutine(topPosition, restPosition, sinkTime, sinkEase);

        running = null;
    }

    private IEnumerator MoveRoutine(Vector3 from, Vector3 to, float duration, AnimationCurve ease)
    {
        if (duration > 0f)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                // 곡선이 1을 넘어가는 구간을 살리려면 Unclamped 로 보간해야 한다.
                float k = ease != null ? ease.Evaluate(t / duration) : t / duration;
                transform.localPosition = Vector3.LerpUnclamped(from, to, k);
                yield return null;
            }
        }

        transform.localPosition = to;
    }
}
