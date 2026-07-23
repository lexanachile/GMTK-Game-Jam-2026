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
        
        foreach (var obj in MapRegistry.Instance.Objects)
        {
            Vector2Int cell = database.WorldToCell(obj.transform.position);
            
            if (!database.IsInside(cell))
                continue;

            if (!database.Cells[cell.x, cell.y].explored)
                continue;

            MapCellType newType = obj.type;

            if (database.Cells[cell.x, cell.y].type != newType)
            {
                database.Cells[cell.x, cell.y].type = newType;

                MapRenderer.Instance.UpdateCell(cell.x, cell.y);
            }
        }
    }
}