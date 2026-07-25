using UnityEngine;
using UnityEngine.Tilemaps;

public class GridManager : MonoBehaviour
{
    [Header("Map Bounds")]
    public Vector2 mapOrigin = Vector2.zero;   // левый нижний угол мира
    public Vector2 mapSize = new Vector2(20, 20);
    public float cellSize = 1f;

    [Header("Obstacle Detection")]
    public LayerMask obstacleMask;

    private bool[,] walkable;
    private int gridWidth, gridHeight;

    private void Awake()
    {
        gridWidth = Mathf.CeilToInt(mapSize.x / cellSize);
        gridHeight = Mathf.CeilToInt(mapSize.y / cellSize);
        walkable = new bool[gridWidth, gridHeight];

        // Заполняем сетку: проверяем каждую клетку на наличие коллайдера-препятствия
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector2 cellCenter = CellToWorld(x, y);
                Collider2D hit = Physics2D.OverlapBox(cellCenter,
                    Vector2.one * cellSize * 0.9f, 0f, obstacleMask);
                // Коллайдеры тайлмапов (земля/фон) не считаем препятствиями
                if (hit != null && (hit is TilemapCollider2D ||
                    (hit is CompositeCollider2D && hit.GetComponent<TilemapCollider2D>() != null)))
                    hit = null;
                walkable[x, y] = (hit == null);
            }
        }
    }

    public Vector2 CellToWorld(int x, int y)
    {
        float worldX = mapOrigin.x + (x + 0.5f) * cellSize;
        float worldY = mapOrigin.y + (y + 0.5f) * cellSize;
        return new Vector2(worldX, worldY);
    }

    public bool WorldToCell(Vector2 worldPos, out int x, out int y)
    {
        x = Mathf.FloorToInt((worldPos.x - mapOrigin.x) / cellSize);
        y = Mathf.FloorToInt((worldPos.y - mapOrigin.y) / cellSize);
        return (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight);
    }

    public bool IsWalkable(Vector2 worldPos)
    {
        int x, y;
        if (!WorldToCell(worldPos, out x, out y))
            return false;
        return walkable[x, y];
    }

    public bool IsWalkable(int x, int y)
    {
        if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
            return walkable[x, y];
        return false;
    }

    // Попытаться найти случайную проходимую клетку внутри заданного прямоугольника,
    // в которой ещё нет монстра.
    public Vector2? GetRandomFreeCellInRect(Rect area, LayerMask monsterLayer)
    {
        int minX = Mathf.FloorToInt((area.xMin - mapOrigin.x) / cellSize);
        int maxX = Mathf.FloorToInt((area.xMax - mapOrigin.x) / cellSize);
        int minY = Mathf.FloorToInt((area.yMin - mapOrigin.y) / cellSize);
        int maxY = Mathf.FloorToInt((area.yMax - mapOrigin.y) / cellSize);

        minX = Mathf.Clamp(minX, 0, gridWidth - 1);
        maxX = Mathf.Clamp(maxX, 0, gridWidth - 1);
        minY = Mathf.Clamp(minY, 0, gridHeight - 1);
        maxY = Mathf.Clamp(maxY, 0, gridHeight - 1);

        for (int attempt = 0; attempt < 100; attempt++)
        {
            int x = Random.Range(minX, maxX + 1);
            int y = Random.Range(minY, maxY + 1);
            if (walkable[x, y])
            {
                Vector2 pos = CellToWorld(x, y);
                if (Physics2D.OverlapCircle(pos, cellSize * 0.3f, monsterLayer) == null)
                    return pos;
            }
        }
        return null;
    }

    // Случайная свободная клетка в кольце вокруг точки (для LiveSpawner)
    public Vector2? GetRandomFreeCellInRing(Vector2 center, float minDist, float maxDist, LayerMask monsterLayer)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            int x = Random.Range(0, gridWidth);
            int y = Random.Range(0, gridHeight);
            if (!walkable[x, y])
                continue;

            Vector2 pos = CellToWorld(x, y);
            float dist = Vector2.Distance(pos, center);
            if (dist >= minDist && dist <= maxDist)
            {
                if (Physics2D.OverlapCircle(pos, cellSize * 0.3f, monsterLayer) == null)
                    return pos;
            }
        }
        return null;
    }
}