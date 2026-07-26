using UnityEngine;

public class MapScanner : MonoBehaviour
{
    public Transform player;

    private MapDatabase database;
    private MapSettings settings;

    private void Start()
    {
        database = MapDatabase.Instance;
        settings = database.settings;

        InvokeRepeating(nameof(Scan), 0f, 0.2f);
    }

    void Scan()
    {
        RevealAroundPlayer();
        RevealObjects();
    }

    void RevealAroundPlayer()
    {
        Vector2Int playerCell = database.WorldToCell(player.position);

        for (int x = -settings.scanRadius; x <= settings.scanRadius; x++)
        {
            for (int y = -settings.scanRadius; y <= settings.scanRadius; y++)
            {
                if (x * x + y * y > settings.scanRadius * settings.scanRadius)
                    continue;

                Vector2Int cell = playerCell + new Vector2Int(x, y);

                if (!database.IsInside(cell))
                    continue;

                if (!database.Cells[cell.x, cell.y].explored)
                {
                    database.Cells[cell.x, cell.y].explored = true;
                    database.Cells[cell.x, cell.y].type = MapCellType.Empty;

                    MapRenderer.Instance.UpdateCell(cell.x, cell.y);
                }
            }
        }
    }

    void RevealObjects()
    {
        foreach (var obj in MapRegistry.Instance.Objects)
        {
            if (!obj.visibleOnMap)
                continue;

            Collider2D col = obj.GetComponent<Collider2D>();

            if (col == null)
                continue;

            Bounds bounds = col.bounds;

            Vector2Int min = database.WorldToCell(bounds.min);
            Vector2Int max = database.WorldToCell(bounds.max);

            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);

                    if (!database.IsInside(cell))
                        continue;

                    if (!database.Cells[cell.x, cell.y].explored)
                        continue;

                    Vector3 worldPos = database.CellToWorld(cell);

                    if (!col.OverlapPoint(worldPos))
                        continue;

                    if (database.Cells[cell.x, cell.y].type != obj.type)
                    {
                        database.Cells[cell.x, cell.y].type = obj.type;
                        MapRenderer.Instance.UpdateCell(cell.x, cell.y);
                    }
                }
            }
        }
    }
}