using UnityEngine;

public class MapDatabase : MonoBehaviour
{
    public static MapDatabase Instance;

    public MapSettings settings;

    public MapCell[,] Cells;

    public int WorldWidth { get; private set; }
    public int WorldHeight { get; private set; }

    private MapBounds bounds;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        bounds = FindFirstObjectByType<MapBounds>();

        if (bounds == null)
        {
            Debug.LogError("MapBounds not found!");
            return;
        }

        WorldWidth = Mathf.CeilToInt(bounds.size.x / settings.cellSize);
        WorldHeight = Mathf.CeilToInt(bounds.size.y / settings.cellSize);

        Cells = new MapCell[WorldWidth, WorldHeight];
    }

    public Vector2Int WorldToCell(Vector2 worldPosition)
    {
        Vector2 min = (Vector2)bounds.Bounds.min;

        int x = Mathf.FloorToInt((worldPosition.x - min.x) / settings.cellSize);
        int y = Mathf.FloorToInt((worldPosition.y - min.y) / settings.cellSize);

        return new Vector2Int(x, y);
    }

    public bool IsInside(Vector2Int cell)
    {
        return cell.x >= 0 &&
               cell.y >= 0 &&
               cell.x < WorldWidth &&
               cell.y < WorldHeight;
    }
}