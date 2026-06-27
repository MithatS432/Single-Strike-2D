using UnityEngine;

public static class MoveUtility
{
    public static void MoveTo(Transform obj, Tile targetTile)
    {
        obj.position = targetTile.transform.position;
    }
}