using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지정한 자리들에서 증기를 뿜어낸다. (뿜는 쪽)
/// 실패 연출용으로 <see cref="SpannerMiniGame.onFail"/> 에 <see cref="Burst"/> 를 연결해서 쓴다.
/// 뿜을 위치는 빈 오브젝트를 원하는 자리에 두고 <c>spawnPoints</c> 에 끌어다 넣어 지정한다.
/// </summary>
public class SteamBurst : MonoBehaviour
{
    [Header("뿜을 위치")]
    [Tooltip("여기 넣은 오브젝트들의 자리에서 증기가 나온다. 개수는 자유롭게 늘리면 된다")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("증기")]
    [Tooltip("복제해서 뿜을 증기. 프리팹이어도 되고, 씬에 비활성으로 놔둔 오브젝트를 지정해도 된다")]
    [SerializeField] private SteamPuff puffPrefab;

    [Tooltip("위치 하나당 뿜어낼 덩이 수")]
    [SerializeField] private int puffsPerPoint = 5;

    [Tooltip("한 덩이씩 나오는 간격(초). 0이면 전부 한꺼번에 터진다")]
    [SerializeField] private float interval = 0.1f;

    [Tooltip("나오는 자리를 이만큼 무작위로 흩는다. 0이면 정확히 같은 점에서만 나온다")]
    [SerializeField] private Vector2 positionJitter = new Vector2(0.25f, 0.1f);

    // 뿜을 때마다 새로 만들지 않고 꺼진 것을 다시 쓴다.
    private readonly List<SteamPuff> pool = new List<SteamPuff>();
    private Coroutine running;

    /// <summary>증기를 뿜기 시작한다. 이미 뿜고 있었다면 처음부터 다시 시작한다.</summary>
    public void Burst()
    {
        if (puffPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
            return;

        StopBurst();
        running = StartCoroutine(BurstRoutine());
    }

    /// <summary>뿜기를 멈추고 나와 있던 증기를 모두 거둔다. (재시작용)</summary>
    public void StopBurst()
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null)
                pool[i].gameObject.SetActive(false);
        }
    }

    private IEnumerator BurstRoutine()
    {
        for (int round = 0; round < puffsPerPoint; round++)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                Transform point = spawnPoints[i];
                if (point == null)
                    continue;

                Vector3 position = point.position;
                position.x += Random.Range(-positionJitter.x, positionJitter.x);
                position.y += Random.Range(-positionJitter.y, positionJitter.y);

                Take().Play(position);
            }

            if (interval > 0f)
                yield return new WaitForSeconds(interval);
        }

        running = null;
    }

    /// <summary>꺼져 있는 덩이를 하나 꺼내 온다. 모자라면 새로 만든다.</summary>
    private SteamPuff Take()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && !pool[i].gameObject.activeSelf)
                return pool[i];
        }

        SteamPuff puff = Instantiate(puffPrefab, transform);
        puff.gameObject.SetActive(false);
        pool.Add(puff);
        return puff;
    }
}
