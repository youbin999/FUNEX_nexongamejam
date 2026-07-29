using UnityEngine;

/// <summary>
/// 방의 어둠을 걷어내는 연출. (밝아지는 쪽)
/// 스위치를 찾은 순간 <see cref="TurnOn"/> 을 불러서 암흑 레이어들을 투명하게 만든다.
/// <see cref="LightMiniGame.onClear"/> 에 연결해서 쓴다.
/// </summary>
public class RoomLighting : MonoBehaviour
{
    [Header("걷어낼 어둠")]
    [Tooltip("화면을 덮는 검은 스프라이트들. 빛 구멍용 레이어까지 전부 넣어야 빈틈없이 밝아진다")]
    [SerializeField] private SpriteRenderer[] darkness;

    [Header("밝아지기")]
    [Tooltip("어둠이 걷히는 시간(초). 0이면 즉시 확 밝아진다")]
    [SerializeField] private float brightenTime = 0.25f;

    [Tooltip("어둠이 걷히는 곡선. 앞이 가파르면 '확' 켜지는 느낌이 난다")]
    [SerializeField]
    private AnimationCurve brightenEase = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(1f, 1f));

    private Color[] restColors;
    private bool cached;

    // 1 이면 원래 어둠 그대로, 0 이면 완전히 걷힌 상태.
    private float darkAmount = 1f;
    private bool turningOn;
    private float elapsed;

    /// <summary>방 조명의 기준 밝기를 기억해 둔다.</summary>
    private void Awake()
    {
        CacheRest();
    }

    /// <summary>
    /// 어둠 레이어들의 원래 색을 기억해 둔다.
    /// Awake 순서가 보장되지 않아 값을 쓰기 전에 매번 부른다.
    /// </summary>
    private void CacheRest()
    {
        if (cached)
            return;

        cached = true;

        if (darkness == null)
        {
            restColors = new Color[0];
            return;
        }

        restColors = new Color[darkness.Length];
        for (int i = 0; i < darkness.Length; i++)
        {
            if (darkness[i] != null)
                restColors[i] = darkness[i].color;
        }
    }

    /// <summary>
    /// 불을 켠다. 어둠이 걷히기 시작한다.
    /// 이 컴포넌트는 미니게임 인스턴스 밖(항상 켜져 있는 오브젝트)에 둬야 한다.
    /// 클리어 통지 직후 <see cref="MiniGamePlayer"/> 가 인스턴스를 비활성화하는데,
    /// 인스턴스 안에 있으면 Update 가 멈춰서 어둠이 걷히다 만다.
    /// </summary>
    public void TurnOn()
    {
        CacheRest();
        elapsed = 0f;

        // 시간이 0이면 Update 를 기다리지 않고 여기서 끝낸다.
        if (brightenTime <= 0f)
        {
            turningOn = false;
            darkAmount = 0f;
            Apply();
            return;
        }

        turningOn = true;
    }

    /// <summary>다시 캄캄하게 되돌린다. (재시작용)</summary>
    public void ResetPose()
    {
        CacheRest();
        turningOn = false;
        elapsed = 0f;
        darkAmount = 1f;
        Apply();
    }

    /// <summary>불이 켜지는 동안 어둠을 서서히 걷어낸다.</summary>
    private void Update()
    {
        if (!turningOn)
            return;

        if (brightenTime <= 0f)
        {
            darkAmount = 0f;
            turningOn = false;
        }
        else
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / brightenTime);
            darkAmount = 1f - brightenEase.Evaluate(t);

            if (t >= 1f)
                turningOn = false;
        }

        Apply();
    }

    /// <summary>현재 어둠 정도를 어둠 레이어들의 투명도에 반영한다.</summary>
    private void Apply()
    {
        if (darkness == null)
            return;

        for (int i = 0; i < darkness.Length; i++)
        {
            if (darkness[i] == null)
                continue;

            Color color = restColors[i];
            color.a = restColors[i].a * darkAmount;
            darkness[i].color = color;
        }
    }
}
