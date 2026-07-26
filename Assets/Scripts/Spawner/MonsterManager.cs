using System.Collections.Generic;
using UnityEngine;

public class MonsterManager : MonoBehaviour
{
    [Header("Monster Prefab & Layer")]
    public GameObject monsterPrefab;
    public LayerMask monsterLayer;

    // Все монстры, заспавленные через этот менеджер
    private readonly List<Monster> spawnedMonsters = new List<Monster>();

    // Создаёт монстра в указанной позиции и возвращает его компонент Monster
    public Monster SpawnMonster(Vector3 position)
    {
        GameObject go = Instantiate(monsterPrefab, position, Quaternion.identity);
        Monster monster = go.GetComponent<Monster>();
        spawnedMonsters.Add(monster);
        return monster;
    }

    // Уничтожает всех живых монстров (вызывается при рестарте)
    public void DestroyAllMonsters()
    {
        foreach (Monster monster in spawnedMonsters)
        {
            if (monster != null)
                Destroy(monster.gameObject);
        }
        spawnedMonsters.Clear();
    }

    // Проверяет, нет ли в точке монстра
    public bool IsPositionFree(Vector2 position)
    {
        return Physics2D.OverlapCircle(position, 0.3f, monsterLayer) == null;
    }
}