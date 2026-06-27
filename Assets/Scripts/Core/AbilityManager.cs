using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class AbilityManager : MonoBehaviour
{
    [Header("Referances")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Transform playerTransform;

    [Header("Obstacle Prefabs")]
    [SerializeField] private GameObject playerObstaclePrefab;
    [SerializeField] private GameObject aiObstaclePrefab;

    public int playerObstacleCount = 3;
    public int playerPushbackCount = 3;
    public int aiObstacleCount = 3;
    public int aiPushbackCount = 3;

    [Header("UI -(TextMeshPro)")]
    [SerializeField] private TMP_Text playerObstacleText;
    [SerializeField] private TMP_Text playerPushbackText;
    [SerializeField] private TMP_Text aiObstacleText;
    [SerializeField] private TMP_Text aiPushbackText;

    [Header("UI - Buttons Of Player")]
    [SerializeField] private Button playerObstacleButton;
    [SerializeField] private Button playerPushbackButton;

    [Header("Audios")]
    [SerializeField] private AudioClip obstacleClip;
    [SerializeField] private AudioClip pushbackClip;

    public bool IsPlacingObstacle { get; private set; }
    private Transform aiTransform;

    private void Start()
    {
        if (playerObstacleButton != null)
            playerObstacleButton.onClick.AddListener(OnPlayerObstacleClicked);

        if (playerPushbackButton != null)
            playerPushbackButton.onClick.AddListener(OnPlayerPushbackClicked);

        UpdateUI();
    }

    private void Update()
    {
        UpdateButtonStates();
    }
    public void SetAITransform(Transform ai)
    {
        aiTransform = ai;
    }

    #region UI Güncellemeleri

    private void UpdateUI()
    {
        if (playerObstacleText != null) playerObstacleText.text = $"x{playerObstacleCount}";
        if (playerPushbackText != null) playerPushbackText.text = $"x{playerPushbackCount}";
        if (aiObstacleText != null) aiObstacleText.text = $"x{aiObstacleCount}";
        if (aiPushbackText != null) aiPushbackText.text = $"x{aiPushbackCount}";
    }

    private void UpdateButtonStates()
    {
        bool canUse = gameManager != null &&
                      gameManager.isGameStarted &&
                      !gameManager.IsGameOver &&
                      gameManager.CurrentTurn == GameManager.TurnState.PlayerTurn;

        if (playerObstacleButton != null)
            playerObstacleButton.interactable = canUse && playerObstacleCount > 0;

        if (playerPushbackButton != null)
            playerPushbackButton.interactable = canUse && playerPushbackCount > 0;
    }

    #endregion

    #region Player Yetenekleri

    /// <summary>
    /// Player engel butonuna tıkladığında çağrılır - engel yerleştirme modunu açar/kapatır.
    /// </summary>
    public void OnPlayerObstacleClicked()
    {
        if (gameManager.CurrentTurn != GameManager.TurnState.PlayerTurn) return;
        if (playerObstacleCount <= 0) return;

        IsPlacingObstacle = !IsPlacingObstacle;
    }

    /// <summary>
    /// Player geri itme butonuna tıkladığında çağrılır - AI'yı 1 kare geri iter.
    /// </summary>
    public void OnPlayerPushbackClicked()
    {
        if (gameManager.CurrentTurn != GameManager.TurnState.PlayerTurn) return;
        if (playerPushbackCount <= 0) return;
        if (aiTransform == null) return;

        Vector2Int playerPos = new Vector2Int(
            Mathf.RoundToInt(playerTransform.position.x),
            Mathf.RoundToInt(playerTransform.position.y));

        Vector2Int aiPos = new Vector2Int(
            Mathf.RoundToInt(aiTransform.position.x),
            Mathf.RoundToInt(aiTransform.position.y));

        if (Pushback(aiTransform, Vector2Int.up))
        {
            playerPushbackCount--;
            IsPlacingObstacle = false;
            UpdateUI();
            PlaySound(pushbackClip);
            gameManager.CurrentTurn = GameManager.TurnState.AITurn;
        }
        else
        {
            Debug.LogWarning("Geri itme başarısız! Hedefin arkasında kare yok veya engel var.");
        }
    }

    /// <summary>
    /// PlayerMovement tarafından çağrılır - engel yerleştirme modundayken tıklanan
    /// world pozisyonuna en yakın kenarı bulup engel yerleştirir.
    /// </summary>
    public bool TryPlacePlayerObstacleAtWorldPos(Vector2 worldPos)
    {
        if (playerObstacleCount <= 0) return false;

        Vector2Int tileA, tileB;
        if (!FindNearestEdge(worldPos, out tileA, out tileB)) return false;
        if (!GridManager.CanPlaceObstacle(tileA, tileB)) return false;

        PlaceObstacleVisual(tileA, tileB, playerObstaclePrefab);
        GridManager.AddObstacle(tileA, tileB);

        playerObstacleCount--;
        IsPlacingObstacle = false;
        UpdateUI();
        PlaySound(obstacleClip);

        return true;
    }

    /// <summary>
    /// Engel yerleştirme modunu iptal eder.
    /// </summary>
    public void CancelPlacement()
    {
        IsPlacingObstacle = false;
    }

    #endregion

    #region AI Yetenekleri

    /// <summary>
    /// Easy AI rastgele bir yetenek seçip kullanır.
    /// Başarılıysa true döner.
    /// </summary>
    public bool AIUseRandomAbility()
    {
        List<System.Func<bool>> available = new();

        if (aiObstacleCount > 0) available.Add(AITryPlaceObstacle);

        if (aiPushbackCount > 0) available.Add(() =>
        {
            bool result = Pushback(playerTransform, Vector2Int.down);
            if (result)
            {
                aiPushbackCount--;
                UpdateUI();
                PlaySound(pushbackClip);
                Debug.Log("AI pushback → Player DOWN");
            }
            return result;
        });

        if (available.Count == 0) return false;

        return available[Random.Range(0, available.Count)]();
    }


    private bool AITryPlaceObstacle()
    {
        if (playerTransform == null) return false;

        Vector2Int playerPos = new Vector2Int(
            Mathf.RoundToInt(playerTransform.position.x),
            Mathf.RoundToInt(playerTransform.position.y));

        var validEdges = GridManager.GetValidEdgesAround(playerPos);

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in dirs)
        {
            Vector2Int neighbor = playerPos + dir;
            if (GridManager.GetTile(neighbor) != null)
            {
                var edges = GridManager.GetValidEdgesAround(neighbor);
                foreach (var e in edges)
                {
                    if (!validEdges.Contains(e))
                        validEdges.Add(e);
                }
            }
        }

        if (validEdges.Count == 0) return false;

        var edge = validEdges[Random.Range(0, validEdges.Count)];

        PlaceObstacleVisual(edge.Item1, edge.Item2, aiObstaclePrefab);
        GridManager.AddObstacle(edge.Item1, edge.Item2);

        aiObstacleCount--;
        UpdateUI();
        PlaySound(obstacleClip);

        Debug.Log($"AI engel koydu: ({edge.Item1}) - ({edge.Item2})");
        return true;
    }
    private bool Pushback(Transform target, Vector2Int direction)
    {
        Vector2Int targetPos = new Vector2Int(
            Mathf.RoundToInt(target.position.x),
            Mathf.RoundToInt(target.position.y));

        Vector2Int destination = targetPos + direction;

        Tile destTile = GridManager.GetTile(destination);
        if (destTile == null) return false;
        if (GridManager.IsBlocked(targetPos, destination)) return false;

        target.position = destTile.transform.position;
        return true;
    }

    #endregion

    #region Ortak Mantık

    /// <summary>
    /// Hedefi, kullanan kişiden 1 kare uzaklaştırır (itme).
    /// userPos: Yeteneği kullanan kişinin pozisyonu
    /// targetPos: Hedefin pozisyonu
    /// targetTransform: Hedefin Transform'u (pozisyon güncellemek için)
    /// </summary>
    /// <summary>
    /// Hedefi, kendi bölgesine doğru 1 kare geri iter.
    /// Player → aşağı (y-1), AI → yukarı (y+1) yönünde.
    /// </summary>


    /// <summary>
    /// World pozisyonuna en yakın kenarı bulur (iki komşu karenin arasını).
    /// </summary>
    private bool FindNearestEdge(Vector2 worldPos, out Vector2Int tileA, out Vector2Int tileB)
    {
        tileA = tileB = Vector2Int.zero;

        float fx = worldPos.x - Mathf.Floor(worldPos.x);
        float fy = worldPos.y - Mathf.Floor(worldPos.y);

        // Hangi eksene daha yakın: x = 0.5 (dikey duvar) mı, y = 0.5 (yatay duvar) mı?
        float dxEdge = Mathf.Abs(fx - 0.5f);
        float dyEdge = Mathf.Abs(fy - 0.5f);

        if (dxEdge < dyEdge)
        {
            int leftX = Mathf.FloorToInt(worldPos.x);
            int rightX = leftX + 1;
            int y = Mathf.RoundToInt(worldPos.y);
            tileA = new Vector2Int(leftX, y);
            tileB = new Vector2Int(rightX, y);
        }
        else
        {
            int x = Mathf.RoundToInt(worldPos.x);
            int bottomY = Mathf.FloorToInt(worldPos.y);
            int topY = bottomY + 1;
            tileA = new Vector2Int(x, bottomY);
            tileB = new Vector2Int(x, topY);
        }

        return GridManager.GetTile(tileA) != null && GridManager.GetTile(tileB) != null;
    }

    /// <summary>
    /// İki kare arasının orta noktasına engel prefabı yerleştirir.
    /// Yönüne göre 90° döndürür.
    /// </summary>
    private void PlaceObstacleVisual(Vector2Int a, Vector2Int b, GameObject prefab)
    {
        Tile tA = GridManager.GetTile(a);
        Tile tB = GridManager.GetTile(b);

        if (tA == null || tB == null) return;

        Vector3 midpoint = (tA.transform.position + tB.transform.position) / 2f;

        Vector2Int diff = b - a;
        Quaternion rotation = (diff.y == 0)
            ? Quaternion.Euler(0, 0, 90f)
            : Quaternion.identity;

        Instantiate(prefab, midpoint, rotation);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && Camera.main != null)
            AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position);
    }

    #endregion
}