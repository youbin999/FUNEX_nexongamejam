using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지정한 자리들에서 동전과 지폐를 뿜어낸다. (뿜는 쪽)
/// 성공 연출용으로 <see cref="StockMiniGame"/> 의 봉별 <c>onSuccess</c> 에 <see cref="Burst"/> 를 연결해서 쓴다.
/// 뿜을 위치는 빈 오브젝트를 원하는 자리에 두고 <c>spawnPoints</c> 에 끌어다 넣어 지정한다.
/// 조각은 <see cref="MoneyPiece"/> 를 복제해 재사용한다.
/// </summary>
public class MoneyBurst : MonoBehaviour
{
    [Header("뿜을 위치")]
    [Tooltip("여기 넣은 오브젝트들의 자리에서 돈이 나온다. 비워두면 자기 자신의 위치에서 나온다")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("나오는 자리를 이만큼 무작위로 흩는다")]
    [SerializeField] private Vector2 positionJitter = new Vector2(0.3f, 0.15f);

    [Header("뿜을 것")]
    [Tooltip("복제해서 뿜을 동전. 여러 개 넣으면 그중 하나가 무작위로 나온다")]
    [SerializeField] private MoneyPiece[] coinTemplates;

    [Tooltip("한 번에 뿜을 동전 수")]
    [SerializeField] private int coinCount = 1;

    [Tooltip("복제해서 뿜을 지폐. 여러 개 넣으면 그중 하나가 무작위로 나온다")]
    [SerializeField] private MoneyPiece[] billTemplates;

    [Tooltip("한 번에 뿜을 지폐 수")]
    [SerializeField] private int billCount = 0;

    [Header("튀어나가는 힘")]
    [Tooltip("초기 속도(초당 유닛)의 범위")]
    [SerializeField] private Vector2 speedRange = new Vector2(5f, 7f);

    [Tooltip("똑바로 위를 0도로 봤을 때 퍼지는 각도(도)의 범위. 음수가 왼쪽")]
    [SerializeField] private Vector2 angleRange = new Vector2(-30f, 30f);

    [Tooltip("한 조각씩 나오는 간격(초). 0이면 전부 한꺼번에 터진다")]
    [SerializeField] private float interval = 0.04f;

    // 뿜을 때마다 새로 만들지 않고 꺼진 것을 다시 쓴다. 템플릿마다 따로 담아 둔다.
    private readonly Dictionary<MoneyPiece, List<MoneyPiece>> pools = new Dictionary<MoneyPiece, List<MoneyPiece>>();
    private Coroutine running;

    /// <summary>돈을 뿜기 시작한다. 이미 뿜고 있었다면 처음부터 다시 시작한다.</summary>
    public void Burst()
    {
        if (!isActiveAndEnabled)
            return;

        StopBurst();
        running = StartCoroutine(BurstRoutine());
    }

    /// <summary>뿜기를 멈추고 나와 있던 조각을 모두 거둔다. (재시작용)</summary>
    public void StopBurst()
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        foreach (var pool in pools.Values)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null)
                    pool[i].gameObject.SetActive(false);
            }
        }
    }

    private IEnumerator BurstRoutine()
    {
        // 동전과 지폐를 번갈아 내보낸다. 한쪽이 다 나오고 나서 다른 쪽이 나오면 두 번 터진 것처럼 보인다.
        int coinsLeft = HasTemplate(coinTemplates) ? Mathf.Max(0, coinCount) : 0;
        int billsLeft = HasTemplate(billTemplates) ? Mathf.Max(0, billCount) : 0;

        while (coinsLeft > 0 || billsLeft > 0)
        {
            if (coinsLeft > 0)
            {
                Spawn(coinTemplates);
                coinsLeft--;
            }

            if (billsLeft > 0)
            {
                Spawn(billTemplates);
                billsLeft--;
            }

            if (interval > 0f)
                yield return new WaitForSeconds(interval);
        }

        running = null;
    }

    private void Spawn(MoneyPiece[] templates)
    {
        MoneyPiece template = PickTemplate(templates);
        if (template == null)
            return;

        Vector3 position = PickSpawnPosition();

        float angle = Random.Range(angleRange.x, angleRange.y);
        float speed = Random.Range(speedRange.x, speedRange.y);
        Vector2 velocity = Quaternion.Euler(0f, 0f, angle) * Vector2.up * speed;

        Take(template).Launch(position, velocity);
    }

    private Vector3 PickSpawnPosition()
    {
        Vector3 position = transform.position;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (point != null)
                position = point.position;
        }

        position.x += Random.Range(-positionJitter.x, positionJitter.x);
        position.y += Random.Range(-positionJitter.y, positionJitter.y);
        return position;
    }

    private static bool HasTemplate(MoneyPiece[] templates)
    {
        if (templates == null)
            return false;

        for (int i = 0; i < templates.Length; i++)
        {
            if (templates[i] != null)
                return true;
        }

        return false;
    }

    private static MoneyPiece PickTemplate(MoneyPiece[] templates)
    {
        if (templates == null || templates.Length == 0)
            return null;

        // 빈 칸이 섞여 있어도 되도록 몇 번 더 뽑아본다.
        for (int attempt = 0; attempt < templates.Length; attempt++)
        {
            MoneyPiece candidate = templates[Random.Range(0, templates.Length)];
            if (candidate != null)
                return candidate;
        }

        return null;
    }

    /// <summary>꺼져 있는 조각을 하나 꺼내 온다. 모자라면 새로 만든다.</summary>
    private MoneyPiece Take(MoneyPiece template)
    {
        if (!pools.TryGetValue(template, out List<MoneyPiece> pool))
        {
            pool = new List<MoneyPiece>();
            pools.Add(template, pool);
        }

        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && !pool[i].gameObject.activeSelf)
                return pool[i];
        }

        MoneyPiece piece = Instantiate(template, transform);
        piece.gameObject.SetActive(false);
        pool.Add(piece);
        return piece;
    }
}
