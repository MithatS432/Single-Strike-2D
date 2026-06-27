using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MediumAI : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    private AbilityManager abilityManager;
    private bool isMoving;
    public Vector2Int LastMoveDirection { get; private set; }

    [SerializeField] private float moveSpeed = 8f;

    [Header("VFX & SFX")]
    [SerializeField] private ParticleSystem hitEffect;
    [SerializeField] private AudioClip clickClip;

    private Transform loseTile;

    private Vector2Int lastPos = new Vector2Int(-999, -999);
    private Vector2Int secondLastPos = new Vector2Int(-999, -999);

    private void Start()
    {
        abilityManager = FindAnyObjectByType<AbilityManager>();
        GameObject targetObj = GameObject.FindGameObjectWithTag("Lose");
        if (targetObj != null) loseTile = targetObj.transform;
    }

    private void Update()
    {
        if (gameManager.IsGameOver) return;
        if (!gameManager.isGameStarted) return;
        if (gameManager.CurrentTurn != GameManager.TurnState.AITurn) return;
        if (isMoving) return;

        StartCoroutine(MakeMove());
    }

    IEnumerator MakeMove()
    {
        isMoving = true;
        yield return new WaitForSeconds(0.5f);

        if (gameManager.IsGameOver) { isMoving = false; yield break; }

        Vector2Int currentPos = Vector2Int.RoundToInt(transform.position);
        Tile currentTile = GridManager.GetTile(currentPos);

        if (currentTile == null || loseTile == null) { isMoving = false; yield break; }

        if (abilityManager != null)
        {
            bool trulyBlocked = GetBFSDistance(currentPos) == int.MaxValue;
            bool frontBlocked = IsWayBlockedToTarget(currentTile.gridPos);
            if (trulyBlocked || frontBlocked || Random.value < 0.20f)
            {
                bool usedAbility = abilityManager.AIUseRandomAbility();
                if (usedAbility)
                {
                    Debug.Log("Medium AI taktiksel olarak yetenek kullandı!");
                    gameManager.CurrentTurn = GameManager.TurnState.PlayerTurn;
                    isMoving = false;
                    yield break;
                }
            }
        }

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        List<Tile> validTiles = new List<Tile>();
        Tile bestTile = null;
        float bestScore = float.MaxValue;

        foreach (var dir in directions)
        {
            Vector2Int targetPos = currentTile.gridPos + dir;
            Tile tile = GridManager.GetTile(targetPos);

            if (tile == null) continue;
            if (GridManager.IsBlocked(currentTile.gridPos, targetPos)) continue;

            validTiles.Add(tile);

            int bfsDist = GetBFSDistance(targetPos);
            float score = bfsDist == int.MaxValue ? 999f : bfsDist;

            if (targetPos == lastPos) score += 3f;
            if (targetPos == secondLastPos) score += 2f;

            if (IsWayBlockedToTarget(targetPos)) score += 1.5f;

            if (score < bestScore)
            {
                bestScore = score;
                bestTile = tile;
            }
        }

        if (validTiles.Count == 0)
        {
            Debug.Log("Medium AI sıkıştı, pas geçiyor!");
            gameManager.CurrentTurn = GameManager.TurnState.PlayerTurn;
            isMoving = false;
            yield break;
        }

        Tile chosenTile;
        float roll = Random.value;
        float currentDistToTarget = Vector2.Distance(transform.position, loseTile.position);

        if (currentDistToTarget <= 2f)
        {
            // Hedefe çok yakın → kesinlikle en iyi hamle
            chosenTile = bestTile;
        }
        else
        {
            if (roll < 0.85f)
                chosenTile = bestTile;                                         // %85 en akıllı hamle
            else
                chosenTile = validTiles[Random.Range(0, validTiles.Count)];    // %15 rastgele
        }

        secondLastPos = lastPos;
        lastPos = currentPos;

        LastMoveDirection = chosenTile.gridPos - currentTile.gridPos;
        yield return StartCoroutine(MoveToTile(chosenTile));

        if (hitEffect != null)
        {
            var fx = Instantiate(hitEffect, chosenTile.transform.position, Quaternion.identity);
            Destroy(fx.gameObject, 1f);
        }
        AudioSource audio = GetComponent<AudioSource>();
        if (audio && clickClip) audio.PlayOneShot(clickClip);

        gameManager.CurrentTurn = GameManager.TurnState.PlayerTurn;
        isMoving = false;
    }

    IEnumerator MoveToTile(Tile targetTile)
    {
        Vector3 start = transform.position;
        Vector3 end = targetTile.transform.position;
        float t = 0f;
        float duration = 1f / moveSpeed;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }
        transform.position = end;
    }

    /// <summary>
    /// BFS ile startPos'tan loseTile'a gerçek adım mesafesini hesaplar.
    /// Duvarları ve engelleri dikkate alır. Ulaşılamazsa int.MaxValue döner.
    /// </summary>
    private int GetBFSDistance(Vector2Int startPos)
    {
        if (loseTile == null) return int.MaxValue;

        Vector2Int goal = new Vector2Int(
            Mathf.RoundToInt(loseTile.position.x),
            Mathf.RoundToInt(loseTile.position.y)
        );

        if (startPos == goal) return 0;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> visited = new Dictionary<Vector2Int, int>();

        queue.Enqueue(startPos);
        visited[startPos] = 0;

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            int dist = visited[cur];

            foreach (var dir in dirs)
            {
                Vector2Int next = cur + dir;
                if (visited.ContainsKey(next)) continue;
                if (GridManager.GetTile(next) == null) continue;
                if (GridManager.IsBlocked(cur, next)) continue;

                if (next == goal) return dist + 1;

                visited[next] = dist + 1;
                queue.Enqueue(next);
            }
        }

        return int.MaxValue;
    }


    private bool IsWayBlockedToTarget(Vector2Int checkPos)
    {
        if (loseTile == null) return false;

        Vector2Int goal = new Vector2Int(
            Mathf.RoundToInt(loseTile.position.x),
            Mathf.RoundToInt(loseTile.position.y)
        );

        Vector2Int primaryDir = Vector2Int.zero;
        if (goal.y > checkPos.y) primaryDir = Vector2Int.up;
        else if (goal.y < checkPos.y) primaryDir = Vector2Int.down;
        else if (goal.x > checkPos.x) primaryDir = Vector2Int.right;
        else if (goal.x < checkPos.x) primaryDir = Vector2Int.left;

        if (primaryDir != Vector2Int.zero)
            return GridManager.IsBlocked(checkPos, checkPos + primaryDir);

        return false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Lose"))
            gameManager.EndGame(GameManager.TurnState.Lose);
    }
}