using UnityEngine;

public class MapObject : MonoBehaviour
{
    [Header("Map")]
    public MapCellType type;

    [Tooltip("Show on map")]
    public bool visibleOnMap = true;
}