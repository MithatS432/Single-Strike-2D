using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static Dictionary<Vector2Int, Tile> tiles = new();

    // Kenar bazlı engel sistemi
    private static HashSet<long> blockedEdges = new();
    private static List<(Vector2Int, Vector2Int)> allObstacles = new();

    private void Awake()
    {
        tiles.Clear();
        blockedEdges.Clear();
        allObstacles.Clear();

        Tile[] allTiles = Object.FindObjectsByType<Tile>(FindObjectsSortMode.None);

        foreach (var t in allTiles)
        {
            Vector2Int hesaplananPos = new Vector2Int(
                Mathf.RoundToInt(t.transform.position.x),
                Mathf.RoundToInt(t.transform.position.y)
            );

            t.gridPos = hesaplananPos;
            tiles[hesaplananPos] = t;
        }
    }

    public static Tile GetTile(Vector2Int pos)
    {
        tiles.TryGetValue(pos, out Tile tile);
        return tile;
    }

    #region Edge-Based Obstacle System

    /// <summary>
    /// İki komşu tile pozisyonunu tek bir long key'e dönüştürür.
    /// Sıralama normalize edilir: (A,B) == (B,A)
    /// </summary>
    private static long MakeEdgeKey(Vector2Int a, Vector2Int b)
    {
        Vector2Int min, max;
        if (a.x < b.x || (a.x == b.x && a.y < b.y))
        { min = a; max = b; }
        else
        { min = b; max = a; }

        return ((long)(min.x + 500) << 48) |
               ((long)(min.y + 500) << 32) |
               ((long)(max.x + 500) << 16) |
               (long)(max.y + 500);
    }

    /// <summary>
    /// İki kare arasına engel ekler.
    /// </summary>
    public static void AddObstacle(Vector2Int a, Vector2Int b)
    {
        blockedEdges.Add(MakeEdgeKey(a, b));
        allObstacles.Add((a, b));
    }

    /// <summary>
    /// İki kare arası engelli mi kontrol eder.
    /// </summary>
    public static bool IsBlocked(Vector2Int from, Vector2Int to)
    {
        return blockedEdges.Contains(MakeEdgeKey(from, to));
    }

    /// <summary>
    /// Verilen iki kare arasına engel koyulabilir mi kontrol eder.
    /// Maksimum 3 engel kuralı: Yatay hatlar ve dikey hatlar kendi içinde sınırlandırılır.
    /// </summary>
    public static bool CanPlaceObstacle(Vector2Int a, Vector2Int b)
    {
        if (GetTile(a) == null || GetTile(b) == null) return false;
        if (IsBlocked(a, b)) return false;

        Vector2Int diff = b - a;
        bool isAdjacent = (Mathf.Abs(diff.x) + Mathf.Abs(diff.y)) == 1;
        if (!isAdjacent) return false;

        if (diff.y == 0)
        {
            float wallX = (a.x + b.x) / 2f;
            int count = 0;

            foreach (var obs in allObstacles)
            {
                Vector2Int oDiff = obs.Item2 - obs.Item1;
                if (oDiff.y == 0) // Eğer o da dikey duvarsa
                {
                    float oWallX = (obs.Item1.x + obs.Item2.x) / 2f;
                    if (Mathf.Approximately(wallX, oWallX)) count++;
                }
            }

            if (count >= 3) return false;
        }
        else
        {
            float wallY = (a.y + b.y) / 2f;
            int count = 0;

            foreach (var obs in allObstacles)
            {
                Vector2Int oDiff = obs.Item2 - obs.Item1;
                if (oDiff.x == 0) // Eğer o da yatay duvarsa
                {
                    float oWallY = (obs.Item1.y + obs.Item2.y) / 2f;
                    if (Mathf.Approximately(wallY, oWallY)) count++;
                }
            }

            if (count >= 3) return false;
        }

        return true;
    }

    /// <summary>
    /// Belirtilen karenin etrafındaki koyulabilir kenarları döndürür.
    /// </summary>
    public static List<(Vector2Int, Vector2Int)> GetValidEdgesAround(Vector2Int center)
    {
        var result = new List<(Vector2Int, Vector2Int)>();
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var dir in dirs)
        {
            Vector2Int neighbor = center + dir;
            if (CanPlaceObstacle(center, neighbor))
            {
                result.Add((center, neighbor));
            }
        }

        return result;
    }

    #endregion
}