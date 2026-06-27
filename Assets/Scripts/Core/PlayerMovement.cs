using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    public GameManager gameManager;
    [SerializeField] private AbilityManager abilityManager;


    [Header("Components")]
    private Rigidbody2D rb;
    private AudioSource audioSource;


    [Header("Audio Clips & VFX")]
    [SerializeField] private AudioClip clickClip;
    [SerializeField] private AudioClip winClip;
    [SerializeField] private AudioClip loseClip;

    public ParticleSystem hitEffect;


    [Header("Player Status")]
    [SerializeField] private LayerMask clickMask;
    private Camera cam;

    [SerializeField] private float moveSpeed = 8f;
    private bool isMoving;
    public Vector2Int LastMoveDirection { get; private set; }
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();
        cam = Camera.main;
    }

    void Update()
    {
        if (gameManager.IsGameOver) return;
        if (!gameManager.isGameStarted) return;
        if (gameManager.CurrentTurn != GameManager.TurnState.PlayerTurn) return;
        if (Pointer.current == null) return;
        if (isMoving) return;

        if (Pointer.current.press.wasPressedThisFrame)
        {
            Vector2 screenPos = Pointer.current.position.ReadValue();
            Vector2 worldPos = cam.ScreenToWorldPoint(screenPos);

            if (abilityManager != null && abilityManager.IsPlacingObstacle)
            {
                bool placed = abilityManager.TryPlacePlayerObstacleAtWorldPos(worldPos);
                if (placed)
                {
                    Debug.Log("Engel yerleştirildi!");
                    gameManager.CurrentTurn = GameManager.TurnState.AITurn;
                }
                else
                {
                    Debug.LogWarning("Engel yerleştirilemedi! Geçersiz konum.");
                }
                return;
            }

            RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero, 0f, clickMask);
            if (hit.collider == null) return;

            Tile currentTile = GetCurrentTile();
            Tile targetTile = hit.collider.GetComponent<Tile>();

            if (currentTile == null || targetTile == null) return;

            Vector2Int delta = targetTile.gridPos - currentTile.gridPos;
            LastMoveDirection = delta;

            bool isValidMove = (Mathf.Abs(delta.x) == 1 && delta.y == 0) ||
                               (Mathf.Abs(delta.y) == 1 && delta.x == 0);

            if (!isValidMove)
            {
                Debug.LogWarning("Geçersiz hamle! Sadece komşu kareye gidebilirsin.");
                return;
            }

            if (GridManager.IsBlocked(currentTile.gridPos, targetTile.gridPos))
            {
                Debug.LogWarning("Bu yönde engel var, geçemezsin!");
                return;
            }

            StartCoroutine(MoveToTile(targetTile));

            if (hitEffect != null)
            {
                var fx = Instantiate(hitEffect, targetTile.transform.position, Quaternion.identity);
                Destroy(fx.gameObject, 1f);
            }
            if (audioSource && clickClip)
                audioSource.PlayOneShot(clickClip);

            gameManager.CurrentTurn = GameManager.TurnState.AITurn;
        }
    }
    IEnumerator MoveToTile(Tile targetTile)
    {
        isMoving = true;

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

        isMoving = false;
    }

    Tile GetCurrentTile()
    {
        Collider2D hitCollider = Physics2D.OverlapCircle(transform.position, 0.1f, clickMask);

        if (hitCollider != null)
        {
            return hitCollider.GetComponent<Tile>();
        }

        Vector2Int approximateGridPos = new Vector2Int(
            Mathf.RoundToInt(transform.position.x),
            Mathf.RoundToInt(transform.position.y)
        );
        return GridManager.GetTile(approximateGridPos);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Win"))
        {
            gameManager.EndGame(GameManager.TurnState.Win);
        }
    }
}