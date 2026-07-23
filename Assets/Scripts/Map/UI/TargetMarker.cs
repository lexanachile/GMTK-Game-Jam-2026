using UnityEngine;

public class TargetMarker : MonoBehaviour
{
    private RectTransform rect;
    private RectTransform mapRect;

    private MapDatabase database;

    private void Start()
    {
        rect = GetComponent<RectTransform>();
        mapRect = transform.parent.GetComponent<RectTransform>();

        database = MapDatabase.Instance;
    }

    private void Update()
    {
        if (DestinationPoint.Instance == null)
            return;

        Vector2Int cell = database.WorldToCell(DestinationPoint.Instance.transform.position);

        float x = (float)cell.x / (database.WorldWidth - 1);
        float y = (float)cell.y / (database.WorldHeight - 1);

        rect.anchoredPosition = new Vector2(
            (x - 0.5f) * mapRect.rect.width,
            (y - 0.5f) * mapRect.rect.height
        );
    }
}