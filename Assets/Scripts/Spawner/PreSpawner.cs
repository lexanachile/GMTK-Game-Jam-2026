using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpawnZone
{
    public Rect area;        // Мировые координаты прямоугольника
    public float density;    // Монстров на квадратную единицу
}

public class PreSpawner : MonoBehaviour
{
    [Header("References")]
    public GridManager gridManager;
    public MonsterManager monsterManager;
    public List<SpawnZone> zones;

    [Header("Sleep Distance (same as GameManager)")]
    public float outerSleepDist = 10f;  // Должно совпадать с GameManager

    private IEnumerator Start()
    {
        // Ждём, чтобы GridManager точно проинициализировался
        yield return null;

        foreach (var zone in zones)
        {
            // Рассчитываем целевое количество монстров для зоны
            float areaSize = zone.area.width * zone.area.height;
            int targetCount = Mathf.RoundToInt(areaSize * zone.density);

            for (int i = 0; i < targetCount; i++)
            {
                Vector2? pos = gridManager.GetRandomFreeCellInRect(zone.area, monsterManager.monsterLayer);
                if (pos.HasValue)
                {
                    Monster monster = monsterManager.SpawnMonster(pos.Value);
                    monster.Initialize(outerSleepDist);
                }
                else
                {
                    Debug.LogWarning($"PreSpawner: not enough free cells in zone {zone.area}");
                    break;
                }
            }
        }
    }
}