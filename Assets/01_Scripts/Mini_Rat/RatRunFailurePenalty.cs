using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 쥐 잡기 실패 패널티. 놓친 쥐가 판이 끝날 때까지 화면을 가로지른다.
/// 매번 높이(Y)와 방향(좌→우 / 우→좌)을 새로 뽑아 화면 밖에서 밖으로 빠르게 달리고,
/// 달릴 때마다 <see cref="onDashStarted"/> 로 소리를 낸다.
///
/// <see cref="Apply"/> 는 첫 달리기 한 번만 보여주고 끝난다(계약 2.2) — 그 뒤로는
/// 코루틴이 알아서 반복하므로 다음 미니게임이 곧바로 진행된다.
/// 인스턴스는 스스로 지우지 않는다. 새 판을 시작할 때 <c>FailurePenaltyController</c> 가 정리한다.
/// </summary>
public sealed class RatRunFailurePenalty : FailurePenalty
{
    [Header("쥐 이미지")]
    [Tooltip("화면을 가로지를 쥐 Image. 앵커는 캔버스 중앙(0.5, 0.5)으로 둔다")]
    [SerializeField] private Image ratImage;

    [Tooltip("쥐 크기(px, 1920x1080 기준)")]
    [SerializeField] private Vector2 ratSize = new Vector2(240f, 150f);

    [Tooltip("스프라이트가 오른쪽을 보고 있다고 보고, 왼쪽으로 달릴 때 좌우를 뒤집는다")]
    [SerializeField] private bool flipSpriteByDirection = true;

    [Header("달리기")]
    [Tooltip("달리는 속도 범위(px/초, 1920x1080 기준). 매번 이 사이에서 뽑는다")]
    [SerializeField] private Vector2 speedRange = new Vector2(1500f, 2300f);

    [Tooltip("한 번 지나간 뒤 다음까지 기다리는 시간 범위(초)")]
    [SerializeField] private Vector2 intervalRange = new Vector2(2.5f, 6f);

    [Tooltip("달리는 높이 범위(캔버스 중앙 기준 비율). -0.5 가 화면 맨 아래, 0.5 가 맨 위")]
    [SerializeField] private Vector2 heightRatioRange = new Vector2(-0.38f, 0.38f);

    [Header("이벤트")]
    [Tooltip("한 번 달리기 시작할 때마다 발화. 쥐 달리는 소리를 연결한다")]
    public UnityEvent onDashStarted;

    private RectTransform ratRect;
    private RectTransform canvasRect;
    private Vector3 ratBaseScale = Vector3.one;

    /// <summary>쥐가 화면을 한 번 가로지르는 것을 보여준다. 이후 반복 질주는 판이 끝날 때까지 알아서 돈다.</summary>
    public override IEnumerator Apply()
    {
        if (!Prepare())
            yield break;

        // 첫 한 번은 실패 연출로 직접 보여준다. 이게 끝나야 다음 미니게임이 시작된다.
        yield return DashRoutine();

        // 이후로는 판이 끝날 때까지 알아서 반복한다.
        StartCoroutine(LoopRoutine());
    }

    /// <summary>이미지와 캔버스를 준비한다. 띄울 스프라이트가 없으면 false.</summary>
    private bool Prepare()
    {
        if (ratImage == null || ratImage.sprite == null)
        {
            // 스프라이트가 비면 기본 흰 사각형이 화면을 가로지른다. 아예 감춘다(계약 4.1).
            if (ratImage != null)
                ratImage.gameObject.SetActive(false);

            Debug.LogWarning($"[{name}] Rat Image 가 비어 있거나 스프라이트가 없어 패널티가 표시되지 않는다. " +
                "패널티 프리팹의 Rat Image 칸에 쥐 Image 를 넣는다.", this);
            return false;
        }

        // 이 컴포넌트가 Canvas 에 붙어 있든 그 부모에 붙어 있든 상관없도록,
        // 기준 캔버스는 쥐 이미지가 실제로 그려지는 캔버스에서 얻는다.
        Canvas canvas = ratImage.canvas;
        canvasRect = canvas != null ? canvas.transform as RectTransform : transform as RectTransform;

        if (canvasRect == null)
        {
            Debug.LogWarning($"[{name}] 쥐 Image 가 Canvas 아래에 있지 않다. " +
                "Screen Space Overlay Canvas 아래에 두어야 화면을 가로지를 수 있다.", this);
            return false;
        }

        ratImage.raycastTarget = false;
        ratImage.preserveAspect = true;

        ratRect = ratImage.rectTransform;
        ratRect.sizeDelta = ratSize;
        ratBaseScale = ratRect.localScale;

        ratImage.gameObject.SetActive(false);
        return true;
    }

    /// <summary>판이 끝날 때까지 쥐가 화면을 가로지르는 것을 되풀이한다.</summary>
    private IEnumerator LoopRoutine()
    {
        while (true)
        {
            float wait = Random.Range(
                Mathf.Min(intervalRange.x, intervalRange.y),
                Mathf.Max(intervalRange.x, intervalRange.y));

            yield return new WaitForSeconds(Mathf.Max(0f, wait));
            yield return DashRoutine();
        }
    }

    /// <summary>화면 밖에서 반대편 밖까지 한 번 달린다.</summary>
    private IEnumerator DashRoutine()
    {
        if (ratRect == null || canvasRect == null)
            yield break;

        Vector2 canvasSize = canvasRect.rect.size;
        float exitX = canvasSize.x * 0.5f + ratSize.x;
        bool toRight = Random.value < 0.5f;

        float height = canvasSize.y * Random.Range(
            Mathf.Min(heightRatioRange.x, heightRatioRange.y),
            Mathf.Max(heightRatioRange.x, heightRatioRange.y));

        float speed = Mathf.Abs(Random.Range(speedRange.x, speedRange.y));
        if (speed <= 0f)
            speed = 1500f;

        float x = toRight ? -exitX : exitX;
        ratRect.anchoredPosition = new Vector2(x, height);

        if (flipSpriteByDirection)
        {
            Vector3 scale = ratBaseScale;
            scale.x = Mathf.Abs(scale.x) * (toRight ? 1f : -1f);
            ratRect.localScale = scale;
        }

        ratImage.gameObject.SetActive(true);
        onDashStarted.Invoke();

        float direction = toRight ? 1f : -1f;
        while (toRight ? x < exitX : x > -exitX)
        {
            x += direction * speed * Time.deltaTime;
            ratRect.anchoredPosition = new Vector2(x, height);
            yield return null;
        }

        ratImage.gameObject.SetActive(false);
    }
}
