using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그림 몇 장을 화면 위쪽 줄에 매달아 놓고, 게임이 시작되면 차례로 떨어뜨린다.
/// 스프라이트만 넣어주면 오브젝트 생성부터 줄 그리기, 클릭 판정용 콜라이더까지 알아서 만든다.
/// 실제 떨어지고 흔들리는 계산은 <see cref="HangingSprite"/> 가 한다.
///
/// 타이틀 씬의 빈 오브젝트에 붙이고 Entries 에 스프라이트를 채워 넣으면 된다.
/// </summary>
public class HangingSpriteBoard : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        [Tooltip("매달 그림")]
        public Sprite sprite;

        [Tooltip("화면 가로 위치. 0이 왼쪽 끝, 1이 오른쪽 끝")]
        [Range(0f, 1f)] public float screenX = 0.5f;

        [Tooltip("줄 길이. 길수록 아래까지 내려온다")]
        public float ropeLength = 3f;

        [Tooltip("그림 크기")]
        public float scale = 1f;

        [Tooltip("처음 기울어진 각도(도). 부호를 엇갈리게 주면 서로 반대로 흔들린다")]
        public float startAngle = 12f;

        [Tooltip("떨어지기까지 추가로 기다리는 시간. Delay Step 위에 더해진다")]
        public float extraDelay;

        [Tooltip("겹칠 때 앞뒤 순서")]
        public int sortingOrder;
    }

    [Header("매달 그림")]
    [SerializeField] private List<Entry> entries = new List<Entry>();

    [Header("배치")]
    [Tooltip("비워두면 Camera.main 을 쓴다")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("줄을 매다는 높이. 1이 화면 위쪽 끝이라 1보다 크면 화면 밖에서 시작한다")]
    [SerializeField] private float anchorViewportY = 1.05f;

    [Tooltip("생성한 그림의 z 값")]
    [SerializeField] private float depth;

    [Tooltip("스프라이트가 들어갈 정렬 레이어")]
    [SerializeField] private string sortingLayer = "Default";

    [Tooltip("앞 그림이 떨어지고 다음 그림이 떨어지기까지의 간격")]
    [SerializeField] private float delayStep = 0.12f;

    [Tooltip("켜두면 Start 에서 바로 떨어진다. 끄면 Play() 를 직접 부른다")]
    [SerializeField] private bool playOnStart = true;

    [Header("줄")]
    [SerializeField] private bool drawRope = true;

    [Tooltip("줄 재질. 비워두면 Sprites/Default 로 만들어 쓴다")]
    [SerializeField] private Material ropeMaterial;

    [SerializeField] private Color ropeColor = new Color(0.15f, 0.12f, 0.1f);
    [SerializeField] private float ropeWidth = 0.05f;

    [Header("마우스")]
    [Tooltip("마우스가 그림에 닿으면 밀리게 할지")]
    [SerializeField] private bool reactToMouse = true;

    private readonly List<HangingSprite> spawned = new List<HangingSprite>();
    private Material runtimeRopeMaterial;

    /// <summary>생성된 그림들. 순서는 Entries 와 같다.</summary>
    public IReadOnlyList<HangingSprite> Spawned => spawned;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        Build();
    }

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    private void OnDestroy()
    {
        if (runtimeRopeMaterial != null)
            Destroy(runtimeRopeMaterial);
    }

    /// <summary>매달린 그림들을 순서대로 떨어뜨린다.</summary>
    public void Play()
    {
        foreach (HangingSprite hanging in spawned)
            hanging.Drop();
    }

    /// <summary>전부 떨어지기 전 자세로 되돌린다.</summary>
    public void ResetAll()
    {
        foreach (HangingSprite hanging in spawned)
            hanging.ResetPose();
    }

    /// <summary>Entries 를 보고 그림 오브젝트를 만든다.</summary>
    private void Build()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry == null || entry.sprite == null)
                continue;

            HangingSprite hanging = Create(entry, i);
            if (hanging != null)
                spawned.Add(hanging);
        }
    }

    private HangingSprite Create(Entry entry, int index)
    {
        if (targetCamera == null)
        {
            Debug.LogWarning($"{name}: 카메라를 못 찾아서 그림을 매달 수 없다", this);
            return null;
        }

        var go = new GameObject($"Hanging_{entry.sprite.name}");
        go.transform.SetParent(transform, false);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = entry.sprite;
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = entry.sortingOrder;

        float scale = Mathf.Max(entry.scale, 0.01f);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        // 마우스가 그림에 정확히 닿았는지 보려고 스프라이트 크기만큼 콜라이더를 깔아둔다.
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = entry.sprite.bounds.size;
        box.offset = entry.sprite.bounds.center;

        Vector3 anchorPosition = AnchorAt(entry.screenX);
        go.transform.position = anchorPosition + Vector3.down * entry.ropeLength;

        var hanging = go.AddComponent<HangingSprite>();
        hanging.Configure(anchorPosition, entry.ropeLength, entry.startAngle, index * delayStep + entry.extraDelay);
        hanging.SetReactToMouse(reactToMouse);
        hanging.SetCamera(targetCamera);

        if (drawRope)
            hanging.SetRopeRenderer(CreateRope(go.transform, entry.sortingOrder));

        return hanging;
    }

    /// <summary>화면 가로 위치를 앵커 월드 좌표로 바꾼다.</summary>
    private Vector3 AnchorAt(float screenX)
    {
        float distance = Mathf.Abs(depth - targetCamera.transform.position.z);
        Vector3 point = targetCamera.ViewportToWorldPoint(new Vector3(screenX, anchorViewportY, distance));
        point.z = depth;
        return point;
    }

    private LineRenderer CreateRope(Transform owner, int sortingOrder)
    {
        var go = new GameObject("Rope");
        // 그림에 딸려 움직이면 안 되므로 보드 밑에 그대로 둔다.
        go.transform.SetParent(transform, false);

        var line = go.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.widthMultiplier = ropeWidth;
        line.numCapVertices = 2;
        line.material = ResolveRopeMaterial();
        line.startColor = ropeColor;
        line.endColor = ropeColor;
        line.sortingLayerName = sortingLayer;
        // 그림보다 뒤에 그려야 매듭이 가려진다.
        line.sortingOrder = sortingOrder - 1;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        return line;
    }

    private Material ResolveRopeMaterial()
    {
        if (ropeMaterial != null)
            return ropeMaterial;

        if (runtimeRopeMaterial != null)
            return runtimeRopeMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        runtimeRopeMaterial = new Material(shader) { name = "Rope (Runtime)" };
        return runtimeRopeMaterial;
    }
}
