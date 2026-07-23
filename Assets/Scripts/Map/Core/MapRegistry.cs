using System.Collections.Generic;
using UnityEngine;

public class MapRegistry : MonoBehaviour
{
    public static MapRegistry Instance;

    private readonly List<MapObject> mapObjects = new();

    public IReadOnlyList<MapObject> Objects => mapObjects;

    private void Awake()
    {
        Instance = this;

        mapObjects.AddRange(FindObjectsByType<MapObject>(FindObjectsSortMode.None));
    }
}