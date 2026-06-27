using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EasyAI : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    private AbilityManager abilityManager;
    private bool isMoving;
    public Vector2Int LastMoveDirection { get; private set; }


    [SerializeField] private float moveSpeed = 8f;

    [Header("VFX & SFX")]
    [SerializeField] private ParticleSystem hitEffect;
    [SerializeField] private AudioClip clickClip;

    private void Start()
    {
        abilityManager = FindAnyObjectByType<AbilityManager>();
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

        yield return new WaitForSeconds(0.7f);

        if (gameManager.IsGameOver)
        {
            isMoving = false;
            yield break;
        }


        if (abilityManager != null && Random.value < 0.30f)
        {
            bool usedAbility = abilityManager.AIUseRandomAbility();
            if (usedAbility)
            {
                Debug.Log("AI yetenek kullandı!");
                gameManager.CurrentTurn = GameManager.TurnState.PlayerTurn;
                isMoving = false;
                yield break;
            }
        }

        Transform loseTile = GameObject.FindGameObjectWithTag("Lose")?.transform;
        if (loseTile == null)
        {
            isMoving = false;
            yield break;
        }

        Vector2Int currentPos = Vector2Int.RoundToInt(transform.position);
        Tile currentTile = GridManager.GetTile(currentPos);

        if (currentTile == null)
        {
            isMoving = false;
            yield break;
        }

        List<Tile> validTiles = new List<Tile>();
        Tile bestTile = null;
        float bestDist = float.MaxValue;

        foreach (var dir in new Vector2Int[]
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        })
        {
            Vector2Int targetPos = currentTile.gridPos + dir;
            Tile tile = GridManager.GetTile(targetPos);

            if (tile == null) continue;

            if (GridManager.IsBlocked(currentTile.gridPos, targetPos))
            {
                continue;
            }

            validTiles.Add(tile);

            float dist = Vector2.Distance(tile.transform.position, loseTile.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestTile = tile;
            }
        }

        if (validTiles.Count == 0)
        {
            Debug.Log("AI hiçbir yere gidemiyor (engeller yüzünden)!");
            gameManager.CurrentTurn = GameManager.TurnState.PlayerTurn;
            isMoving = false;
            yield break;
        }

        Tile chosenTile;
        float roll = Random.value;

        if (roll < 0.60f) chosenTile = bestTile;
        else if (roll < 0.85f) chosenTile = validTiles[Random.Range(0, validTiles.Count)];
        else
        {
            Tile worstTile = bestTile;
            float worstDist = 0f;
            foreach (var tile in validTiles)
            {
                float dist = Vector2.Distance(tile.transform.position, loseTile.position);
                if (dist > worstDist)
                {
                    worstDist = dist;
                    worstTile = tile;
                }
            }
            chosenTile = worstTile;
        }

        LastMoveDirection = chosenTile.gridPos - currentTile.gridPos;

        yield return StartCoroutine(MoveToTile(chosenTile));

        if (hitEffect != null)
        {
            var fx = Instantiate(hitEffect, chosenTile.transform.position, Quaternion.identity);
            Destroy(fx.gameObject, 1f);
        }

        AudioSource audio = GetComponent<AudioSource>();
        if (audio && clickClip)
            audio.PlayOneShot(clickClip);

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
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Lose"))
        {
            gameManager.EndGame(GameManager.TurnState.Lose);
        }
    }
}