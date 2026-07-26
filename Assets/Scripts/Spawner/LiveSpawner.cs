using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LiveSpawner : MonoBehaviour
{
    [Header("References")]
    public GridManager gridManager;
    public MonsterManager monsterManager;

    [Header("Spawn Settings")]
    public float spawnInterval = 3f;
    public int maxMonsters = 20;

    private List<Monster> activeMonsters = new List<Monster>();
    private float innerDist, middleDist, outerSleepDist;

    private IEnumerator Start()
    {
        // Читаем параметры из GameManager
        var gm = GameManager.Instance;
        innerDist = gm.innerSpawnDist;
        middleDist = gm.middleSpawnDist;
        outerSleepDist = gm.outerSleepDist;

        // Ждём кадр, чтобы все менеджеры инициализировались
        yield return null;
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

            // Убираем уничтоженных монстров из списка
            activeMonsters.RemoveAll(m => m == null);

            if (activeMonsters.Count < maxMonsters)
            {
                TrySpawn();
            }
        }
    }

    private void TrySpawn()
    {
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null) return;

        Vector2? spawnPos = gridManager.GetRandomFreeCellInRing(
            player.position,
            innerDist,
            middleDist,
            monsterManager.monsterLayer
        );

        if (spawnPos.HasValue)
        {
            Monster newMonster = monsterManager.SpawnMonster(spawnPos.Value);
            newMonster.Initialize(outerSleepDist);
            activeMonsters.Add(newMonster);
        }
    }
}