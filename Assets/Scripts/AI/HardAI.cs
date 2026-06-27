using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class HardAIData
{
    // Q-Table: "x,y,dx,dy" → Q değeri
    public List<string> qKeys = new List<string>();
    public List<float> qValues = new List<float>();

    // Oyuncunun hamle geçmişi istatistiği: "dx,dy" → kaç kez yapıldı
    public List<string> playerPatternKeys = new List<string>();
    public List<int> playerPatternValues = new List<int>();

    public int gamesPlayed = 0;
}

public class HardAI : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    private AbilityManager abilityManager;

    [SerializeField] private float moveSpeed = 8f;

    [Header("VFX & SFX")]
    [SerializeField] private ParticleSystem hitEffect;
    [SerializeField] private AudioClip clickClip;

    public Vector2Int LastMoveDirection { get; private set; }

    // ── Q-Learning parametreleri ─────────────────────────────────────────────
    private const float LearningRate = 0.2f;
    private const float Discount = 0.9f;
    private const float ExploreStart = 0.3f;
    private const float ExploreMin = 0.05f;
    private const float ExploreDecay = 0.01f;

    private const int PatternWindow = 5;

    private bool isMoving;
    private Transform loseTile;
    private HardAIData data = new HardAIData();
    private Dictionary<string, float> qTable = new Dictionary<string, float>();
    private Dictionary<string, int> playerPatterns = new Dictionary<string, int>();
    private List<Vector2Int> playerMoveHistory = new List<Vector2Int>();

    private Vector2Int lastPos = new Vector2Int(-999, -999);
    private Vector2Int secondLastPos = new Vector2Int(-999, -999);

    private struct MoveRecord { public string state; public string action; }
    private MoveRecord lastRecord;

    private string SavePath => Path.Combine(Application.persistentDataPath, "hard_ai_brain.json");

    private void Start()
    {
        abilityManager = FindAnyObjectByType<AbilityManager>();
        GameObject t = GameObject.FindGameObjectWithTag("Lose");
        if (t != null) loseTile = t.transform;

        LoadBrain();
    }

    private void Update()
    {
        if (gameManager.IsGameOver) return;
        if (!gameManager.isGameStarted) return;
        if (gameManager.CurrentTurn != GameManager.TurnState.AITurn) return;
        if (isMoving) return;

        StartCoroutine(MakeMove());
    }

    private void OnDestroy() => SaveBrain();

    IEnumerator MakeMove()
    {
        isMoving = true;
        yield return new WaitForSeconds(0.3f); // Hard AI biraz daha hızlı düşünür

        if (gameManager.IsGameOver) { isMoving = false; yield break; }

        Vector2Int currentPos = Vector2Int.RoundToInt(transform.position);
        Tile currentTile = GridManager.GetTile(currentPos);
        if (currentTile == null || loseTile == null) { isMoving = false; yield break; }

        if (abilityManager != null)
        {
            bool trulyBlocked = GetBFSDistance(currentPos) == int.MaxValue;
            if (trulyBlocked || Random.value < 0.15f)
            {
                bool used = abilityManager.AIUseRandomAbility();
                if (used)
                {
                    Debug.Log("Hard AI yetenek kullandı.");
                    gameManager.CurrentTurn = GameManager.TurnState.PlayerTurn;
                    isMoving = false;
                    yield break;
                }
            }
        }

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        List<Tile> validTiles = new List<Tile>();

        foreach (var dir in dirs)
        {
            Vector2Int np = currentTile.gridPos + dir;
            Tile tile = GridManager.GetTile(np);
            if (tile == null) continue;
            if (GridManager.IsBlocked(currentTile.gridPos, np)) continue;
            validTiles.Add(tile);
        }

        if (validTiles.Count == 0)
        {
            Debug.Log("Hard AI sıkıştı!");
            gameManager.CurrentTurn = GameManager.TurnState.PlayerTurn;
            isMoving = false;
            yield break;
        }

        string state = StateKey(currentTile.gridPos);
        Tile chosenTile = ChooseAction(state, currentTile, validTiles);

        if (lastRecord.state != null)
        {
            float reward = ComputeReward(currentPos);
            UpdateQ(lastRecord.state, lastRecord.action, reward, state, validTiles, currentTile);
        }

        string actionKey = ActionKey(chosenTile.gridPos - currentTile.gridPos);
        lastRecord = new MoveRecord { state = state, action = actionKey };

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

    // ── Hamle Seçim Mantığı ──────────────────────────────────────────────────
    private Tile ChooseAction(string state, Tile currentTile, List<Tile> validTiles)
    {
        float epsilon = Mathf.Max(ExploreMin, ExploreStart - data.gamesPlayed * ExploreDecay);

        // Epsilon-greedy: küçük ihtimalle keşfet
        if (Random.value < epsilon)
            return validTiles[Random.Range(0, validTiles.Count)];

        Tile counterTile = TryCounterPlayerMove(currentTile, validTiles);

        if (counterTile != null && Random.value < 0.4f)
            return counterTile;

        Tile bestTile = null;
        float bestQ = float.MinValue;

        foreach (var tile in validTiles)
        {
            Vector2Int dir = tile.gridPos - currentTile.gridPos;
            string aKey = ActionKey(dir);
            float q = GetQ(state, aKey);

            if (tile.gridPos == lastPos) q -= 2f;
            if (tile.gridPos == secondLastPos) q -= 1.5f;

            if (IsWayBlockedToTarget(tile.gridPos)) q -= 1f;

            if (q > bestQ) { bestQ = q; bestTile = tile; }
        }

        return bestTile ?? validTiles[0];
    }

    private Tile TryCounterPlayerMove(Tile currentTile, List<Tile> validTiles)
    {
        if (playerPatterns.Count == 0) return null;

        Vector2Int likelyPlayerMove = Vector2Int.zero;
        int maxCount = 0;
        foreach (var kv in playerPatterns)
        {
            if (kv.Value > maxCount)
            {
                maxCount = kv.Value;
                likelyPlayerMove = ParseDir(kv.Key);
            }
        }

        if (maxCount < 2) return null;

        foreach (var tile in validTiles)
        {
            Vector2Int d = tile.gridPos - currentTile.gridPos;
            if (d == likelyPlayerMove) return tile;
        }
        return null;
    }

    // ── Oyuncu Hamlesi Kayıt ─────────────────────────────────────────────────
    /// <summary>
    /// GameManager'dan çağrılır — oyuncu her hamle yaptığında buraya bildir.
    /// </summary>
    public void RecordPlayerMove(Vector2Int direction)
    {
        playerMoveHistory.Add(direction);
        if (playerMoveHistory.Count > 50) playerMoveHistory.RemoveAt(0); // Belleği sınırla

        // Son PatternWindow hamleyi say
        int start = Mathf.Max(0, playerMoveHistory.Count - PatternWindow);
        for (int i = start; i < playerMoveHistory.Count; i++)
        {
            string key = ActionKey(playerMoveHistory[i]);
            if (!playerPatterns.ContainsKey(key)) playerPatterns[key] = 0;
            playerPatterns[key]++;
        }
    }

    // ── Oyun Sonu Ödülü ──────────────────────────────────────────────────────
    /// <summary>
    /// Oyun bittiğinde GameManager'dan çağrılır.
    /// </summary>
    public void OnGameEnd(bool aiWon)
    {
        Vector2Int pos = Vector2Int.RoundToInt(transform.position);
        string state = StateKey(pos);

        float terminalReward = aiWon ? 10f : -10f;

        if (lastRecord.state != null)
            UpdateQTerminal(lastRecord.state, lastRecord.action, terminalReward);

        data.gamesPlayed++;
        lastRecord = default;
        SaveBrain();
        Debug.Log($"Hard AI oyun bitti. Kazandı mı: {aiWon} | Toplam oyun: {data.gamesPlayed}");
    }

    // ── Q-Learning Çekirdek ──────────────────────────────────────────────────
    private float GetQ(string state, string action)
    {
        string key = state + "|" + action;
        return qTable.ContainsKey(key) ? qTable[key] : 0f;
    }

    private void SetQ(string state, string action, float val)
    {
        qTable[state + "|" + action] = val;
    }

    private void UpdateQ(string state, string action, float reward, string nextState, List<Tile> nextValidTiles, Tile nextCurrentTile)
    {
        float maxNextQ = float.MinValue;
        foreach (var tile in nextValidTiles)
        {
            string a = ActionKey(tile.gridPos - nextCurrentTile.gridPos);
            float q = GetQ(nextState, a);
            if (q > maxNextQ) maxNextQ = q;
        }
        if (maxNextQ == float.MinValue) maxNextQ = 0f;

        float oldQ = GetQ(state, action);
        float newQ = oldQ + LearningRate * (reward + Discount * maxNextQ - oldQ);
        SetQ(state, action, newQ);
    }

    private void UpdateQTerminal(string state, string action, float reward)
    {
        float oldQ = GetQ(state, action);
        float newQ = oldQ + LearningRate * (reward - oldQ);
        SetQ(state, action, newQ);
    }

    /// <summary>
    /// Bir adımın anlık ödülü: hedefe yaklaşıyorsa pozitif, uzaklaşıyorsa negatif.
    /// </summary>
    private float ComputeReward(Vector2Int currentPos)
    {
        if (loseTile == null) return 0f;
        int bfs = GetBFSDistance(currentPos);
        if (bfs == int.MaxValue) return -2f;   // Çıkış yok → kötü
        // Hedefe yaklaştıkça artan ödül
        return 1f / Mathf.Max(1f, bfs);
    }

    private string StateKey(Vector2Int pos)
    {
        if (loseTile == null) return $"{pos.x},{pos.y},0,0";
        Vector2Int goal = new Vector2Int(
            Mathf.RoundToInt(loseTile.position.x),
            Mathf.RoundToInt(loseTile.position.y));
        Vector2Int rel = goal - pos;
        rel.x = Mathf.Clamp(rel.x, -3, 3);
        rel.y = Mathf.Clamp(rel.y, -3, 3);
        return $"{rel.x},{rel.y}";
    }

    private string ActionKey(Vector2Int dir) => $"{dir.x},{dir.y}";

    private Vector2Int ParseDir(string key)
    {
        var p = key.Split(',');
        return new Vector2Int(int.Parse(p[0]), int.Parse(p[1]));
    }

    private bool IsWayBlockedToTarget(Vector2Int checkPos)
    {
        if (loseTile == null) return false;
        Vector2Int goal = new Vector2Int(
            Mathf.RoundToInt(loseTile.position.x),
            Mathf.RoundToInt(loseTile.position.y));

        Vector2Int primaryDir = Vector2Int.zero;
        if (goal.y > checkPos.y) primaryDir = Vector2Int.up;
        else if (goal.y < checkPos.y) primaryDir = Vector2Int.down;
        else if (goal.x > checkPos.x) primaryDir = Vector2Int.right;
        else if (goal.x < checkPos.x) primaryDir = Vector2Int.left;

        if (primaryDir != Vector2Int.zero)
            return GridManager.IsBlocked(checkPos, checkPos + primaryDir);
        return false;
    }

    private int GetBFSDistance(Vector2Int startPos)
    {
        if (loseTile == null) return int.MaxValue;
        Vector2Int goal = new Vector2Int(
            Mathf.RoundToInt(loseTile.position.x),
            Mathf.RoundToInt(loseTile.position.y));

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

    IEnumerator MoveToTile(Tile targetTile)
    {
        Vector3 start = transform.position;
        Vector3 end = targetTile.transform.position;
        float t = 0f, duration = 1f / moveSpeed;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }
        transform.position = end;
    }

    // ── Kayıt / Yükleme ──────────────────────────────────────────────────────
    private void SaveBrain()
    {
        data.qKeys.Clear(); data.qValues.Clear();
        foreach (var kv in qTable) { data.qKeys.Add(kv.Key); data.qValues.Add(kv.Value); }

        data.playerPatternKeys.Clear(); data.playerPatternValues.Clear();
        foreach (var kv in playerPatterns) { data.playerPatternKeys.Add(kv.Key); data.playerPatternValues.Add(kv.Value); }

        File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        Debug.Log($"Hard AI beyni kaydedildi → {SavePath}");
    }
    private void LoadBrain()
    {
        if (!File.Exists(SavePath)) { Debug.Log("Hard AI: Kayıt bulunamadı, sıfırdan başlıyor."); return; }

        data = JsonUtility.FromJson<HardAIData>(File.ReadAllText(SavePath));

        qTable.Clear();
        for (int i = 0; i < data.qKeys.Count; i++) qTable[data.qKeys[i]] = data.qValues[i];

        playerPatterns.Clear();
        for (int i = 0; i < data.playerPatternKeys.Count; i++) playerPatterns[data.playerPatternKeys[i]] = data.playerPatternValues[i];

        Debug.Log($"Hard AI beyni yüklendi. Toplam oyun: {data.gamesPlayed} | Q-Table: {qTable.Count} giriş");
    }
}