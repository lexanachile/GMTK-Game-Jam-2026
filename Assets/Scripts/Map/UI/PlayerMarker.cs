using UnityEngine;

public class PlayerMarker : MonoBehaviour
{
    public Transform player;

    private RectTransform rect;
    private RectTransform mapRect;

    private MapDatabase database;
    private MapSettings settings;

    private void Start()
    {
        rect = GetComponent<RectTransform>();
        mapRect = transform.parent.GetComponent<RectTransform>();

        database = MapDatabase.Instance;
        settings = database.settings;
    }

    private void Update()
    {
        Vector2Int cell = database.WorldToCell(player.position);

        float x = (float)cell.x / (database.WorldWidth - 1);
        float y = (float)cell.y / (database.WorldHeight - 1);

        rect.anchoredPosition = new Vector2(
            (x - 0.5f) * mapRect.rect.width,
            (y - 0.5f) * mapRect.rect.height
        );
    }
}