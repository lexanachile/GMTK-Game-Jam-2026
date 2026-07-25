using UnityEngine;

public class MonsterManager : MonoBehaviour
{
    [Header("Monster Prefab & Layer")]
    public GameObject monsterPrefab;
    public LayerMask monsterLayer;

    // Создаёт монстра в указанной позиции и возвращает его компонент Monster
    public Monster SpawnMonster(Vector3 position)
    {
        GameObject go = Instantiate(monsterPrefab, position, Quaternion.identity);
        return go.GetComponent<Monster>();
    }

    // Проверяет, нет ли в точке монстра
    public bool IsPositionFree(Vector2 position)
    {
        return Physics2D.OverlapCircle(position, 0.3f, monsterLayer) == null;
    }
}