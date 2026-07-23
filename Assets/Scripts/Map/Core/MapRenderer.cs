using UnityEngine;
using UnityEngine.UI;

public class MapRenderer : MonoBehaviour
{
    public static MapRenderer Instance;

    public RawImage mapImage;

    Texture2D texture;

    MapDatabase database;
    MapSettings settings;

    bool dirty;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        database = MapDatabase.Instance;
        settings = database.settings;

        texture = new Texture2D(database.WorldWidth, database.WorldHeight);
        texture.filterMode = FilterMode.Point;

        mapImage.texture = texture;

        ClearMap();
    }

    void LateUpdate()
    {
        if (!dirty)
            return;

        texture.Apply();

        dirty = false;
    }

    public void UpdateCell(int x, int y)
    {
        MapCell cell = database.Cells[x, y];

        Color color = settings.unexploredColor;

        if (cell.explored)
        {
            switch (cell.type)
            {
                case MapCellType.Empty:
                    color = settings.emptyColor;
                    break;

                case MapCellType.Rock:
                    color = settings.rockColor;
                    break;

                case MapCellType.Water:
                    color = settings.waterColor;
                    break;

                case MapCellType.Road:
                    color = settings.roadColor;
                    break;

                case MapCellType.Forest:
                    color = settings.forestColor;
                    break;

                case MapCellType.Building:
                    color = settings.buildingColor;
                    break;
            }
        }

        texture.SetPixel(x, y, color);

        dirty = true;
    }

    void ClearMap()
    {
        for (int x = 0; x < database.WorldWidth; x++)
        {
            for (int y = 0; y < database.WorldHeight; y++)
            {
                texture.SetPixel(x, y, settings.unexploredColor);
            }
        }

        texture.Apply();
    }
}