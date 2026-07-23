using UnityEngine;

[CreateAssetMenu(fileName = "MapSettings", menuName = "Map/Settings")]
public class MapSettings : ScriptableObject
{
    [Header("Grid")]
    [Tooltip("Размер одной клетки карты в мировых единицах")]
    public float cellSize = 1f;

    [Header("Scan")]
    public int scanRadius = 20;

    [Header("Colors")]
    public Color unexploredColor = Color.black;
    public Color emptyColor = new Color(0.2f, 0.2f, 0.2f);
    public Color rockColor = Color.red;
    public Color waterColor = Color.blue;
    public Color roadColor = Color.yellow;
    public Color forestColor = Color.green;
    public Color buildingColor = Color.white;

    [Header("View")]
    public int textureSize = 256;
    public float zoom = 1f;
}