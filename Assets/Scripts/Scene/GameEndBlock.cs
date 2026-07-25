using UnityEngine;
using System.Collections;

public class GameEndBlock : MonoBehaviour
{
    [Header("Префабы взрывов")]
    public GameObject[] explosionPrefabs;

    [Header("Количество взрывов")]
    public int minExplosions = 4;
    public int maxExplosions = 6;

    [Header("Размер взрывов")]
    public float minScale = 1f;
    public float maxScale = 2f;

    [Header("Разброс позиции")]
    public float maxOffsetX = 0.7f;
    public float maxOffsetY = 0.7f;

    [Header("Задержки между взрывами")]
    public float minDelay = 0f;
    public float maxDelay = 1.5f;

    private bool triggered = false;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryActivate(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryActivate(other.gameObject);
    }

    private void TryActivate(GameObject other)
    {
        if (triggered) return;

        CargoExplosion cargo = other.GetComponent<CargoExplosion>();
        if (cargo == null) return;   // реагируем только на коробку

        triggered = true;

        // Отключаем коллайдер и визуал, чтобы избежать повторных срабатываний
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        // Запускаем взрыв коробки (без показа рестарта) – мотоцикл остановится внутри
        cargo.ExplodeWithoutRestart();

        // Запускаем собственную серию взрывов
        StartCoroutine(ExplodeSequence());
    }

    private IEnumerator ExplodeSequence()
    {
        if (explosionPrefabs == null || explosionPrefabs.Length == 0)
        {
            ShowGameOverAndDestroy();
            yield break;
        }

        int count = Random.Range(minExplosions, maxExplosions + 1);
        float lastDelay = 0f;

        for (int i = 0; i < count; i++)
        {
            float delay = Random.Range(minDelay, maxDelay);
            if (delay > lastDelay) lastDelay = delay;

            GameObject prefab = explosionPrefabs[Random.Range(0, explosionPrefabs.Length)];
            Vector3 offset = new Vector3(Random.Range(-maxOffsetX, maxOffsetX),
                                         Random.Range(-maxOffsetY, maxOffsetY), 0f);
            Vector3 spawnPos = transform.position + offset;
            float scale = Random.Range(minScale, maxScale);

            StartCoroutine(SpawnSingleExplosion(prefab, spawnPos, scale, delay));
        }

        // Ждём завершения последнего взрыва, затем показываем меню конца игры
        yield return new WaitForSeconds(lastDelay + 0.1f);
        ShowGameOverAndDestroy();
    }

    private IEnumerator SpawnSingleExplosion(GameObject prefab, Vector3 position, float scale, float delay)
    {
        yield return new WaitForSeconds(delay);
        GameObject explosion = Instantiate(prefab, position, Quaternion.identity);
        explosion.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void ShowGameOverAndDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ShowGameEndMenu();

        Destroy(gameObject);
    }
}
